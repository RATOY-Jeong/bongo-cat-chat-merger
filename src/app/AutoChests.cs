using System;using System.IO;using System.Text;using System.Threading;using System.Threading.Tasks;using System.Web.Script.Serialization;using System.Windows;using System.Windows.Controls;using BongoChat.Protocol;
namespace BongoChat
{
    public sealed class AutoChestLeaseWriter
    {
        private readonly object writeGate=new object();
        private readonly bool others;
        public AutoChestLeaseWriter():this(false){} 
        public AutoChestLeaseWriter(bool otherMembers){others=otherMembers;}
        private int revision;
        private readonly string owner=Guid.NewGuid().ToString("N");
        public int Revision {get{return Volatile.Read(ref revision);}}
        public void Invalidate(){Interlocked.Increment(ref revision);}
        public void Write(string game,BridgeState state,bool enabled,int expected)
        {
            if(String.IsNullOrEmpty(game)||state==null||String.IsNullOrEmpty(state.session)||expected!=Revision)return;
            DateTime stamp;bool fresh=DateTime.TryParse(state.utc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out stamp)&&Math.Abs((DateTime.UtcNow-stamp.ToUniversalTime()).TotalSeconds)<6;
            var now=DateTime.UtcNow;var lease=new AutoChestLease{protocol=1,kind=others?"other_chests":"auto_chests",room=others?state.room:null,owner=owner,session=state.session,enabled=enabled&&fresh&&state.protocol==1&&state.ready&&!state.stopped&&(others?state.autoOtherChestSupported&&Journal.RoomIdValid(state.room):state.autoChestSupported),createdUtc=now.ToString("o"),expiresUtc=now.AddSeconds(5).ToString("o")};
            string folder=Path.Combine(game,"BepInEx","BongoChat"),file=Path.Combine(folder,others?"other-chests.json":"auto-chests.json"),temp=file+"."+owner+".tmp";
            if(!Directory.Exists(folder))return;
            lock(writeGate)
            {
                if(expected!=Revision)return;
                try
                {
                    File.WriteAllText(temp,new JavaScriptSerializer().Serialize(lease),new UTF8Encoding(false));
                    if(expected!=Revision){File.Delete(temp);return;}
                    if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
                    if(expected!=Revision)try{File.Delete(file);}catch{}
                }
                finally{try{File.Delete(temp);}catch{}}
            }
        }
    }
    public sealed partial class ChatWindow
    {
        private bool autoChests;
        private CheckBox autoChestToggle;
        private TextBlock autoChestHint;
        private readonly AutoChestLeaseWriter autoChestWriter=new AutoChestLeaseWriter();
        private readonly string autoChestPreference=AppStorage.PathFor("auto-chests.txt");
        private void InitializeAutoChests(Grid root)
        {
            autoChestToggle=(CheckBox)root.FindName("AutoChests");autoChestHint=(TextBlock)root.FindName("AutoChestHint");
            try{autoChests=!demo&&File.ReadAllText(autoChestPreference).Trim()=="true";}catch{autoChests=false;}
            autoChestToggle.IsChecked=autoChests;autoChestToggle.IsEnabled=!demo;
            autoChestToggle.Checked+=delegate{ChangeAutoChests(true);};autoChestToggle.Unchecked+=delegate{ChangeAutoChests(false);};
            Closed+=delegate{autoChests=false;autoChestWriter.Invalidate();SendChestLease(false);};
            RefreshAutoChestLabels();
        }
        private void ChangeAutoChests(bool enabled)
        {
            autoChests=enabled;autoChestWriter.Invalidate();
            try{File.WriteAllText(autoChestPreference,enabled?"true":"false",new UTF8Encoding(false));}catch{sendNotice="상자 자동 열기는 적용됐지만 설정을 저장하지 못했어요.";}
            SendChestLease(enabled);RefreshAutoChestLabels();
            if(enabled&&automaticSetupEnabled&&liveState!=null&&!BridgeSetup.MeetsRequiredBridgeVersion(liveState.version))BeginSetup(false);
        }
        private void SendChestLease(bool enabled)
        {
            if(demo||liveState==null)return;string game=gamePath;var state=liveState;int version=autoChestWriter.Revision;
            Action write=delegate{try{autoChestWriter.Write(game,state,enabled&&!connectionPaused&&!setupBusy,version);}catch{}};
            if(backgroundIo)Task.Factory.StartNew(write);else write();
        }
        private void RefreshAutoChestLabels()
        {
            if(autoChestToggle==null)return;
            autoChestToggle.Content=L("상자 자동 열기");autoChestToggle.ToolTip=L("준비된 일반·이모티콘 상자를 게임 포인트를 사용하여 하나씩 엽니다. 머지어 실행 중에만 동작합니다. 끄거나 종료하면 최대 6초 안에 새 요청을 멈춥니다. 진행 중인 열기는 완료됩니다.");
            string label=!autoChests?"자동 열기 꺼짐":connectionPaused?"연결을 켜야 자동으로 열어요":liveState==null?"게임 연결 대기":!liveState.autoChestSupported?(BridgeSetup.MeetsRequiredBridgeVersion(liveState.version)?"현재 게임의 상자 기능을 지원하지 않아요":"연결 도우미에서 플러그인을 업데이트해 주세요"):liveState.autoChestState=="error"||liveState.autoChestState=="retry"?"열기 대기 · 껐다 켜서 재시도":liveState.autoChestState=="opening"||liveState.autoChestState=="normal"||liveState.autoChestState=="emote"?"상자를 여는 중…":"상자 준비·포인트 대기";
            autoChestHint.Text=L(label);autoChestHint.ToolTip=autoChestToggle.ToolTip;
        }
    }
}