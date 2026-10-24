using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Threading;

namespace BongoChat
{
    internal sealed class UiHealthMonitor : IDisposable
    {
        private readonly DispatcherTimer pulse;
        private readonly Timer observer;
        private readonly string path;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private long lastPulse, lastReport = -30000;
        private string phase = "starting";
        private int writing;
        public UiHealthMonitor(Dispatcher dispatcher, string logPath)
        {
            path = logPath;
            pulse = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(1) };
            pulse.Tick += delegate { Interlocked.Exchange(ref lastPulse, clock.ElapsedMilliseconds); };
            pulse.Start();
            observer = new Timer(Check, null, 1000, 1000);
        }
        public void Phase(string value) { phase = value; }
        private void Check(object state)
        {
            long now = clock.ElapsedMilliseconds;
            long delay = now - Interlocked.Read(ref lastPulse);
            if (delay < 8000 || now - Interlocked.Read(ref lastReport) < 30000 || Interlocked.Exchange(ref writing, 1) != 0) return;
            try
            {
                Interlocked.Exchange(ref lastReport, now);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(path) && new FileInfo(path).Length > 65536)
                { try { File.Delete(path + ".previous"); File.Move(path, path + ".previous"); } catch { } }
                File.AppendAllText(path, DateTime.UtcNow.ToString("o") + " version=" +
                    typeof(ChatWindow).Assembly.GetName().Version + " UI-delay-ms=" + delay +
                    " phase=" + phase + " CLR=" + Environment.Version + " OS=" + Environment.OSVersion.Version +
                    " pen=" + (TabletCompatibility.Status ?? "not-configured") + Environment.NewLine);
            }
            catch { }
            finally { Interlocked.Exchange(ref writing, 0); }
        }
        public static string LastReport()
        {
            try
            {
                string path = AppStorage.PathFor("ui-health.log");
                if (!File.Exists(path)) return "No stalled UI recorded";
                string text = Journal.ReadShared(path, 131072);
                return text.Length > 4096 ? text.Substring(text.Length - 4096) : text;
            }
            catch { return "UI diagnostic log unavailable"; }
        }
        public void Dispose() { pulse.Stop(); observer.Dispose(); }
    }
}