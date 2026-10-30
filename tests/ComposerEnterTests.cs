using System;using System.IO;using System.Reflection;using System.Windows;using System.Windows.Controls;using System.Windows.Input;using System.Windows.Interop;using System.Windows.Threading;using BongoChat;using BongoChat.Protocol;
public static class ComposerEnterTests
{
 static int checks;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Get(ChatWindow w,string field){return typeof(ChatWindow).GetField(field,flags).GetValue(w);}
 static void Set(ChatWindow w,string field,object value){typeof(ChatWindow).GetField(field,flags).SetValue(w,value);}
 static void Call(ChatWindow w,string method){typeof(ChatWindow).GetMethod(method,flags).Invoke(w,null);}
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static void Pump(ChatWindow w){w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));}
 sealed class Keys:KeyboardDevice
 {
  bool shift;public Keys(bool value):base(InputManager.Current){shift=value;}
  protected override KeyStates GetKeyStatesFromSystem(Key key){return shift&&(key==Key.LeftShift||key==Key.RightShift)?KeyStates.Down:KeyStates.None;}
 }
 static KeyEventArgs Enter(TextBox box,PresentationSource source,bool shift=false,bool ime=false)
 {
  var e=new KeyEventArgs(new Keys(shift),source,Environment.TickCount,Key.Return);
  if(ime)typeof(KeyEventArgs).GetMethod("MarkImeProcessed",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(e,null);
  e.RoutedEvent=Keyboard.PreviewKeyDownEvent;box.RaiseEvent(e);return e;
 }
 static void Reset(ChatWindow w,TextBox box){Set(w,"pendingRequest",null);Set(w,"sendAfterComposition",false);Set(w,"imeComposing",false);box.Text="";}
 [STAThread]public static int Main(string[] args){try{
  string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);AppStorage.Root=root;
  string game=Path.Combine(root,"fixture-game"),folder=Path.Combine(game,"BepInEx","BongoChat");Directory.CreateDirectory(Path.Combine(folder,"commands","results"));File.WriteAllText(Path.Combine(game,"BongoCat.exe"),"");
  var state=new BridgeState{protocol=1,ready=true,canSend=true,session="enter-test",room="123",maxMessageLength=300,utc=DateTime.UtcNow.ToString("o")};File.WriteAllText(Path.Combine(folder,"state.json"),Journal.Json.Serialize(state));
  TabletCompatibility.Configure();new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  var w=new ChatWindow(false,game,true,"ko");var box=(TextBox)Get(w,"composer");
  using(var source=new HwndSource(new HwndSourceParameters("Composer Enter isolated tests"){Width=1,Height=1,WindowStyle=0})){
   box.Text="English";Check(Enter(box,source).Handled&&Get(w,"pendingRequest")!=null,"ordinary Enter is consumed and sends");
   Reset(w,box);box.Text="한글";Set(w,"imeComposing",true);
   Check(Enter(box,source).Handled&&Get(w,"pendingRequest")==null&&box.Text=="한글","composition Enter is consumed without clearing uncommitted text");
   Enter(box,source);box.AppendText("완성");Call(w,"FinishCompositionLater");Pump(w);
   var request=(SendRequest)Get(w,"pendingRequest");Check(request!=null&&request.text=="한글완성","deferred send includes the committed final syllable");
   string firstId=request.id;Call(w,"FinishCompositionLater");Pump(w);Check(((SendRequest)Get(w,"pendingRequest")).id==firstId,"duplicate completion and Enter do not send twice");
   Reset(w,box);box.Text="경계";Set(w,"imeComposing",true);Call(w,"FinishCompositionLater");Check(Enter(box,source).Handled,"Enter at completion boundary is consumed");Pump(w);Check(Get(w,"pendingRequest")!=null,"Enter before deferred completion still sends once");
   Reset(w,box);box.Text="줄바꿈";Check(!Enter(box,source,true).Handled&&Get(w,"pendingRequest")==null&&box.AcceptsReturn,"Shift+Enter retains TextBox newline behavior without sending");
   Set(w,"imeComposing",true);Enter(box,source,true);Call(w,"FinishCompositionLater");Pump(w);Check(Get(w,"pendingRequest")==null,"Shift+Enter during composition never arms a delayed send");
   Reset(w,box);box.Text="IME";Check(Enter(box,source,false,true).Handled&&Get(w,"pendingRequest")!=null,"IME-processed Enter is recognized");
   Reset(w,box);box.Text="미완성";Set(w,"imeComposing",true);Enter(box,source,false,true);Call(w,"FinishCompositionLater");Pump(w);Check(Get(w,"pendingRequest")!=null,"IME-processed Enter waits for composition and then sends");
   Reset(w,box);box.Text="취소";Set(w,"imeComposing",true);Enter(box,source);box.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice,0,box,null){RoutedEvent=Keyboard.LostKeyboardFocusEvent});Pump(w);Check(Get(w,"pendingRequest")==null&&box.Text=="취소","losing focus cancels delayed send and preserves draft");
   Reset(w,box);box.Text="연결 끊김";Set(w,"imeComposing",true);Enter(box,source);Set(w,"canSend",false);Call(w,"FinishCompositionLater");Pump(w);Check(Get(w,"pendingRequest")==null&&box.Text=="연결 끊김","disconnect before completion preserves unsent draft");Set(w,"canSend",true);
   Reset(w,box);box.Text="첫 조합";Set(w,"imeComposing",true);Enter(box,source);Call(w,"FinishCompositionLater");
   var next=new TextComposition(InputManager.Current,box,"새 조합");box.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,next){RoutedEvent=TextCompositionManager.PreviewTextInputStartEvent});Pump(w);
   Check((bool)Get(w,"imeComposing")&&!(bool)Get(w,"sendAfterComposition")&&Get(w,"pendingRequest")==null,"new composition cancels stale completion and its pending send");
  }w.Close();Application.Current.Shutdown();Console.WriteLine("All "+checks+" composer Enter checks passed. Physical IME still requires manual verification.");return 0;
 }catch(Exception ex){Console.WriteLine(ex);if(Application.Current!=null)Application.Current.Shutdown();return 1;}}
}
