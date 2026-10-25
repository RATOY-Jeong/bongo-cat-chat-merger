using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using BongoChat;
public static class SetupTests
{
    static int checks; static string root;
    static JavaScriptSerializer json = new JavaScriptSerializer();
    static void Check(bool value, string description) { if(!value) throw new Exception(description); checks++; Console.WriteLine("PASS "+description); }
    static string Game(string name)
    {
        string game=Path.Combine(root,name); Directory.CreateDirectory(Path.Combine(game,"BongoCat_Data","Managed"));
        File.WriteAllText(Path.Combine(game,"BongoCat.exe"),"original game");
        File.WriteAllText(Path.Combine(game,"BongoCat_Data","Managed","Assembly-CSharp.dll"),"original assembly");
        return game;
    }
    static void Reject(Action action, string description)
    {
        bool rejected=false; try{action();}catch(IOException){rejected=true;} Check(rejected,description);
    }
    static InstallManifest Manifest(string game) {return json.Deserialize<InstallManifest>(File.ReadAllText(Path.Combine(game,"BongoChat-install.json")));}
    static void Save(string game,InstallManifest manifest){File.WriteAllText(Path.Combine(game,"BongoChat-install.json"),json.Serialize(manifest));}
    [STAThread] public static int Main(string[] args)
    {
        try
        {
            root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
            byte[] binary=File.ReadAllBytes(typeof(ChatWindow).Assembly.Location);int pe=BitConverter.ToInt32(binary,0x3c);
            Check(BitConverter.ToUInt16(binary,pe+4)==0x8664,"standalone EXE is an explicit Windows x64 build");
            Check(Encoding.UTF8.GetString(binary).Contains("8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a"),"Windows 10/11 compatibility manifest is embedded");
            var framework=(System.Runtime.Versioning.TargetFrameworkAttribute)Attribute.GetCustomAttribute(typeof(ChatWindow).Assembly,typeof(System.Runtime.Versioning.TargetFrameworkAttribute));
            Check(framework!=null && framework.FrameworkName==".NETFramework,Version=v4.5","managed target uses the Windows 10 inbox-compatible Framework baseline");
            Check(!ChatWindow.CanStyleTitleBar(new Version(10,0,19045)) && ChatWindow.CanStyleTitleBar(new Version(10,0,22000)),"Windows 11 title-bar styling is skipped on Windows 10");
            using(var notices=typeof(ChatWindow).Assembly.GetManifestResourceStream("BongoChat.ThirdPartyNotices"))using(var reader=new StreamReader(notices))
                Check(reader.ReadToEnd().Contains("Doorstop-LICENSE.txt"),"standalone EXE also carries third-party license notices");
            string game=Game("새 설치 한글");
            Check(BridgeSetup.Inspect(game).NeedsInstall,"fresh game is detected as needing setup");
            string exeHash=BridgeSetup.FileHash(Path.Combine(game,"BongoCat.exe")), assemblyHash=BridgeSetup.FileHash(Path.Combine(game,"BongoCat_Data","Managed","Assembly-CSharp.dll"));
            int progress=0;BridgeSetup.Install(game,p=>progress=p);
            Check(progress==100 && !BridgeSetup.Inspect(game).NeedsInstall,"fresh setup installs and verifies every embedded file");
            string plugin=Path.Combine(game,"BepInEx","plugins","BongoChat","BongoChatBridge.dll");
            Check(BridgeSetup.FileHash(plugin)==BridgeSetup.FileHash(args[1]),"installed bridge exactly matches the verified 0.2.2 binary");
            Check(BridgeSetup.FileHash(Path.Combine(game,"BongoCat.exe"))==exeHash && BridgeSetup.FileHash(Path.Combine(game,"BongoCat_Data","Managed","Assembly-CSharp.dll"))==assemblyHash,"game EXE and original managed assembly remain unchanged");
            var manifest=Manifest(game);
            Check(!manifest.pending && manifest.files.Length>10 && manifest.files.All(f=>f.previousSha256==null),"successful setup leaves a complete compatible ownership manifest");
            string records=Path.Combine(game,"BepInEx","BongoChat","room_123.jsonl");Directory.CreateDirectory(Path.GetDirectoryName(records));File.WriteAllText(records,"private fixture conversation");
            DateTime originalWrite=File.GetLastWriteTimeUtc(plugin);BridgeSetup.Install(game,null);
            Check(File.GetLastWriteTimeUtc(plugin)==originalWrite && File.ReadAllText(records)=="private fixture conversation","repeat setup is idempotent and preserves chat records");
            File.Delete(plugin);
            Check(BridgeSetup.Inspect(game).NeedsInstall,"a missing plugin is detected");
            BridgeSetup.Install(game,null);Check(BridgeSetup.FileHash(plugin)==BridgeSetup.FileHash(args[1]),"missing plugin is repaired automatically");
            string config=Path.Combine(game,"BepInEx","config","BepInEx.cfg");
            File.AppendAllText(config,"\r\n# user's custom option\r\n");string userConfig=File.ReadAllText(config);
            File.Delete(plugin);BridgeSetup.Install(game,null);
            Check(File.ReadAllText(config)==userConfig,"compatible custom logging configuration is preserved during repair");
            string loader=Path.Combine(game,"winhttp.dll");string loaderHash=BridgeSetup.FileHash(loader);
            File.WriteAllText(loader,"externally modified loader");string modified=File.ReadAllText(loader);
            Check(BridgeSetup.Inspect(game).Problem!=null,"an externally modified owned loader is detected");
            Reject(()=>BridgeSetup.Install(game,null),"setup refuses to overwrite a changed loader");
            Check(File.ReadAllText(loader)==modified && File.ReadAllText(records)=="private fixture conversation","refused repair preserves modified files and journals");

            string interrupted=Game("interrupted");
            BridgeSetup.Install(interrupted,null);string interruptedPlugin=Path.Combine(interrupted,"BepInEx","plugins","BongoChat","BongoChatBridge.dll");
            var pending=Manifest(interrupted);var item=pending.files.First(f=>f.relative.EndsWith("BongoChatBridge.dll"));
            File.WriteAllText(interruptedPlugin,"previous owned version");item.previousSha256=BridgeSetup.FileHash(interruptedPlugin);pending.pending=true;Save(interrupted,pending);
            BridgeSetup.Install(interrupted,null);
            Check(!BridgeSetup.Inspect(interrupted).NeedsInstall && BridgeSetup.FileHash(interruptedPlugin)==BridgeSetup.FileHash(args[1]),"an interrupted owned upgrade is recoverable");
            string backups=Path.Combine(interrupted,"BepInEx","BongoChat","installer-backups");
            Check(Directory.GetFiles(backups,"BongoChatBridge.dll",SearchOption.AllDirectories).Any(f=>File.ReadAllText(f)=="previous owned version"),"replaced owned files receive a backup");
            string badGame=Game("foreign-mod");File.WriteAllText(Path.Combine(badGame,"winhttp.dll"),"another mod");
            Check(BridgeSetup.Inspect(badGame).Problem!=null,"a different mod loader is reported before installation");
            Reject(()=>BridgeSetup.Install(badGame,null),"automatic setup does not replace another mod loader");
            Check(!File.Exists(Path.Combine(badGame,"BongoChat-install.json")) && File.ReadAllText(Path.Combine(badGame,"winhttp.dll"))=="another mod","foreign mod files and ownership are preserved");
            var originalManifest=Manifest(interrupted);
            string manifestText=File.ReadAllText(Path.Combine(interrupted,"BongoChat-install.json"));
            originalManifest.gamePath=root;Save(interrupted,originalManifest);
            Reject(()=>BridgeSetup.Inspect(interrupted),"manifest ownership from another game folder is rejected");
            File.WriteAllText(Path.Combine(interrupted,"BongoChat-install.json"),manifestText);
            var unsafeManifest=Manifest(interrupted);var unsafeRecords=unsafeManifest.files.ToList();unsafeRecords.Add(new InstallRecord{relative=@"..\outside.txt",sha256=new string('A',64)});unsafeManifest.files=unsafeRecords.ToArray();Save(interrupted,unsafeManifest);
            Reject(()=>BridgeSetup.Install(interrupted,null),"path traversal in an ownership manifest is rejected");
            Check(!File.Exists(Path.Combine(root,"outside.txt")),"no file was written outside the game");
            Reject(()=>BridgeSetup.Install(Path.Combine(root,"missing"),null),"a non-game folder cannot be an installation target");

            string steam=Path.Combine(root,"Steam"),library=Path.Combine(root,"다른 라이브러리");Directory.CreateDirectory(Path.Combine(steam,"steamapps"));Directory.CreateDirectory(Path.Combine(library,"steamapps","common"));
            string custom=Path.Combine(library,"steamapps","common","Bongo Cat 사용자 지정");Directory.CreateDirectory(custom);Directory.CreateDirectory(Path.Combine(custom,"BongoCat_Data","Managed"));File.WriteAllText(Path.Combine(custom,"BongoCat.exe"),"");File.WriteAllText(Path.Combine(custom,"BongoCat_Data","Managed","Assembly-CSharp.dll"),"");
            File.WriteAllText(Path.Combine(steam,"steamapps","libraryfolders.vdf"),"\"libraryfolders\" { \"0\" { \"path\" \""+library.Replace(@"\",@"\\")+"\" } }");
            File.WriteAllText(Path.Combine(library,"steamapps","appmanifest_3419430.acf"),"\"AppState\" { \"appid\" \"3419430\" \"installdir\" \"Bongo Cat 사용자 지정\" }");
            Check(GameLocator.FindInLibraries(new[]{steam},null)==custom,"Steam manifests locate a custom Unicode install directory on another library");
            Check(GameLocator.FindInLibraries(new[]{steam},interrupted)==interrupted,"a valid user-selected installation takes priority");
            File.WriteAllText(Path.Combine(library,"steamapps","appmanifest_3419430.acf"),"\"installdir\" \"..\\..\\escape\"");
            Check(GameLocator.FindInLibraries(new[]{steam},null)=="","Steam manifest traversal is not treated as a game folder");

            string downloaded=Path.Combine(root,"download"),installed=Path.Combine(root,"managed");Directory.CreateDirectory(downloaded);Directory.CreateDirectory(installed);
            string downloadExe=Path.Combine(downloaded,"BongoCatChatMerger-0.4.0.exe");File.Copy(typeof(ChatWindow).Assembly.Location,downloadExe);
            File.WriteAllText(Path.Combine(downloaded,"language.txt"),"ko");File.WriteAllText(Path.Combine(downloaded,"appearance.txt"),"light");File.WriteAllText(Path.Combine(downloaded,"error.log"),"do not migrate");
            File.WriteAllText(Path.Combine(installed,"language.txt"),"ja");
            string staged=DesktopInstall.StageExecutable(downloadExe,installed);
            Check(File.Exists(staged) && BridgeSetup.FileHash(staged)==BridgeSetup.FileHash(downloadExe),"single downloaded EXE stages a complete hash-matching managed copy");
            Check(File.ReadAllText(Path.Combine(installed,"language.txt"))=="ja" && File.ReadAllText(Path.Combine(installed,"appearance.txt"))=="light" && !File.Exists(Path.Combine(installed,"error.log")),"preference migration preserves existing choices and excludes logs");
            Check(DesktopInstall.StageExecutable(downloadExe,installed)==staged,"repeated app staging uses the same managed executable");
            string standaloneGame=Game("standalone-helper");
            using(var helper=Process.Start(new ProcessStartInfo(staged,"--prepare-game "+DesktopInstall.QuoteArgument(standaloneGame)){UseShellExecute=false,CreateNoWindow=true})){Check(helper.WaitForExit(20000) && helper.ExitCode==0,"embedded installer works with only the EXE and no adjacent runtime or scripts");}
            Check(!BridgeSetup.Inspect(standaloneGame).NeedsInstall,"headless setup helper leaves a fully verified installation");
            string recoveryGame=Game("connection recovery");BridgeSetup.Install(recoveryGame,null);
            string proxy=Path.Combine(recoveryGame,"winhttp.dll"),pausedProxy=proxy+".bongochat-paused",proxyHash=BridgeSetup.FileHash(proxy);
            string recordFile=Path.Combine(recoveryGame,"BepInEx","BongoChat","room_123.jsonl");Directory.CreateDirectory(Path.GetDirectoryName(recordFile));File.WriteAllText(recordFile,"private journal sentinel");
            BridgeSetup.SetConnectionPaused(recoveryGame,true);
            Check(!File.Exists(proxy)&&File.Exists(pausedProxy)&&BridgeSetup.IsConnectionPaused(recoveryGame),"pause removes the owned DLL from the game's loader search path without deleting it");
            Check(File.ReadAllText(recordFile)=="private journal sentinel"&&File.ReadAllText(Path.Combine(recoveryGame,"BongoCat.exe"))=="original game","connection pause preserves chat history and original game files");
            Check(BridgeSetup.Inspect(recoveryGame).Problem!=null,"paused setup is recognized instead of silently reinstalled");
            Reject(()=>BridgeSetup.Install(recoveryGame,null),"automatic setup cannot undo an intentional connection pause");
            BridgeSetup.SetConnectionPaused(recoveryGame,true);
            Check(BridgeSetup.FileHash(pausedProxy)==proxyHash,"repeating pause is idempotent and retains the exact loader");
            File.WriteAllText(proxy,"foreign loader");Reject(()=>BridgeSetup.SetConnectionPaused(recoveryGame,false),"recovery never overwrites a loader installed while paused");File.Delete(proxy);
            File.WriteAllText(pausedProxy,"modified backup");Reject(()=>BridgeSetup.SetConnectionPaused(recoveryGame,false),"recovery refuses a changed backup");
            File.Copy(Path.Combine(standaloneGame,"winhttp.dll"),pausedProxy,true);
            BridgeSetup.SetConnectionPaused(recoveryGame,false);BridgeSetup.SetConnectionPaused(recoveryGame,false);
            Check(File.Exists(proxy)&&!File.Exists(pausedProxy)&&BridgeSetup.FileHash(proxy)==proxyHash&&!BridgeSetup.Inspect(recoveryGame).NeedsInstall,"restoration is idempotent and reinstates the exact verified installation");
            string unownedGame=Game("unowned recovery");File.WriteAllText(Path.Combine(unownedGame,"winhttp.dll"),"foreign");
            Reject(()=>BridgeSetup.SetConnectionPaused(unownedGame,true),"pause requires an ownership record and cannot rename a foreign loader");
            string fixtureExe=args[2];File.Copy(fixtureExe,Path.Combine(standaloneGame,"BongoCat.exe"),true);
            Environment.SetEnvironmentVariable("DOORSTOP_DISABLE","1");Environment.SetEnvironmentVariable("DOORSTOP_INITIALIZED","1");
            try
            {
                var info=ChatWindow.GameStartInfo(standaloneGame);
                Check(info.EnvironmentVariables["DOORSTOP_DISABLE"]==null && info.EnvironmentVariables["DOORSTOP_INITIALIZED"]==null,"game launch strips both inherited loader guards without changing system settings");
                using(var child=Process.Start(info)){Check(child.WaitForExit(10000),"clean game-launch fixture completes");}
                string launch=File.ReadAllText(Path.Combine(standaloneGame,"launch.txt"));
                Check(launch==standaloneGame+"\n\n","game starts in its install directory with clean child environment");
                Check(Environment.GetEnvironmentVariable("DOORSTOP_DISABLE")=="1","launcher does not change even the caller's environment");
            }
            finally{Environment.SetEnvironmentVariable("DOORSTOP_DISABLE",null);Environment.SetEnvironmentVariable("DOORSTOP_INITIALIZED",null);}
            string shortcutFolder=Path.Combine(root,"shortcuts");Directory.CreateDirectory(shortcutFolder);
            typeof(DesktopInstall).GetMethod("CreateShortcut",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{shortcutFolder,staged});
            Check(File.Exists(Path.Combine(shortcutFolder,"Bongo Cat Chat Merger.lnk")),"Windows shortcut creation works in an isolated folder");
            object shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));object link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{Path.Combine(shortcutFolder,"Bongo Cat Chat Merger.lnk")});
            string target=(string)link.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,link,null);
            Check(String.Equals(target,staged,StringComparison.OrdinalIgnoreCase),"created shortcut points to the managed executable");
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            BridgeSetup.SetConnectionPaused(recoveryGame,true);
            string recoveryState=Path.Combine(recoveryGame,"BepInEx","BongoChat","state.json");
            File.WriteAllText(recoveryState,json.Serialize(new BridgeState{protocol=1,ready=true,canSend=true,room="123",session=Guid.NewGuid().ToString("N"),utc=DateTime.UtcNow.ToString("o")}));
            var pausedWindow=new ChatWindow(false,recoveryGame,true,"ko");var pausedType=typeof(ChatWindow);var privateFlags=BindingFlags.NonPublic|BindingFlags.Instance;
            Check(!(bool)pausedType.GetField("canSend",privateFlags).GetValue(pausedWindow),"reopened paused app ignores even a fresh leftover heartbeat");
            pausedType.GetMethod("BeginSetup",privateFlags).Invoke(pausedWindow,new object[]{false});DateTime pauseDeadline=DateTime.UtcNow.AddSeconds(5);
            while((bool)pausedType.GetField("setupBusy",privateFlags).GetValue(pausedWindow)&&DateTime.UtcNow<pauseDeadline){pausedWindow.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(delegate{}));System.Threading.Thread.Sleep(10);}
            Check(BridgeSetup.IsConnectionPaused(recoveryGame)&&!File.Exists(proxy)&&(string)pausedType.GetField("setupAction",privateFlags).GetValue(pausedWindow)=="resume","automatic startup preserves pause and exposes explicit restoration");
            pausedWindow.Poll();Check(!((Button)pausedType.GetField("sendButton",privateFlags).GetValue(pausedWindow)).IsEnabled,"paused app cannot send through stale bridge state");
            pausedWindow.Snapshot(Path.Combine(root,"connection-paused-ko.png"));
            pausedWindow.Close();BridgeSetup.SetConnectionPaused(recoveryGame,false);
            string flowGame=Game("automatic UI flow");File.Copy(fixtureExe,Path.Combine(flowGame,"BongoCat.exe"),true);
            var flow=new ChatWindow(false,flowGame,true,"ko");var flowType=typeof(ChatWindow);
            flowType.GetMethod("BeginSetup",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(flow,new object[]{false});
            DateTime flowDeadline=DateTime.UtcNow.AddSeconds(20);
            while((bool)flowType.GetField("setupBusy",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flow) && DateTime.UtcNow<flowDeadline)
            {flow.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(delegate{}));System.Threading.Thread.Sleep(25);}
            Check(!(bool)flowType.GetField("setupBusy",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flow),"automatic UI setup completes without a script or folder dialog");
            Check(!BridgeSetup.Inspect(flowGame).NeedsInstall,"automatic UI setup installs all connection files");
            DateTime launchDeadline=DateTime.UtcNow.AddSeconds(5);
            while(!File.Exists(Path.Combine(flowGame,"launch.txt")) && DateTime.UtcNow<launchDeadline)System.Threading.Thread.Sleep(25);
            Check(File.Exists(Path.Combine(flowGame,"launch.txt")),"automatic UI setup launches the selected game after installation");
            Directory.CreateDirectory(Path.Combine(flowGame,"BepInEx","BongoChat"));
            File.WriteAllText(Path.Combine(flowGame,"BepInEx","BongoChat","state.json"),json.Serialize(new BridgeState{protocol=1,ready=true,canSend=true,stopped=false,room="123",session=Guid.NewGuid().ToString("N"),name="fixture room",utc=DateTime.UtcNow.ToString("o"),maxMessageLength=50}));
            flow.Poll();var statusField=flowType.GetField("statusText",BindingFlags.NonPublic|BindingFlags.Instance);
            Check(((TextBlock)statusField.GetValue(flow)).Text.Contains("연결됨") && flowType.GetField("setupNotice",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(flow)==null,"a fresh game heartbeat clears setup guidance and confirms connection");
            flow.Close();
            foreach(string code in new[]{"en","ko","ja"})
            {
                var w=new ChatWindow(true,null,true,code);var button=(Button)((Grid)w.Content).FindName("Choose");
                Check(button.Content.ToString()==UiText.Get(code,"연결 도우미") && !button.IsEnabled,code+" connection helper is localized and disabled in demo mode");
                typeof(ChatWindow).GetField("setupAction",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(w,"restart");
                typeof(ChatWindow).GetMethod("RefreshSetupLabels",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(w,null);
                Check(button.Content.ToString()==UiText.Get(code,"설치하고 게임 재시작"),code+" restart action states its effect explicitly");
                w.Snapshot(Path.Combine(root,"setup-"+code+".png"));w.Close();
            }
            Check(UiText.Get("en","자동 연결을 완료하지 못했어요: 다른 모드가 설치되어 있어요. 기존 파일을 보존하고 설치를 멈췄어요.").Contains("Another mod loader"),"setup failures translate their nested reason");
            Console.WriteLine("All "+checks+" setup checks passed.");return 0;
        }catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
}