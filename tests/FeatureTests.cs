using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BongoChat;
using BongoChat.Protocol;

public static class FeatureTests
{
    static int checks;
    static void Check(bool ok, string message) { if(!ok) throw new Exception(message); Console.WriteLine("PASS " + message); checks++; }
    static object Field(ChatWindow w,string name) { return typeof(ChatWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(w); }
    static void Invoke(ChatWindow w,string name) { typeof(ChatWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(w,null); }
    static SendRequest Request(string session,string room) { return new SendRequest {id=Guid.NewGuid().ToString("N"),session=session,room=room,kind="send_chat",createdUtc=DateTime.UtcNow.ToString("o"),text="한글 😺\n줄바꿈"}; }
    [STAThread] public static int Main(string[] args)
    {
        try
        {
            string session=Guid.NewGuid().ToString("N");
            var request=Request(session,"123");
            Check(RequestPolicy.Validate(request,session,"123",DateTime.UtcNow,300)==null,"Unicode and multiline valid");
            Check(RequestPolicy.Validate(request,session,"456",DateTime.UtcNow,300)!=null,"room switch rejected");
            Check(RequestPolicy.Validate(request,"new-session","123",DateTime.UtcNow,300)!=null,"old game session rejected");
            Check(RequestPolicy.Validate(request,session,"123",DateTime.UtcNow.AddSeconds(25),300)!=null,"expired request rejected");
            request.text="x\0y"; Check(RequestPolicy.Validate(request,session,"123",DateTime.UtcNow,300)!=null,"control characters rejected");
            request.text=new string('가',301); Check(RequestPolicy.Validate(request,session,"123",DateTime.UtcNow,300)!=null,"oversize request rejected");
            var app=new Application();
            string fixture=Path.GetFullPath(args[0]);
            string bridge=Path.Combine(fixture,"BepInEx","BongoChat"); Directory.CreateDirectory(Path.Combine(bridge,"commands","results"));
            string statePath=Path.Combine(bridge,"state.json");
            var state=new BridgeState {protocol=1,version="0.2.0",session=session,ready=true,canSend=true,utc=DateTime.UtcNow.ToString("o"),room="123",name="기능 검사",maxMessageLength=150};
            File.WriteAllText(statePath,Journal.Json.Serialize(state));
            File.WriteAllText(Path.Combine(bridge,"room_123.jsonl"),Journal.Json.Serialize(new ChatEntry {kind="emote",id="emote-test",room="123",emoteId=42,text="하트",name="냥",utc=DateTime.UtcNow.ToString("o")})+"\n");
            var w=new ChatWindow(false,fixture,true);
            Check(w.Topmost,"companion starts above the game by default");
            var composer=(TextBox)Field(w,"composer"); var send=(Button)Field(w,"sendButton");
            Check(((SolidColorBrush)((Grid)w.Content).Background).Color==Color.FromRgb(0x13,0x19,0x22),"dark palette applied");
            Check(Journal.ReadRecent(Path.Combine(bridge,"room_123.jsonl"),500)[0].emoteId==42,"emote events retained in journal");
            string emotePreferencePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"show-emotes.txt");
            string previousEmotePreference=File.Exists(emotePreferencePath) ? File.ReadAllText(emotePreferencePath) : null;
            try
            {
                var emoteToggle=(CheckBox)((Grid)w.Content).FindName("ShowEmotes");
                var results=(TextBlock)Field(w,"resultText");
                var search=(TextBox)Field(w,"search");
                Check(emoteToggle.IsChecked==true && results.Text.Contains("1개 표시"),"emotes visible by default");
                emoteToggle.IsChecked=false;
                Check(results.Text.Contains("0개 표시") && Journal.ReadRecent(Path.Combine(bridge,"room_123.jsonl"),500).Count==1,"hiding emotes removes bubbles without deleting the journal");
                search.Text="하트";
                File.AppendAllText(Path.Combine(bridge,"room_123.jsonl"),Journal.Json.Serialize(new ChatEntry {kind="chat",id="chat-test",room="123",text="하트 일반 채팅",name="냥",utc=DateTime.UtcNow.ToString("o")})+"\n");
                w.Poll();
                Check(results.Text.Contains("1개 표시"),"new ordinary chat stays visible while emotes are hidden from search");
                var reopenedEmotes=new ChatWindow(false,fixture,true);
                Check(((CheckBox)((Grid)reopenedEmotes.Content).FindName("ShowEmotes")).IsChecked==false && ((TextBlock)Field(reopenedEmotes,"resultText")).Text.Contains("1개 표시"),"hidden preference survives restart and retains ordinary chat");
                reopenedEmotes.Close();
                emoteToggle.IsChecked=true;
                Check(results.Text.Contains("2개 표시"),"turning emotes back on restores retained matching messages");
                search.Clear();
            }
            finally
            {
                if(previousEmotePreference==null) File.Delete(emotePreferencePath); else File.WriteAllText(emotePreferencePath,previousEmotePreference);
            }
            composer.Text="직접 전송 😺\n두 줄";
            Check(send.IsEnabled && composer.MaxLength==150,"composer uses live game limit");
            Invoke(w,"SendComposer");
            var pending=(SendRequest)Field(w,"pendingRequest");
            string queued=Path.Combine(bridge,"commands",pending.id+".request.json");
            var queuedRequest=Journal.Json.Deserialize<SendRequest>(File.ReadAllText(queued));
            Check(queuedRequest.text==composer.Text && queuedRequest.room=="123" && queuedRequest.session==session,"atomic queue carries exact text and current session");
            Check(composer.IsReadOnly && !send.IsEnabled,"duplicate sends blocked while pending");
            File.WriteAllText(Path.Combine(bridge,"commands","results",pending.id+".json"),Journal.Json.Serialize(new SendResult {id=pending.id,session=session,room="123",accepted=true}));
            w.Poll(); Check(composer.Text=="" && Field(w,"pendingRequest")==null,"accepted acknowledgement clears draft");
            composer.Text="방 바꾸기 전 초안"; Invoke(w,"SendComposer"); pending=(SendRequest)Field(w,"pendingRequest");
            state.room="456"; File.WriteAllText(statePath,Journal.Json.Serialize(state)); w.Poll();
            Check(composer.Text=="방 바꾸기 전 초안" && Field(w,"pendingRequest")==null && !File.Exists(Path.Combine(bridge,"commands",pending.id+".request.json")),"room switch cancels pending request and keeps draft");
            state.canSend=false; File.WriteAllText(statePath,Journal.Json.Serialize(state)); w.Poll();
            Check(!send.IsEnabled,"older bridge cannot send");
            string pinPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"always-on-top.txt");
            string previousPin=File.Exists(pinPath) ? File.ReadAllText(pinPath) : null;
            try
            {
                ((CheckBox)((Grid)w.Content).FindName("Pin")).IsChecked=false;
                var reopened=new ChatWindow(false,fixture,true);
                Check(!reopened.Topmost,"explicit always-on-top choice survives a new window instance");
                reopened.Close();
            }
            finally
            {
                if(previousPin==null) File.Delete(pinPath); else File.WriteAllText(pinPath,previousPin);
            }
            w.Close(); Console.WriteLine("All " + checks + " feature checks passed."); return 0;
        }
        catch(Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
