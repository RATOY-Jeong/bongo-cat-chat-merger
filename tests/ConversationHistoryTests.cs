using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BongoChat;
class ConversationHistoryTests
{
    static int checks;static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Get(ChatWindow w,string f){return typeof(ChatWindow).GetField(f,flags).GetValue(w);}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
    static ChatEntry E(string id,string text,string sender="1",string kind="chat",string room="123"){return new ChatEntry{kind=kind,id=id,room=room,text=text,sender=sender,name="cat "+sender,emoteId=kind=="emote"?7:0,utc=DateTime.UtcNow.ToString("o")};}
    static string J(ChatEntry e){return Journal.Json.Serialize(e)+"\n";}
    static void Append(string p,ChatEntry e){File.AppendAllText(p,J(e),new UTF8Encoding(false));}
    static int Normal(List<ChatEntry> e){return e.Count(x=>x.kind!="meow_group");}
    static void Pump(ChatWindow w,Func<bool> done){var until=DateTime.UtcNow.AddSeconds(8);while(!done()&&DateTime.UtcNow<until){w.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(delegate{}));Thread.Sleep(5);}if(!done())throw new Exception("Background history deadline");}
    [STAThread] static int Main(string[] args){try{
        string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);AppStorage.Root=root;new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        string file=Path.Combine(root,"flood.jsonl");
        using(var writer=new StreamWriter(file,false,new UTF8Encoding(false))){for(int i=0;i<500;i++)writer.Write(J(E("n"+i,"ordinary "+i)));for(int i=0;i<15000;i++)writer.Write(J(E("m"+i,new[]{"야옹 야옹","Meow Meow","にゃんにゃ～ん","Мяу Мяу"}[i%4],"speaker"+(i%4))));}
        long size=new FileInfo(file).Length;Check(size>2*1024*1024,"flood fixture exceeds the old two-megabyte tail window");
        string original=BridgeSetup.FileHash(file);var reader=new ConversationHistory();var rows=reader.Read(file,"123",500,CancellationToken.None);
        Check(Normal(rows)==500&&rows.First().id=="n0"&&rows.Last().meowCount==15000,"15000 meows do not evict any of the 500 preceding conversation events");
        Check(rows.Count==501&&rows.Last().meowAuthors.Count==4&&rows.Last().meowAuthors.Sum(a=>a.count)==15000,"consecutive multilingual meows collapse into one counted four-person group");
        Check(reader.LastBytesRead>2*1024*1024&&BridgeSetup.FileHash(file)==original,"history reaches past the old tail limit without changing the journal");
        ChatEntry oldGroup=rows.Last();Append(file,E("ma","야옹 야옹","speaker0"));Append(file,E("mb","Meow Meow","speaker1"));rows=reader.Read(file,"123",500,CancellationToken.None);
        Check(rows.Last().meowCount==15002&&oldGroup.meowCount==15000,"incremental updates create immutable snapshots and preserve exact counts");
        Check(reader.LastBytesRead<4096,"new meows read only appended bytes instead of rescanning the large journal");
        Append(file,E("n500","new conversation"));rows=reader.Read(file,"123",500,CancellationToken.None);
        Check(Normal(rows)==500&&rows.First().id=="n1"&&rows.Last().id=="n500"&&rows.Single(e=>e.kind=="meow_group").meowCount==15002,"only a new ordinary event advances the 500-event window");
        Append(file,E("after","Meow Meow"));rows=reader.Read(file,"123",500,CancellationToken.None);
        Check(rows.Count(e=>e.kind=="meow_group")==2&&rows.Last().meowCount==1,"ordinary conversation separates meow groups and preserves chronology");
        byte[] split=Encoding.UTF8.GetBytes(J(E("utf8","새 글 😺 여러 언어")));int cut=Array.IndexOf(split,(byte)0xF0)+2;
        using(var output=new FileStream(file,FileMode.Append)){output.Write(split,0,cut);}rows=reader.Read(file,"123",500,CancellationToken.None);
        Check(rows.All(e=>e.id!="utf8"),"a partially written UTF-8 record is not displayed prematurely");
        using(var output=new FileStream(file,FileMode.Append)){output.Write(split,cut,split.Length-cut);}rows=reader.Read(file,"123",500,CancellationToken.None);
        Check(rows.Count(e=>e.id=="utf8")==1&&rows.Last().text=="새 글 😺 여러 언어","a split Unicode record completes once on the next append");
        string mixed=Path.Combine(root,"mixed.jsonl");File.WriteAllText(mixed,J(E("a","야옹 야옹"))+J(E("a","야옹 야옹"))+"{bad}\n"+J(E("wrong","ordinary",room:"456"))+J(E("b","Meow Meow"))+J(E("emote","야옹 야옹",kind:"emote"))+J(E("sentence","야옹 야옹 하고 우네"))+J(E("c","にゃんにゃ～ん")));
        var mixedReader=new ConversationHistory();var small=mixedReader.Read(mixed,"123",500,CancellationToken.None);
        Check(small.Count==4&&small.First().meowCount==2&&Normal(small)==2,"duplicates, malformed/foreign-room rows, emotes and ordinary mentions are classified correctly");
        string huge=Path.Combine(root,"huge.jsonl");File.WriteAllText(huge,J(E("before","keep me"))+new string('x',200000));var hugeReader=new ConversationHistory();var hugeRows=hugeReader.Read(huge,"123",500,CancellationToken.None);
        Check(hugeRows.Count==1&&hugeRows[0].id=="before","an oversized unfinished tail cannot hide older valid conversation");File.AppendAllText(huge,"\n"+J(E("after-huge","keep this too")));hugeRows=hugeReader.Read(huge,"123",500,CancellationToken.None);
        Check(hugeRows.Count==2,"oversized records are skipped with bounded memory and later valid records recover");
        File.WriteAllText(mixed,J(E("replacement","changed A")));small=mixedReader.Read(mixed,"123",500,CancellationToken.None);Check(small.Count==1&&small[0].id=="replacement","truncation/rotation drops stale cached entries");
        var previousStamp=File.GetLastWriteTimeUtc(mixed);File.WriteAllText(mixed,J(E("replacement","changed B")));File.SetLastWriteTimeUtc(mixed,previousStamp.AddSeconds(2));small=mixedReader.Read(mixed,"123",500,CancellationToken.None);Check(small[0].text=="changed B","same-length replacement is detected by journal metadata");
        var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{new ConversationHistory().Read(file,"123",500,cancel.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"large history reads honor cancellation");using(var exclusive=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}Check(true,"cancelled readers release the journal handle");
        string game=Path.Combine(root,"game"),bridge=Path.Combine(game,"BepInEx","BongoChat");Directory.CreateDirectory(bridge);string statePath=Path.Combine(bridge,"state.json"),journal=Path.Combine(bridge,"room_123.jsonl");
        Action state=()=>File.WriteAllText(statePath,Journal.Json.Serialize(new BridgeState{protocol=1,ready=true,canSend=true,session="fixture",room="123",utc=DateTime.UtcNow.ToString("o")}));
        File.WriteAllText(journal,J(E("chat","keep selection"))+J(E("meow-a","야옹 야옹","a"))+J(E("meow-b","Meow Meow","b")));state();
        var w=new ChatWindow(false,game,true,"ko");var stack=(StackPanel)Get(w,"messages");UIElement firstBubble=stack.Children[1];
        var firstText=Find(firstBubble,"keep selection");firstText.Select(0,4);Append(journal,E("meow-c","にゃんにゃ～ん","c"));state();w.Poll();
        Check(Object.ReferenceEquals(firstBubble,stack.Children[1])&&firstText.SelectedText=="keep","meow count updates replace only the last group and preserve selected ordinary text");
        var visible=(List<ChatEntry>)Get(w,"renderedVisible");Check(visible.Last().meowCount==3&&visible.Last().meowAuthors.Count==3,"UI receives a correct counted multi-person group");
        var toggle=(CheckBox)((Grid)w.Content).FindName("HideMeows");toggle.IsChecked=true;Check(((List<ChatEntry>)Get(w,"renderedVisible")).Count==1,"existing Hide meows toggle hides the entire group");toggle.IsChecked=false;Check(((List<ChatEntry>)Get(w,"renderedVisible")).Last().meowCount==3,"turning hiding off restores the counted group without rereading or replaying");
        var search=(TextBox)Get(w,"search");search.Text="cat b";Check(((List<ChatEntry>)Get(w,"renderedVisible")).Count==1,"group contributors remain searchable");search.Clear();
        w.Snapshot(Path.Combine(root,"grouped-ko.png"));w.Close();
        File.WriteAllText(Path.Combine(root,"hide-meows.txt"),"false");state();var background=new ChatWindow(false,game,true,"en",true);
        Check((bool)Get(background,"backgroundIo"),"production-style constructor starts history communication in the background");Pump(background,()=>!(bool)Get(background,"pollReading"));Check(((List<ChatEntry>)Get(background,"entries")).Count==2,"initial background load delivers grouped history");background.Snapshot(Path.Combine(root,"grouped-en.png"));background.Close();
        Console.WriteLine("All "+checks+" grouped history checks passed.");return 0;
    }catch(Exception ex){Console.WriteLine(ex);return 1;}}
    static TextBox Find(DependencyObject node,string text){var box=node as TextBox;if(box!=null&&box.Text==text)return box;foreach(var child in LogicalTreeHelper.GetChildren(node)){var dep=child as DependencyObject;if(dep!=null){var found=Find(dep,text);if(found!=null)return found;}}return null;}
}