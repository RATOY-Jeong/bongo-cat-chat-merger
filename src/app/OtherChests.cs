using System;using System.IO;using System.Text;using System.Threading.Tasks;using System.Windows.Controls;
namespace BongoChat
{
 public sealed partial class ChatWindow
 {
  private bool autoOtherChests;
  private CheckBox otherChestToggle;private TextBlock otherChestLabel,otherChestHint;
  private readonly AutoChestLeaseWriter otherChestWriter=new AutoChestLeaseWriter(true);
  private readonly string otherChestPreference=AppStorage.PathFor("other-chests.txt");
  private void InitializeOtherChests(Grid root)
  {
   otherChestToggle=(CheckBox)root.FindName("AutoOtherChests");otherChestLabel=(TextBlock)root.FindName("AutoOtherChestLabel");otherChestHint=(TextBlock)root.FindName("AutoOtherChestHint");
   try{autoOtherChests=!demo&&File.ReadAllText(otherChestPreference).Trim()=="true";}catch{autoOtherChests=false;}
   otherChestToggle.IsChecked=autoOtherChests;otherChestToggle.IsEnabled=!demo;
   otherChestToggle.Checked+=delegate{ChangeOtherChests(true);};otherChestToggle.Unchecked+=delegate{ChangeOtherChests(false);};
   Closed+=delegate{autoOtherChests=false;otherChestWriter.Invalidate();SendOtherChestLease(false);};RefreshOtherChestLabels();
  }
  private void ChangeOtherChests(bool enabled)
  {
   autoOtherChests=enabled;otherChestWriter.Invalidate();
   try{File.WriteAllText(otherChestPreference,enabled?"true":"false",new UTF8Encoding(false));}catch{sendNotice="다른 사람 상자 옵션은 적용됐지만 설정을 저장하지 못했어요.";}
   SendOtherChestLease(enabled);RefreshOtherChestLabels();
   if(enabled&&automaticSetupEnabled&&liveState!=null&&!BridgeSetup.MeetsRequiredBridgeVersion(liveState.version))BeginSetup(false);
  }
  private void SendOtherChestLease(bool enabled)
  {
   if(demo||liveState==null)return;string game=gamePath;var state=liveState;int revision=otherChestWriter.Revision;
   Action write=delegate{try{otherChestWriter.Write(game,state,enabled&&!connectionPaused&&!setupBusy,revision);}catch{}};
   if(backgroundIo)Task.Factory.StartNew(write);else write();
  }
  private void RefreshOtherChestLabels()
  {
   if(otherChestToggle==null)return;
   otherChestLabel.Text=L("다른 사람 상자 자동 열기");
   otherChestToggle.ToolTip=L("내 게임 포인트를 사용하여 같은 방 다른 사람의 준비된 일반·이모티콘 상자를 하나씩 열어 줍니다. 상대가 상자를 받고 내 포인트가 차감됩니다. 내 상자 자동 열기와 별도이며, 끄거나 종료하면 6초 안에 새 요청을 멈춥니다.");
   string label=!autoOtherChests?"내 포인트 사용 · 꺼짐":connectionPaused?"연결을 켜야 자동으로 열어요":liveState==null?"게임 연결 대기":!liveState.autoOtherChestSupported?(BridgeSetup.MeetsRequiredBridgeVersion(liveState.version)?"현재 게임의 상자 기능을 지원하지 않아요":"연결 도우미에서 플러그인을 업데이트해 주세요"):!Journal.RoomIdValid(liveState.room)?"내 포인트 사용 · 방 입장 대기":liveState.autoOtherChestState=="error"||liveState.autoOtherChestState=="limit"?"열기 대기 · 껐다 켜서 재시도":liveState.autoOtherChestState=="opening"||liveState.autoOtherChestState=="sent"?"내 포인트 사용 · 선물 처리 중":"내 포인트 사용 · 다른 상자 대기";
   otherChestHint.Text=L(label);otherChestHint.ToolTip=otherChestToggle.ToolTip;
  }
 }
}