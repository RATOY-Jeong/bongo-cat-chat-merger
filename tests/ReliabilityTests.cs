using System; using System.IO; using System.Reflection; using System.Runtime.InteropServices; using System.Threading;
using System.Windows; using System.Windows.Controls; using System.Windows.Interop; using System.Windows.Threading;
using BongoChat; using BongoChat.Protocol;
public static class ReliabilityTests {
 static int checks; static BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(ChatWindow w,string name){return typeof(ChatWindow).GetField(name,flags).GetValue(w);}
 static void Set(ChatWindow w,string name,object value){typeof(ChatWindow).GetField(name,flags).SetValue(w,value);}
 static void Call(ChatWindow w,string name){typeof(ChatWindow).GetMethod(name,flags).Invoke(w,null);}
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static void Pump(ChatWindow w,Func<bool> done){
  DateTime end=DateTime.UtcNow.AddSeconds(5);
  while(!done()&&DateTime.UtcNow<end){w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));Thread.Sleep(5);}
  if(!done())throw new Exception("Async fixture timed out");
 }

 [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowStyle(IntPtr hwnd,int index);
 static SendRequest Send(ChatWindow w,string text){((TextBox)Get(w,"composer")).Text=text;Call(w,"SendComposer");return (SendRequest)Get(w,"pendingRequest");}
 static string Reply(string folder,SendRequest r){return Path.Combine(folder,"commands","results",r.id+".json");}
 static void Ack(string folder,SendRequest r,bool accepted,string error){
  File.Delete(Path.Combine(folder,"commands",r.id+".request.json"));
  File.WriteAllText(Reply(folder,r),Journal.Json.Serialize(new SendResult{id=r.id,session=r.session,room=r.room,accepted=accepted,error=error}));
 }
 [STAThread] public static int Main(string[] args){try{
  TabletCompatibility.Configure();new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);AppStorage.Root=root;
  string game=Path.Combine(root,"game"),bridge=Path.Combine(game,"BepInEx","BongoChat");Directory.CreateDirectory(Path.Combine(bridge,"commands","results"));File.WriteAllText(Path.Combine(game,"BongoCat.exe"),"");
  var state=new BridgeState{protocol=1,ready=true,canSend=true,session=Guid.NewGuid().ToString("N"),room="123",utc=DateTime.UtcNow.ToString("o"),maxMessageLength=50};
  string statePath=Path.Combine(bridge,"state.json");File.WriteAllText(statePath,Journal.Json.Serialize(state));
  var w=new ChatWindow(false,game,true,"ko");var input=(TextBox)Get(w,"composer");var send=(Button)Get(w,"sendButton");
  foreach(string text in new[]{"....",",,,,","......",",,,,,,","문장....","...,,,"}){
   var r=Send(w,text);Check(r!=null&&File.ReadAllText(Path.Combine(bridge,"commands",r.id+".request.json")).Contains(text),"punctuation accepted without an app-side repetition rule: "+text);Ack(bridge,r,true,null);w.Poll();
  }
  var rejected=Send(w,"....");Ack(bridge,rejected,false,"게임의 채팅 필터가 전송을 허용하지 않았어요.");w.Poll();
  Check(input.Text=="...."&&Get(w,"pendingRequest")==null&&!input.IsReadOnly&&send.IsEnabled,"filter rejection keeps the draft and leaves input/send usable");
  Check(((TextBlock)Get(w,"composerHint")).Text.Contains("말풍선")&&((TextBlock)Get(w,"composerHint")).Text.Contains("방으로"),"filter guidance distinguishes a local bubble from room broadcast");
  var invalid=Send(w,"응답 확인");File.WriteAllText(Reply(bridge,invalid),Journal.Json.Serialize(new SendResult{id="wrong-id",session=state.session,room="123",accepted=true}));Set(w,"pendingSince",DateTime.UtcNow.AddSeconds(-30));w.Poll();
  Check(Get(w,"pendingRequest")==null&&!input.IsReadOnly,"mismatched acknowledgement cannot bypass the send deadline");
  var broken=Send(w,"잘못된 응답");File.WriteAllText(Reply(bridge,broken),"{broken");Set(w,"pendingSince",DateTime.UtcNow.AddSeconds(-30));w.Poll();
  Check(Get(w,"pendingRequest")==null&&input.Text=="잘못된 응답","malformed acknowledgement recovers without losing the draft or throwing on the UI");
  var locked=Send(w,"잠긴 파일");File.WriteAllText(Reply(bridge,locked),"{}");
  using(var hold=new FileStream(Reply(bridge,locked),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
   Set(w,"pendingSince",DateTime.UtcNow.AddSeconds(-30));w.Poll();Check(Get(w,"pendingRequest")==null&&!input.IsReadOnly,"locked response files cannot leave the editor permanently locked");
  }
  var large=Send(w,"큰 응답");File.WriteAllText(Reply(bridge,large),new string('x',20000));Set(w,"pendingSince",DateTime.UtcNow.AddSeconds(-30));w.Poll();
  Check(Get(w,"pendingRequest")==null,"oversized responses cannot bypass the deadline");
  var draft=Send(w,"첫 문장");input.Text="전송 중 작성한 다음 문장";Ack(bridge,draft,true,null);w.Poll();
  Check(input.Text=="전송 중 작성한 다음 문장"&&!input.IsReadOnly,"an acknowledgement cannot erase a new draft written during sending");
  bool repeated=true;
  for(int i=0;i<30;i++){state.utc=DateTime.UtcNow.ToString("o");File.WriteAllText(statePath,Journal.Json.Serialize(state));var r=Send(w,"cycle "+i);Ack(bridge,r,i%3!=0,i%3==0?"게임의 채팅 필터가 전송을 허용하지 않았어요.":null);w.Poll();repeated&=Get(w,"pendingRequest")==null&&!input.IsReadOnly;}
  Check(repeated,"30 simulated reply cycles alternate success/rejection without a six-message UI limit");
  File.WriteAllText(Path.Combine(root,"oversized.bin"),new string('x',100));bool bounded=false;try{Journal.ReadShared(Path.Combine(root,"oversized.bin"),10);}catch(InvalidDataException){bounded=true;}
  Check(bounded,"shared state readers enforce a size limit before reading an unbounded payload");
  string journal=Path.Combine(bridge,"room_123.jsonl");
  var entry=new ChatEntry{kind="chat",id="cache-one",room="123",name="fixture",text="first cached bubble",utc=DateTime.UtcNow.ToString("o")};
  File.WriteAllText(journal,Journal.Json.Serialize(entry)+Environment.NewLine);state.utc=DateTime.UtcNow.ToString("o");File.WriteAllText(statePath,Journal.Json.Serialize(state));w.Poll();
  var messages=(StackPanel)Get(w,"messages");var originalBubble=messages.Children[1];
  entry.id="cache-two";entry.text="second cached bubble";File.AppendAllText(journal,Journal.Json.Serialize(entry)+Environment.NewLine);w.Poll();
  Check(messages.Children.Count==3&&Object.ReferenceEquals(originalBubble,messages.Children[1]),"new chat appends without rebuilding existing WPF bubbles");
  var search=(TextBox)Get(w,"search");search.Text="second cached";
  Check(messages.Children.Count==2,"search changes rebuild the visible view without duplicating cached rows");search.Clear();
  w.Left=-10000;w.Top=-10000;w.WindowStartupLocation=WindowStartupLocation.Manual;w.ShowActivated=false;w.Show();
  var slider=(Slider)((Grid)w.Content).FindName("OpacityControl");slider.Value=0.5;
  IntPtr hwnd=new WindowInteropHelper(w).Handle;
  Check(HwndSource.FromHwnd(hwnd).UsesPerPixelOpacity&&w.AllowsTransparency&&Math.Abs(w.Opacity-0.5)<0.01,"opacity uses a native per-pixel transparent window instead of dimming an opaque HWND");
  w.UpdateLayout();
  var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
  bitmap.Render(w);byte[] pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(5,5,1,1),pixel,4,0);
  Check(pixel[3]>=120&&pixel[3]<=135,"the per-pixel window renders actual translucent alpha rather than a dark opaque pixel");
  var frameRoot=(Grid)w.Content;
  ((Button)frameRoot.FindName("WindowMinimize")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(w.WindowState==WindowState.Minimized,"custom frame minimizes the actual window");w.WindowState=WindowState.Normal;
  ((Button)frameRoot.FindName("WindowMaximize")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Check(w.WindowState==WindowState.Maximized,"custom frame maximizes the actual window");w.WindowState=WindowState.Normal;
  Check((GetWindowStyle(hwnd,-20)&0x80000)!=0&&(GetWindowStyle(hwnd,-20)&0x20)==0&&((Grid)w.Content).FindName("WindowClose")!=null,"transparent window retains close/resize controls and normal click handling");
  slider.Value=1;Check(w.Opacity==1&&HwndSource.FromHwnd(hwnd).UsesPerPixelOpacity,"opacity restores the transparent window to fully opaque content");
  Type snapshotType=typeof(ChatWindow).Assembly.GetType("BongoChat.PollSnapshot");object delayed=Activator.CreateInstance(snapshotType);
  snapshotType.GetField("StateExists").SetValue(delayed,true);snapshotType.GetField("State").SetValue(delayed,state);
  Set(w,"setupBusy",true);Set(w,"canSend",false);
  typeof(ChatWindow).GetMethod("ApplyPollSnapshot",flags).Invoke(w,new object[]{delayed});
  Check(!(bool)Get(w,"canSend"),"a late poll cannot re-enable chat while setup is restarting the game");Set(w,"setupBusy",false);
  w.EnableBackgroundCommunication();
  state.utc=DateTime.UtcNow.ToString("o");File.WriteAllText(statePath,Journal.Json.Serialize(state));w.Poll();Pump(w,()=>!(bool)Get(w,"pollReading"));
  var asyncRequest=Send(w,"비동기 전송");Check(asyncRequest!=null&&!input.IsReadOnly,"background send reserves one request without toggling editor read-only state");
  string queued=Path.Combine(bridge,"commands",asyncRequest.id+".request.json");Pump(w,()=>File.Exists(queued));Ack(bridge,asyncRequest,true,null);w.Poll();Pump(w,()=>Get(w,"pendingRequest")==null);
  Check(Get(w,"pendingRequest")==null,"production background communication reads and completes an actual file reply");
  state.utc=DateTime.UtcNow.ToString("o");File.WriteAllText(statePath,Journal.Json.Serialize(state));
  string sentinel="PRIVATE_CHAT_SENTINEL_4921";input.Text=sentinel;
  typeof(ChatWindow).GetMethod("UiPhase",flags).Invoke(w,new object[]{"test-stall"});
  Thread.Sleep(9500);
  string health=Path.Combine(root,"ui-health.log");
  Check(File.Exists(health)&&File.ReadAllText(health).Contains("test-stall")&&!File.ReadAllText(health).Contains(sentinel),"external heartbeat records a blocked dispatcher without storing chat text");
  ((Button)frameRoot.FindName("WindowClose")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!w.IsVisible,"custom frame closes the actual window");
  Console.WriteLine("All "+checks+" reliability checks passed.");return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}