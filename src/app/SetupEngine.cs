using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;
namespace BongoChat
{
    public static class GameLocator
    {
        public static bool IsGame(string folder)
        {
            return !String.IsNullOrEmpty(folder) && File.Exists(Path.Combine(folder, "BongoCat.exe"))
                && File.Exists(Path.Combine(folder, "BongoCat_Data", "Managed", "Assembly-CSharp.dll"));
        }
        public static string Find(string preferred)
        {
            if (IsGame(preferred)) return Path.GetFullPath(preferred);
            foreach (Process process in Process.GetProcessesByName("BongoCat"))
            {
                using (process) try { string folder = Path.GetDirectoryName(process.MainModule.FileName); if (IsGame(folder)) return folder; } catch { }
            }
            var roots = new List<string>();
            foreach (string key in new[] { @"HKEY_CURRENT_USER\Software\Valve\Steam", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam" })
            {
                try { string root = Registry.GetValue(key, key.Contains("CURRENT_USER") ? "SteamPath" : "InstallPath", null) as string; if (!String.IsNullOrEmpty(root)) roots.Add(root); } catch { }
            }
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            return FindInLibraries(roots, null);
        }
        public static string FindInLibraries(IEnumerable<string> initialRoots, string preferred)
        {
            if (IsGame(preferred)) return Path.GetFullPath(preferred);
            var roots = new List<string>(initialRoots.Where(r => !String.IsNullOrWhiteSpace(r)));
            foreach (string root in roots.ToArray())
            {
                try
                {
                    string libraries = File.ReadAllText(Path.Combine(root, "steamapps", "libraryfolders.vdf"));
                    foreach (Match match in Regex.Matches(libraries, "\"path\"\\s+\"([^\"]+)\"")) roots.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string manifest = Path.Combine(root, "steamapps", "appmanifest_3419430.acf");
                    if (File.Exists(manifest))
                    {
                        var match = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                        if (match.Success)
                        {
                            string common = Path.GetFullPath(Path.Combine(root, "steamapps", "common"));
                            string folder = Path.GetFullPath(Path.Combine(common, match.Groups[1].Value));
                            if (folder.StartsWith(common + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && IsGame(folder)) return folder;
                        }
                    }
                    string fallback = Path.Combine(root, "steamapps", "common", "BongoCat");
                    if (IsGame(fallback)) return fallback;
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { }
            }
            return "";
        }
        public static bool IsRunning(string folder)
        {
            foreach (Process process in Process.GetProcessesByName("BongoCat"))
            {
                using (process) try { if (String.Equals(process.MainModule.FileName, Path.Combine(folder, "BongoCat.exe"), StringComparison.OrdinalIgnoreCase)) return true; }
                    catch { return true; }
            }
            return false;
        }
        public static bool CloseGame(string folder)
        {
            bool requested = true;
            foreach (Process process in Process.GetProcessesByName("BongoCat"))
            {
                using (process) try
                {
                    if (String.Equals(process.MainModule.FileName, Path.Combine(folder, "BongoCat.exe"), StringComparison.OrdinalIgnoreCase)) requested &= process.CloseMainWindow();
                }
                catch { requested = false; }
            }
            return requested;
        }
    }
    public sealed class InstallRecord
    {
        public string relative { get; set; }
        public string sha256 { get; set; }
        public string previousSha256 { get; set; }
    }
    public sealed class InstallManifest
    {
        public string version { get; set; }
        public string gamePath { get; set; }
        public string installedUtc { get; set; }
        public bool pending { get; set; }
        public InstallRecord[] files { get; set; }
    }
    public sealed class SetupInspection { public bool NeedsInstall; public string Problem; }
    internal sealed class PayloadFile { public string Entry, Relative, Hash; }
    public static partial class BridgeSetup
    {
        public const string ResourceName = "BongoChat.SetupPayload";
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1048576 };
        private static Stream Payload() { var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName); if (stream == null) throw new IOException("연결 파일이 누락됐어요. 전체 실행파일을 다시 받아 주세요."); return stream; }
        public static string Hash(Stream input) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", ""); }
        public static string FileHash(string path) { using (var input = File.OpenRead(path)) return Hash(input); }
        private static List<PayloadFile> ReadPlan(ZipArchive zip)
        {
            var result = new List<PayloadFile>(); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith("/")) continue;
                string relative = entry.FullName == "BongoChatBridge.dll" ? "BepInEx/plugins/BongoChat/BongoChatBridge.dll" : entry.FullName.StartsWith("runtime/", StringComparison.Ordinal) ? entry.FullName.Substring(8) : null;
                if (relative == null || relative.Split('/').Any(p => p == ".." || p == "." || p.Length == 0) || relative.IndexOfAny(new[] { ':', '\\' }) >= 0 || Path.IsPathRooted(relative)
                    || !names.Add(relative) || entry.Length > 32 * 1024 * 1024 || (total += entry.Length) > 96 * 1024 * 1024) throw new IOException("연결 파일의 구성이 올바르지 않아요.");
                using (var source = entry.Open()) result.Add(new PayloadFile { Entry = entry.FullName, Relative = relative.Replace('/', Path.DirectorySeparatorChar), Hash = Hash(source) });
            }
            if (!names.Contains("winhttp.dll") || !names.Contains("doorstop_config.ini") || !names.Contains("BepInEx/core/BepInEx.dll") || !names.Contains("BepInEx/plugins/BongoChat/BongoChatBridge.dll")) throw new IOException("연결 파일의 구성이 올바르지 않아요.");
            return result;
        }
        private static string Destination(string game, string relative)
        {
            string path = Path.GetFullPath(Path.Combine(game, relative));
            if (!path.StartsWith(game + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("설치 경로를 확인할 수 없어요.");
            for (string part = Path.GetDirectoryName(path); part != null; part = Path.GetDirectoryName(part))
            {
                if (Directory.Exists(part) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new IOException("연결된 폴더에는 자동 설치할 수 없어요.");
                if (String.Equals(part, game, StringComparison.OrdinalIgnoreCase)) break;
            }
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("연결된 파일에는 자동 설치할 수 없어요.");
            return path;
        }
        private static string NormalizeGame(string game)
        {
            if (!GameLocator.IsGame(game)) throw new IOException("봉고캣 설치 위치를 찾지 못했어요.");
            return Path.GetFullPath(game).TrimEnd(Path.DirectorySeparatorChar);
        }
        private static InstallManifest ReadManifest(string game, List<PayloadFile> plan)
        {
            string path = Destination(game, "BongoChat-install.json");
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 1048576) throw new IOException("기존 설치 정보를 확인할 수 없어요.");
            InstallManifest manifest;
            try { manifest = Json.Deserialize<InstallManifest>(File.ReadAllText(path)); } catch (ArgumentException) { throw new IOException("기존 설치 정보를 확인할 수 없어요."); }
            if (manifest == null || manifest.files == null || manifest.gamePath == null || !String.Equals(manifest.gamePath.TrimEnd('\\'), game, StringComparison.OrdinalIgnoreCase)) throw new IOException("기존 설치 정보를 확인할 수 없어요.");
            var allowed = new HashSet<string>(plan.Select(p => p.Relative), StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.files)
                if (file == null || !allowed.Contains(file.relative) || !seen.Add(file.relative) || !Regex.IsMatch(file.sha256 ?? "", "^[A-Fa-f0-9]{64}$")) throw new IOException("기존 설치 정보를 확인할 수 없어요.");
            return manifest;
        }
        private static SetupInspection Inspect(string game, List<PayloadFile> plan, InstallManifest manifest)
        {
            if (IsConnectionPaused(game)) return new SetupInspection { Problem = "연결 기능을 꺼 둔 상태예요. 연결 도우미에서 복원할 수 있어요." };
            foreach (string foreign in new[] { "version.dll", "MelonLoader" })
                if (File.Exists(Destination(game, foreign)) || Directory.Exists(Destination(game, foreign))) return new SetupInspection { Problem = "다른 모드가 설치되어 있어요. 기존 파일을 보존하고 설치를 멈췄어요." };
            if (manifest == null)
            {
                foreach (string marker in new[] { "winhttp.dll", "version.dll", "doorstop_config.ini", "BepInEx/core/BepInEx.dll", "MelonLoader" })
                    if (File.Exists(Destination(game, marker)) || Directory.Exists(Destination(game, marker))) return new SetupInspection { Problem = "다른 모드가 설치되어 있어요. 기존 파일을 보존하고 설치를 멈췄어요." };
            }
            var result = new SetupInspection();
            foreach (var file in plan)
            {
                string path = Destination(game, file.Relative);
                if (!File.Exists(path)) { result.NeedsInstall = true; continue; }
                string hash = FileHash(path);
                if (String.Equals(hash, file.Hash, StringComparison.OrdinalIgnoreCase)) continue;
                if (file.Relative == @"BepInEx\config\BepInEx.cfg" && Regex.IsMatch(File.ReadAllText(path), @"(?m)^UnityLogListening\s*=\s*false\s*$")) continue;
                InstallRecord owned = manifest == null ? null : manifest.files.FirstOrDefault(f => String.Equals(f.relative, file.Relative, StringComparison.OrdinalIgnoreCase));
                if (owned == null || !(String.Equals(owned.sha256, hash, StringComparison.OrdinalIgnoreCase) || (manifest.pending && String.Equals(owned.previousSha256, hash, StringComparison.OrdinalIgnoreCase))))
                    return new SetupInspection { Problem = "설치 파일이 다른 프로그램에서 변경됐어요. 기존 파일을 보존했어요: " + file.Relative };
                result.NeedsInstall = true;
            }
            if (manifest == null || manifest.pending) result.NeedsInstall = true;
            return result;
        }
        public static SetupInspection Inspect(string folder)
        {
            string game = NormalizeGame(folder);
            using (var input = Payload()) using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
            { var plan = ReadPlan(zip); return Inspect(game, plan, ReadManifest(game, plan)); }
        }
        private static void SaveManifest(string path, InstallManifest manifest)
        {
            string temporary = Destination(Path.GetDirectoryName(path), Path.GetFileName(path) + ".tmp");
            File.WriteAllText(temporary, Json.Serialize(manifest), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        public static void Install(string folder, Action<int> progress)
        {
            string game = NormalizeGame(folder);
            string mutexId; using (var input = new MemoryStream(Encoding.UTF8.GetBytes(game.ToUpperInvariant()))) mutexId = Hash(input).Substring(0, 24);
            using (var mutex = new Mutex(false, "Local\\BongoChat.Setup." + mutexId))
            {
                bool locked = false;
                try
                {
                    try { locked = mutex.WaitOne(0); } catch (AbandonedMutexException) { locked = true; }
                    if (!locked) throw new IOException("다른 창에서 연결을 준비하고 있어요. 잠시 후 다시 눌러 주세요.");
                    if (GameLocator.IsRunning(game)) throw new IOException("연결 준비를 위해 봉고캣을 먼저 종료해 주세요.");
                    using (var input = Payload()) using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
                    {
                        var plan = ReadPlan(zip); var old = ReadManifest(game, plan); var check = Inspect(game, plan, old);
                        if (check.Problem != null) throw new IOException(check.Problem);
                        if (!check.NeedsInstall) return;
                        var records = new List<InstallRecord>();
                        foreach (var file in plan)
                        {
                            string path = Destination(game, file.Relative), current = File.Exists(path) ? FileHash(path) : null;
                            bool preserve = file.Relative == @"BepInEx\config\BepInEx.cfg" && current != null && !String.Equals(current, file.Hash, StringComparison.OrdinalIgnoreCase);
                            if (preserve) { if (old != null) { var previous = old.files.FirstOrDefault(f => f.relative == file.Relative); if (previous != null) records.Add(previous); } continue; }
                            records.Add(new InstallRecord { relative = file.Relative, sha256 = file.Hash, previousSha256 = current });
                        }
                        var manifest = new InstallManifest { version = "0.2.2", gamePath = game, installedUtc = DateTime.UtcNow.ToString("o"), pending = true, files = records.ToArray() };
                        if (GameLocator.IsRunning(game)) throw new IOException("연결 준비를 위해 봉고캣을 먼저 종료해 주세요.");
                        string manifestPath = Destination(game, "BongoChat-install.json"); SaveManifest(manifestPath, manifest);
                        int completed = 0;
                        foreach (var file in plan)
                        {
                            var record = records.FirstOrDefault(f => f.relative == file.Relative);
                            string path = Destination(game, file.Relative);
                            bool preserveConfig = file.Relative == @"BepInEx\config\BepInEx.cfg" && File.Exists(path) && !String.Equals(FileHash(path), file.Hash, StringComparison.OrdinalIgnoreCase)
                                && Regex.IsMatch(File.ReadAllText(path), @"(?m)^UnityLogListening\s*=\s*false\s*$");
                            if (!preserveConfig && record != null && String.Equals(record.sha256, file.Hash, StringComparison.OrdinalIgnoreCase) && (!File.Exists(path) || !String.Equals(FileHash(path), file.Hash, StringComparison.OrdinalIgnoreCase)))
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(path)); string temporary = Destination(game, file.Relative + ".bongochat-new");
                                using (var source = zip.GetEntry(file.Entry).Open()) using (var target = File.Create(temporary)) source.CopyTo(target);
                                if (!String.Equals(FileHash(temporary), file.Hash, StringComparison.OrdinalIgnoreCase)) throw new IOException("연결 파일 검증에 실패했어요.");
                                if (File.Exists(path))
                                {
                                    string backup = Destination(game, Path.Combine("BepInEx", "BongoChat", "installer-backups", DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), file.Relative));
                                    Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Replace(temporary, path, backup);
                                }
                                else File.Move(temporary, path);
                            }
                            if (progress != null) progress(++completed * 100 / plan.Count);
                        }
                        manifest.pending = false; foreach (var record in manifest.files) record.previousSha256 = null; SaveManifest(manifestPath, manifest);
                    }
                }
                finally { if (locked) mutex.ReleaseMutex(); }
            }
        }
    }
}