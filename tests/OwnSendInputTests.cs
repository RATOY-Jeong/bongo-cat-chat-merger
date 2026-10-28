using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using System.Threading;using System.Windows;using System.Windows.Controls;using System.Windows.Input;using System.Windows.Threading;using BongoChat;using BongoChat.Protocol;
public static class OwnSendInputTests
{
 static int checks;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static object Get(ChatWindow w,string field){return typeof(ChatWindow).GetField(field,flags).GetValue(w);}
 static void Set(ChatWindow w,string field,object value){typeof(ChatWindow).GetField(field,flags).SetValue(w,value);}
 static void Call(ChatWindow w,string method,params object[] args){typeof(ChatWindow).GetMethod(method,flags).Invoke(w,args);}
 static SendRequest Request(string text){return new SendRequest{id=Guid.NewGuid().ToString("N"),room="123",session="fixture-session",createdUtc=DateTime.UtcNow.ToString("o"),kind="send_chat",text=text};}
 static ChatEntry Echo(SendRequest r,string id,bool mine=true){return new ChatEntry{kind="chat",id=id,session=r.session,room=r.room,mine=mine,text=r.text,utc=DateTime.UtcNow.ToString("o")};}
 static TextBox FindText(DependencyObject node,string value){var text=node as TextBox;if(text!=null&&text.Text==value)return text;foreach(var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()){var found=FindText(child,value);if(found!=null)return found;}return null;}
 static void Pump(ChatWindow w){w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));}
 static void Ack(ChatWindow w,string folder,bool accepted){var r=(SendRequest)Get(w,"pendingRequest");File.WriteAllText(Path.Combine(folder,"commands","results",r.id+".json"),Journal.Json.Serialize(new SendResult{id=r.id,session=r.session,room=r.room,accepted=accepted,error=accepted?null:"게임의 채팅 필터가 전송을 허용하지 않았어요."}));w.Poll();}
 [STAThread]public static int Main(string[] args){try{
  string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);AppStorage.Root=root;string game=Path.Combine(root,"game");Directory.CreateDirectory(game);File.WriteAllText(Path.Combine(game,"BongoCat.exe"),"");
  var store=new OwnSendStore();store.Open(game,CancellationToken.None);var first=Request("같은 문장");
  Check(store.Record(first,true,false)&&store.Missing("123").Single().delivery=="unconfirmed","a processed send without an echo has an explicit unconfirmed local record");
  Check(!store.Record(first,true,false)&&store.Missing("123").Count==1,"duplicate acknowledgements cannot duplicate an own-send record");
  Check(store.Missing("456").Count==0,"local send records stay in their own room");
  var foreign=Echo(first,"foreign",false);Check(!store.Observe(new List<ChatEntry>{foreign},false),"another player's identical text cannot confirm a local send");
  var session=Echo(first,"wrong-session");session.session="other";Check(!store.Observe(new List<ChatEntry>{session},false),"a different game session cannot confirm a local send");
  var old=Echo(first,"old");old.utc=DateTime.UtcNow.AddMinutes(-10).ToString("o");Check(!store.Observe(new List<ChatEntry>{old},false),"historical identical text cannot confirm a new send");
  var otherRoom=Echo(first,"room");otherRoom.room="456";Check(!store.Observe(new List<ChatEntry>{otherRoom},false),"a different room cannot confirm a local send");
  var second=Request(first.text);store.Record(second,true,false);var echo=Echo(first,"one-echo");
  Check(store.Observe(new List<ChatEntry>{echo},false)&&store.Missing("123").Count==1,"one echo matches only one of two identical sends");
  Check(!store.Observe(new List<ChatEntry>{echo},false)&&store.Missing("123").Count==1,"rereading an echo never consumes another identical send");
  var reopened=new OwnSendStore();reopened.Open(game,CancellationToken.None);Check(reopened.Missing("123").Count==1,"unconfirmed receipts and matched echo IDs survive app restart");
  Check(reopened.Observe(new List<ChatEntry>{Echo(second,"second-echo")},false)&&reopened.Missing("123").Count==0,"a delayed second echo replaces the remaining local receipt");
  var rejected=Request("거절 문장");reopened.Record(rejected,false,false);Check(reopened.Missing("123").Single().delivery=="rejected","rejected sends remain visibly rejected rather than pretending to be received");
  Check(!reopened.Record(Request("야옹 야옹"),true,false),"exact meows continue to use existing grouping and hiding rules");
  string otherGame=Path.Combine(root,"other-game");Directory.CreateDirectory(otherGame);var isolated=new OwnSendStore();isolated.Open(otherGame,CancellationToken.None);Check(isolated.Missing("123").Count==0,"separate game installations cannot mix send receipts");
  for(int i=0;i<510;i++)isolated.Record(Request("bounded "+i),true,false);Check(isolated.Missing("123").Count==500,"local receipt storage has a bounded 500-record limit");
  TabletCompatibility.Configure();new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
  string uiGame=Path.Combine(root,"ui-game"),folder=Path.Combine(uiGame,"BepInEx","BongoChat");Directory.CreateDirectory(Path.Combine(folder,"commands","results"));File.WriteAllText(Path.Combine(uiGame,"BongoCat.exe"),"");
  var state=new BridgeState{protocol=1,ready=true,canSend=true,session="fixture-session",room="123",maxMessageLength=50,utc=DateTime.UtcNow.ToString("o")};string stateFile=Path.Combine(folder,"state.json"),journal=Path.Combine(folder,"room_123.jsonl");File.WriteAllText(stateFile,Journal.Json.Serialize(state));
  var w=new ChatWindow(false,uiGame,true,"ko");var input=(TextBox)Get(w,"composer");var send=(Button)Get(w,"sendButton");
  Check(!send.Focusable&&InputMethod.GetPreferredImeState(input)==InputMethodState.DoNotCare&&InputMethod.GetPreferredImeConversionMode(input)==ImeConversionModeValues.DoNotCare,"send button preserves editor focus and composer never forces Korean/English mode");
  input.Text="머지어에서 보낸 첫 문장";Call(w,"SendComposer");var outgoing=(SendRequest)Get(w,"pendingRequest");Ack(w,folder,true);
  Check(FindText((Grid)w.Content,outgoing.text)!=null&&((List<ChatEntry>)Get(w,"entries")).Single().kind=="own_send","a game-processed own send displays even when no journal echo arrives");
  Check(FindText((Grid)w.Content,outgoing.text).IsReadOnly&&InputMethod.GetIsInputMethodSuspended(FindText((Grid)w.Content,outgoing.text)),"selectable messages do not create an active IME editing context");
  w.Snapshot(Path.Combine(root,"own-send-unconfirmed-ko.png"));
  File.WriteAllText(journal,Journal.Json.Serialize(Echo(outgoing,"delayed-ui-echo"))+"\n");w.Poll();var rows=(List<ChatEntry>)Get(w,"entries");
  Check(rows.Count==1&&rows[0].id=="delayed-ui-echo"&&rows[0].kind=="chat","late received data replaces the local own row without duplication");
  input.Text="전송 거절된 문장";Call(w,"SendComposer");Ack(w,folder,false);rows=(List<ChatEntry>)Get(w,"entries");
  Check(rows.Any(e=>e.delivery=="rejected")&&input.Text=="전송 거절된 문장","a rejected local row and recoverable draft remain distinct from received messages");
  input.Text="미리 보낸 문장";Call(w,"SendComposer");input.Clear();Set(w,"imeComposing",true);Ack(w,folder,false);
  Check(input.Text==""&&((Queue<string>)Get(w,"recoverableDrafts")).Count>0,"a late rejection never restores old text over active IME composition");
  input.Text="조합 중";Call(w,"SendComposer");Check(Get(w,"pendingRequest")==null,"no send can clear an editor while IME composition is active");
  int previousLimit=input.MaxLength;state.maxMessageLength=80;state.utc=DateTime.UtcNow.ToString("o");File.WriteAllText(stateFile,Journal.Json.Serialize(state));
  int visibleBefore=((StackPanel)Get(w,"messages")).Children.Count;File.AppendAllText(journal,Journal.Json.Serialize(new ChatEntry{kind="chat",id="composition-receive",room="123",session=state.session,text="조합 중 받은 글",utc=DateTime.UtcNow.ToString("o")})+"\n");w.Poll();
  Check(input.MaxLength==previousLimit&&((StackPanel)Get(w,"messages")).Children.Count==visibleBefore&&FindText((Grid)w.Content,"조합 중 받은 글")==null,"polling defers editor limits and message reconstruction during composition");
  Call(w,"FinishCompositionLater");Check((bool)Get(w,"imeComposing"),"composition end waits until TextBox has inserted committed text");Pump(w);
  Check(!(bool)Get(w,"imeComposing")&&input.MaxLength==80&&FindText((Grid)w.Content,"조합 중 받은 글")!=null&&input.Text=="조합 중","committing flushes deferred updates and preserves the new draft");
  Set(w,"imeComposing",true);Call(w,"FinishCompositionLater");Set(w,"compositionGeneration",((int)Get(w,"compositionGeneration"))+1);Pump(w);Check((bool)Get(w,"imeComposing"),"an older completion callback cannot end a newer composition");Set(w,"imeComposing",false);
  var fresh=new ChatWindow(false,uiGame,true,"ko");Check(((List<ChatEntry>)Get(fresh,"entries")).Any(e=>e.delivery=="rejected")&&!((List<ChatEntry>)Get(fresh,"entries")).Any(e=>e.id=="own:"+outgoing.id),"app restart preserves unresolved local rows while retaining confirmed history once");fresh.Close();w.Close();
  Console.WriteLine("All "+checks+" own-send/input checks passed.");return 0;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}