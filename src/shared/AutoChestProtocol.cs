using System;
using System.Globalization;
namespace BongoChat.Protocol
{
    public sealed class AutoChestLease
    {
        public int protocol {get;set;}
        public string kind {get;set;}
        public string owner {get;set;}
        public string session {get;set;}
        public bool enabled {get;set;}
        public string room {get;set;}
        public string createdUtc {get;set;}
        public string expiresUtc {get;set;}
    }
    public static class AutoChestPolicy
    {
        public static bool Valid(AutoChestLease lease,string session,DateTime now) { return ValidKind(lease,session,now,"auto_chests"); }
        public static bool ValidForOthers(AutoChestLease lease,string session,string room,DateTime now)
        { ulong id;return UInt64.TryParse(room,out id)&&id!=0&&ValidKind(lease,session,now,"other_chests")&&lease.room==room; }
        private static bool ValidKind(AutoChestLease lease,string session,DateTime now,string kind)
        {
            DateTime made,expires;Guid owner;
            if(lease==null||lease.protocol!=1||lease.kind!=kind||String.IsNullOrEmpty(session)||lease.session!=session||!Guid.TryParseExact(lease.owner,"N",out owner))return false;
            if(!DateTime.TryParse(lease.createdUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out made)||!DateTime.TryParse(lease.expiresUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out expires))return false;
            made=made.ToUniversalTime();expires=expires.ToUniversalTime();now=now.ToUniversalTime();
            return made<=now.AddSeconds(1)&&made>=now.AddSeconds(-6)&&expires>now&&expires>made&&expires<=made.AddSeconds(6);
        }
    }
    public sealed class ChestReadiness
    {
        public bool Known=true,Ready,CanOpen,Busy;
    }
    public sealed class AutoChestGate
    {
        private int attempted;
        private double nextAttempt;
        public string State {get;private set;}
        public void Reset(){attempted=0;nextAttempt=0;State="off";}
        public int Next(bool enabled,ChestReadiness normal,ChestReadiness emote,double seconds)
        {
            if(normal==null||emote==null){State="waiting";return 0;}
            // Only a real game readiness transition rearms a consumed/requested chest.
            if(normal.Known&&!normal.Ready)attempted&=~1;if(emote.Known&&!emote.Ready)attempted&=~2;
            if(!enabled){State="off";return 0;}
            if(!normal.Known||!emote.Known){State="waiting";return 0;}
            if(normal.Busy||emote.Busy){State="opening";return 0;}
            if(seconds<nextAttempt){State="waiting";return 0;}
            int kind=normal.Ready&&normal.CanOpen&&(attempted&1)==0?1:emote.Ready&&emote.CanOpen&&(attempted&2)==0?2:0;
            if(kind>0){attempted|=kind;nextAttempt=seconds+2;State=kind==1?"normal":"emote";return kind;}
            State=(normal.Ready&&normal.CanOpen&&(attempted&1)!=0)||(emote.Ready&&emote.CanOpen&&(attempted&2)!=0)?"retry":"waiting";return 0;
        }
    }
}