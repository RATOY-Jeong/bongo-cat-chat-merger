using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using BongoChat;
public static class MeowFilterTests
{
    static int checks;
    static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Get(ChatWindow w,string key){return typeof(ChatWindow).GetField(key,flags).GetValue(w);}
    static List<ChatEntry> Visible(ChatWindow w){return (List<ChatEntry>)Get(w,"renderedVisible");}
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
    static ChatEntry Entry(string id,string text,string kind="chat"){return new ChatEntry{kind=kind,id=id,room="123",name="fixture",sender="456",mine=id=="en",text=text,emoteId=kind=="emote"?42:0,utc=DateTime.UtcNow.ToString("o")};}
    [STAThread] public static int Main(string[] args){try{
        string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);AppStorage.Root=root;
        new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        // Independent expected localized strings from the installed game's locale tables.
        string[] exact={"야옹 야옹","Meow Meow","にゃんにゃ～ん","喵~喵~","喵嗚～","Mňau Mňau","Mjav Mjau","Miauw Miauw","Miaou Miaou","Miu Mau","Miau Miau","Miau miau","Νιάου Νιάου","מיאו מיאו","Miao Miao","Mjau Mjau","Мяу Мяу","Miyav Miyav","Няв Няв"};
        foreach(string text in exact)Check(MeowMessages.IsMeow(text),"recognized exact localized phrase: "+text);
        string[] ordinary={null,"","야옹","야옹야옹","야옹 야옹!","야옹 야옹 하고 우네","우리 고양이가 야옹 야옹 하네"," Meow Meow","Meow Meow ","Meow Meow\n","meow meow","Meow  Meow","Meow Meow Meow","She said Meow Meow","にゃんにゃ～んです","喵~喵~!","Miau Miau?","Мяу Мяу текст"};
        Check(ordinary.All(t=>!MeowMessages.IsMeow(t)),"mentions, altered punctuation/spacing/case and longer messages are never substring-filtered");
        string game=Path.Combine(root,"game"),bridge=Path.Combine(game,"BepInEx","BongoChat");Directory.CreateDirectory(bridge);
        var state=new BridgeState{protocol=1,ready=true,canSend=true,session="fixture-session",room="123",utc=DateTime.UtcNow.ToString("o")};
        File.WriteAllText(Path.Combine(bridge,"state.json"),Journal.Json.Serialize(state));
        var records=new List<ChatEntry>{Entry("ko","야옹 야옹"),Entry("en","Meow Meow"),Entry("ja","にゃんにゃ～ん"),Entry("mention","우리 고양이가 야옹 야옹 하네"),Entry("punctuation","야옹 야옹!"),Entry("emote","야옹 야옹","emote"),Entry("multiline","Meow Meow\nnext sentence")};
        string journal=Path.Combine(bridge,"room_123.jsonl");File.WriteAllText(journal,String.Join(Environment.NewLine,records.Select(e=>Journal.Json.Serialize(e)))+Environment.NewLine);string original=File.ReadAllText(journal);
        var w=new ChatWindow(false,game,true,"ko");var grid=(Grid)w.Content;var toggle=(CheckBox)grid.FindName("HideMeows");var emotes=(CheckBox)grid.FindName("ShowEmotes");var search=(TextBox)Get(w,"search");
        Check(toggle.IsChecked==false&&Visible(w).Count==7,"new setting is opt-in and preserves all chat by default");
        var controls=(StackPanel)emotes.Parent;Check(Object.ReferenceEquals(controls.Children[controls.Children.IndexOf(emotes)+1],toggle),"meow toggle is directly below Show emotes");
        toggle.IsChecked=true;
        Check(Visible(w).Count==4&&Visible(w).All(e=>e.kind=="emote"||!exact.Contains(e.text)),"toggle hides exact own/other localized chat while retaining ordinary text and emotes");
        Check(File.ReadAllText(journal)==original,"filtering never rewrites or deletes original journal records");
        search.Text="야옹";Check(Visible(w).Count==3&&Visible(w).All(e=>e.id!="ko"),"hidden meows stay hidden in search, but mentions and emotes remain searchable");
        emotes.IsChecked=false;Check(Visible(w).Count==2,"emote visibility remains independent of the meow filter");search.Clear();
        File.AppendAllText(journal,Journal.Json.Serialize(Entry("ru","Мяу Мяу"))+Environment.NewLine+Journal.Json.Serialize(Entry("new-normal","Meow Meow is a cat sound"))+Environment.NewLine);
        w.Poll();Check(Visible(w).Count==4&&Visible(w).Any(e=>e.id=="new-normal")&&Visible(w).All(e=>e.id!="ru"),"newly received foreign meows are hidden without hiding new ordinary mentions");
        var again=new ChatWindow(false,game,true,"ko");Check(((CheckBox)((Grid)again.Content).FindName("HideMeows")).IsChecked==true&&Visible(again).Count==4,"enabled preference survives a new app window");again.Close();
        toggle.IsChecked=false;Check(Visible(w).Count==8,"turning the filter off restores retained meows immediately");
        var off=new ChatWindow(false,game,true,"ko");Check(((CheckBox)((Grid)off.Content).FindName("HideMeows")).IsChecked==false,"disabled preference also survives a new window");off.Close();
        search.Text="Мяу Мяу";toggle.IsChecked=true;Check(Visible(w).Count==0,"search never reveals a filtered exact meow");toggle.IsChecked=false;Check(Visible(w).Count==1,"switching off restores matching search results");search.Clear();
        emotes.IsChecked=true;Check(Journal.ReadRecent(journal,500).Count==9,"all originals remain available to existing journal/export functionality");w.Close();
        foreach(string lang in new[]{"en","ko","ja"}){
            var demo=new ChatWindow(true,null,true,lang);var g=(Grid)demo.Content;var control=(CheckBox)g.FindName("HideMeows");
            Check((string)control.Content==UiText.Get(lang,"야옹 메시지 숨기기")&&((string)control.ToolTip).Contains(lang=="en"?"exact":lang=="ja"?"全文":"전체"),lang+" toggle and exact-match tooltip are localized");
            demo.Snapshot(Path.Combine(root,"meow-"+lang+".png"));demo.Close();
        }
        string source=Path.Combine(root,"old-app"),target=Path.Combine(root,"new-app");Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"hide-meows.txt"),"true");DesktopInstall.MigratePreferences(source,target);
        Check(File.ReadAllText(Path.Combine(target,"hide-meows.txt"))=="true","downloaded-to-managed app migration includes the new setting");
        File.WriteAllText(Path.Combine(source,"hide-meows.txt"),"false");DesktopInstall.MigratePreferences(source,target);Check(File.ReadAllText(Path.Combine(target,"hide-meows.txt"))=="true","migration does not overwrite an existing choice");
        File.WriteAllText(Path.Combine(root,"hide-meows.txt"),"broken");var invalid=new ChatWindow(false,game,true,"ko");Check(((CheckBox)((Grid)invalid.Content).FindName("HideMeows")).IsChecked==false,"invalid stored preference safely leaves filtering off");invalid.Close();
        Console.WriteLine("All "+checks+" meow filter checks passed.");return 0;
    }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}