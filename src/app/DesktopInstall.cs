using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
namespace BongoChat
{
    public static class AppStorage
    {
        public static string Root = AppDomain.CurrentDomain.BaseDirectory;
        public static string PathFor(string name) { return Path.Combine(Root, name); }
        public static string ManagedRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BongoCatChatMerger"); } }
    }
    public static class DesktopInstall
    {
        public static readonly string[] Preferences = { "language.txt", "appearance.txt", "always-on-top.txt", "show-emotes.txt", "game-path.txt" };
        public static void MigratePreferences(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string name in Preferences)
            {
                string previous = Path.Combine(source, name), next = Path.Combine(target, name);
                if (File.Exists(previous) && !File.Exists(next) && new FileInfo(previous).Length <= 32768) File.Copy(previous, next);
            }
        }
        public static bool Prepare()
        {
            string id = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
            using (var gate = new System.Threading.Mutex(false, "Local\\BongoChat.Stage." + id))
            {
                bool held = false;
                try
                {
                    try { held = gate.WaitOne(10000); } catch (System.Threading.AbandonedMutexException) { held = true; }
                    if (!held) throw new IOException("Another Bongo Cat Chat Merger launch is still preparing. Try again shortly.");
                    return PrepareCore();
                }
                finally { if (held) gate.ReleaseMutex(); }
            }
        }
        private static bool PrepareCore()
        {
            string source = Assembly.GetExecutingAssembly().Location, root = AppStorage.ManagedRoot;
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            AppStorage.Root = root;
            string destination = StageExecutable(source, root);
            bool same = String.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase);
            foreach (string folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.Programs) })
                try { CreateShortcut(folder, destination); } catch { /* A locked desktop does not prevent chat. */ }
            File.WriteAllText(Path.Combine(root, "current-app.txt"), destination, new UTF8Encoding(false));
            if (!same) { Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true, WorkingDirectory = root }); return true; }
            return false;
        }
        public static string StageExecutable(string source, string root)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            string destination = Path.Combine(root, "app-" + version + "-" + BridgeSetup.FileHash(source).Substring(0, 12), "BongoCatChatMerger.exe");
            MigratePreferences(Path.GetDirectoryName(source), root);
            if (!String.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (!File.Exists(destination) || BridgeSetup.FileHash(destination) != BridgeSetup.FileHash(source)) File.Copy(source, destination, true);
            }
            return destination;
        }
        private static void CreateShortcut(string folder, string executable)
        {
            if (String.IsNullOrEmpty(folder)) return;
            Directory.CreateDirectory(folder); object shell = null, link = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
                link = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { Path.Combine(folder, "Bongo Cat Chat Merger.lnk") });
                var type = link.GetType();
                type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, new object[] { executable });
                type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, new object[] { AppStorage.ManagedRoot });
                type.InvokeMember("Description", BindingFlags.SetProperty, null, link, new object[] { "Bongo Cat Chat Merger" });
                type.InvokeMember("IconLocation", BindingFlags.SetProperty, null, link, new object[] { executable + ",0" });
                type.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
            finally { if (link != null) Marshal.FinalReleaseComObject(link); if (shell != null) Marshal.FinalReleaseComObject(shell); }
        }
        public static int RunSetupHelper(string game)
        {
            try { BridgeSetup.Install(game, null); return 0; }
            catch (UnauthorizedAccessException) { return 5; }
            catch (Exception ex) { try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "setup-helper-error.log"), ex.ToString()); } catch { } return 1; }
        }
        public static bool InstallElevated(string game)
        {
            var info = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location)
            {
                UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden,
                Arguments = "--prepare-game " + QuoteArgument(game)
            };
            using (var process = Process.Start(info))
            {
                // No forced termination: the manifest makes a later attempt recoverable.
                if (!process.WaitForExit(120000)) throw new IOException("권한 창의 연결 설치가 아직 진행 중이에요. 잠시 후 다시 눌러 주세요.");
                return process.ExitCode == 0;
            }
        }
        public static string QuoteArgument(string value)
        {
            // Standard Windows argv quoting, including trailing backslashes.
            if (value == null) return "\"\"";
            var result = new StringBuilder("\""); int slashes = 0;
            foreach (char ch in value)
            {
                if (ch == '\\') { slashes++; continue; }
                if (ch == '"') { result.Append('\\', slashes * 2 + 1).Append('"'); slashes = 0; continue; }
                result.Append('\\', slashes).Append(ch); slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}