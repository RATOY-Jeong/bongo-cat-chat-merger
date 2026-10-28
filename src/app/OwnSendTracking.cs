using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using BongoChat.Protocol;
namespace BongoChat
{
    public sealed class OwnSendReceipt
    {
        public string id {get;set;}
        public string session {get;set;}
        public string room {get;set;}
        public string text {get;set;}
        public string utc {get;set;}
        public string echoId {get;set;}
        public bool accepted {get;set;}
    }
    // These are local send receipts, never invented room-received messages.
    public sealed class OwnSendStore
    {
        private readonly object gate=new object();
        private readonly object disk=new object();
        private List<OwnSendReceipt> records=new List<OwnSendReceipt>();
        private string file;
        private int revision;
        public string Error {get;private set;}
        public void Open(string game, CancellationToken cancel)
        {
            string key;using(var hash=SHA256.Create())key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(game).ToUpperInvariant()))).Replace("-","").Substring(0,24);
            string next=Path.Combine(AppStorage.Root,"own-sends",key+".json");
            lock(gate)
            {
                if(file==next)return;
                cancel.ThrowIfCancellationRequested();records=new List<OwnSendReceipt>();file=next;revision++;Error=null;
                try
                {
                    if(!File.Exists(file))return;
                    var loaded=new JavaScriptSerializer{MaxJsonLength=1048576}.Deserialize<List<OwnSendReceipt>>(Journal.ReadShared(file,1048576));
                    if(loaded!=null)records=loaded.Where(Valid).GroupBy(r=>r.id).Select(g=>g.First()).Take(500).ToList();
                }
                catch(OperationCanceledException){throw;}
                catch(Exception ex){Error=ex.GetType().Name;}
            }
        }
        private static bool Valid(OwnSendReceipt r)
        {
            DateTime utc;Guid id;
            return r!=null&&Guid.TryParseExact(r.id,"N",out id)&&Journal.RoomIdValid(r.room)&&!String.IsNullOrEmpty(r.session)
                &&!String.IsNullOrWhiteSpace(r.text)&&r.text.Length<=300&&!MeowMessages.IsMeow(r.text)
                &&DateTime.TryParse(r.utc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out utc);
        }
        public bool Record(SendRequest request,bool accepted,bool background)
        {
            var r=new OwnSendReceipt{id=request.id,session=request.session,room=request.room,text=request.text,utc=request.createdUtc,accepted=accepted};
            if(!Valid(r))return false;
            lock(gate){if(records.Any(x=>x.id==r.id))return false;records.Add(r);if(records.Count>500)records.RemoveAt(0);revision++;}
            Save(background);return true;
        }
        public bool Observe(List<ChatEntry> received,bool background)
        {
            bool changed=false;
            lock(gate)
            {
                var used=new HashSet<string>(records.Where(r=>!String.IsNullOrEmpty(r.echoId)).Select(r=>r.echoId),StringComparer.Ordinal);
                foreach(var r in records.Where(r=>String.IsNullOrEmpty(r.echoId)))
                {
                    DateTime start=DateTime.Parse(r.utc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
                    var echo=received.FirstOrDefault(e=>e.kind=="chat"&&e.mine&&e.room==r.room&&e.session==r.session&&e.text==r.text&&!used.Contains(e.id)&&InRange(e.utc,start));
                    if(echo!=null){r.echoId=echo.id;used.Add(echo.id);changed=true;}
                }
                if(changed)revision++;
            }
            if(changed)Save(background);return changed;
        }
        private static bool InRange(string text,DateTime start)
        {
            DateTime end;return DateTime.TryParse(text,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out end)
                &&end.ToUniversalTime()>=start.AddSeconds(-1)&&end.ToUniversalTime()<=start.AddMinutes(2);
        }
        public List<ChatEntry> Missing(string room)
        {
            lock(gate)return records.Where(r=>r.room==room&&String.IsNullOrEmpty(r.echoId)).Select(r=>new ChatEntry{kind="own_send",id="own:"+r.id,session=r.session,room=r.room,mine=true,text=r.text,utc=r.utc,delivery=r.accepted?"unconfirmed":"rejected"}).ToList();
        }
        public void Save(bool background)
        {
            string target,json;int version;
            lock(gate){target=file;version=revision;json=new JavaScriptSerializer{MaxJsonLength=1048576}.Serialize(records);}
            if(target==null)return;
            Action write=delegate
            {
                lock(disk)
                {
                    lock(gate)if(target!=file||version!=revision)return;
                    string temp=target+".tmp";
                    try{Directory.CreateDirectory(Path.GetDirectoryName(target));File.WriteAllText(temp,json,new UTF8Encoding(false));if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);Error=null;}
                    catch(Exception ex){Error=ex.GetType().Name;try{File.Delete(temp);}catch{}}
                }
            };
            if(background)Task.Factory.StartNew(write);else write();
        }
    }
    public sealed partial class ChatWindow
    {
        private readonly OwnSendStore ownSends=new OwnSendStore();
        private List<ChatEntry> receivedEntries=new List<ChatEntry>();
        private void MergeOwnSends()
        {
            ownSends.Observe(receivedEntries,backgroundIo);
            var local=ownSends.Missing(currentRoom);
            var merged=receivedEntries.Concat(local).OrderBy(e=>e.utc,StringComparer.Ordinal).ToList();
            int normal=merged.Count(e=>e.kind!="meow_group");
            while(normal>500&&merged.Count>0){if(merged[0].kind!="meow_group")normal--;merged.RemoveAt(0);}
            while(merged.Count>0&&merged[0].kind=="meow_group"&&normal>=500)merged.RemoveAt(0);
            entries=merged;
        }
        private string OwnSendLabel(ChatEntry entry)
        {
            return L(entry.delivery=="rejected"?"전송 거절 · 내 PC 기록":"게임 처리 · 방 수신 미확인");
        }
    }
}