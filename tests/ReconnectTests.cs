using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BongoChat;
using BongoChat.Protocol;
public static class ReconnectTests
{
    static int checks;
    static object Field(ChatWindow w,string name) { return typeof(ChatWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(w); }
    static void Set(ChatWindow w,string name,object value) { typeof(ChatWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(w,value); }
    static void Call(ChatWindow w,string name) { typeof(ChatWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(w,null); }
    static void Check(bool ok,string description) { if(!ok) throw new Exception(description); checks++; Console.WriteLine("PASS "+description); }
    [STAThread] public static int Main(string[] args)
    {
        try
        {
            new Application();
            string game=Path.GetFullPath(args[0]); Directory.CreateDirectory(game); File.WriteAllText(Path.Combine(game,"BongoCat.exe"),"");
            string bridge=Path.Combine(game,"BepInEx","BongoChat"); Directory.CreateDirectory(bridge);
            string statePath=Path.Combine(bridge,"state.json"), journal=Path.Combine(bridge,"room_123.jsonl");
            var state=new BridgeState {protocol=1,version="0.2.2",session=Guid.NewGuid().ToString("N"),ready=true,canSend=true,room="123",name="재연결 검사",utc=DateTime.UtcNow.ToString("o"),maxMessageLength=50};
            File.WriteAllText(statePath,Journal.Json.Serialize(state));
            File.WriteAllText(journal,Journal.Json.Serialize(new ChatEntry {kind="chat",id="retained",room="123",name="예시",text="남겨둘 기록",utc=DateTime.UtcNow.ToString("o")})+"\n");
            var w=new ChatWindow(false,game,true);
            var button=(Button)Field(w,"reconnectButton"); var composer=(TextBox)Field(w,"composer"); var retry=(DispatcherTimer)Field(w,"reconnectTimer");
            composer.Text="보존할 초안"; Call(w,"Reconnect");
            Check(button.IsEnabled && !retry.IsEnabled && (bool)Field(w,"statusConnected"),"healthy reconnect completes immediately");
            Check(composer.Text=="보존할 초안" && File.ReadAllText(journal).Contains("남겨둘 기록"),"reconnect preserves draft and stored conversation");
            state.stopped=true; state.utc=DateTime.UtcNow.AddMinutes(-1).ToString("o"); File.WriteAllText(statePath,Journal.Json.Serialize(state));
            Call(w,"Reconnect"); var deadline=(DateTime)Field(w,"reconnectDeadline"); Call(w,"Reconnect");
            Check(retry.IsEnabled && !button.IsEnabled && (DateTime)Field(w,"reconnectDeadline")==deadline,"stale bridge waits and repeated clicks do not start duplicate attempts");
            Check(!(bool)Field(w,"canSend") && !(bool)Field(w,"statusConnected"),"stale bridge never reports a successful connection");
            Set(w,"reconnectDeadline",DateTime.UtcNow.AddSeconds(-1)); Call(w,"CheckReconnect");
            Check(!retry.IsEnabled && button.IsEnabled && ((TextBlock)Field(w,"composerHint")).Text.Contains("Steam"),"timeout restores button and shows concrete Steam restart guidance");
            Call(w,"Reconnect"); state.stopped=false; state.utc=DateTime.UtcNow.ToString("o"); state.session=Guid.NewGuid().ToString("N"); File.WriteAllText(statePath,Journal.Json.Serialize(state)); w.Poll();
            Check(!retry.IsEnabled && (bool)Field(w,"canSend") && button.IsEnabled,"a restarted game session reconnects during an attempt");
            composer.Text="전송 중 초안"; Call(w,"SendComposer"); var pending=(SendRequest)Field(w,"pendingRequest");
            string request=Path.Combine(bridge,"commands",pending.id+".request.json"); Check(File.Exists(request),"fixture creates a pending request");
            Call(w,"Reconnect");
            Check(!File.Exists(request) && Field(w,"pendingRequest")==null && composer.Text=="전송 중 초안","reconnect cancels pending request without replaying or erasing draft");
            Check(Directory.GetFiles(Path.Combine(bridge,"commands"),"*.request.json").Length==0,"reconnect sends no chat message");
            state.room=""; state.utc=DateTime.UtcNow.ToString("o"); File.WriteAllText(statePath,Journal.Json.Serialize(state)); Call(w,"Reconnect");
            Check(retry.IsEnabled && !(bool)Field(w,"canSend"),"running game outside a room is not treated as chat-ready");
            state.room="123"; File.WriteAllText(statePath,Journal.Json.Serialize(state)); w.Poll();
            Check(!retry.IsEnabled && (bool)Field(w,"canSend"),"joining a room completes the reconnect attempt");
            w.Close(); var demo=new ChatWindow(true,null,true); Check(!((Button)Field(demo,"reconnectButton")).IsEnabled,"demo cannot start real reconnection"); demo.Close();
            Console.WriteLine("All "+checks+" reconnect checks passed."); return 0;
        }
        catch(Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
