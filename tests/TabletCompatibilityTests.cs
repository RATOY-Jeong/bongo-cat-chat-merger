using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BongoChat;

public static class TabletCompatibilityTests
{
    static int checks;
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        checks++; Console.WriteLine("PASS " + description);
    }
    static object StylusProperty(string name)
    {
        Type type = typeof(InputManager).Assembly.GetType("System.Windows.Input.StylusLogic", true);
        return type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
    }
    static string Control()
    {
        using (var child = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--control")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
        {
            string result = child.StandardOutput.ReadToEnd().Trim();
            if (!child.WaitForExit(10000) || child.ExitCode != 0) throw new Exception("Control process failed");
            return result;
        }
    }
    [STAThread] public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--control")
            {
                Console.WriteLine(StylusProperty("IsStylusAndTouchSupportEnabled")); return 0;
            }
            string root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root); AppStorage.Root = root;
            string before = Control();
            Check(before == "True" || before == "False", "independent control process reports the runtime's default pen mode");
            string preview = Path.Combine(root, "startup-en.png");
            int exit = Program.Main(new[] { "--preview", preview, "--lang=en", "--dark" });
            Check(exit == 0 && File.Exists(preview) && new FileInfo(preview).Length > 1000, "real application entry point renders a usable window after pen mitigation");
            Check(TabletCompatibility.Applied, "pen mitigation is applied during actual startup");
            Check(!(bool)StylusProperty("IsStylusAndTouchSupportEnabled"), "WPF itself confirms its stylus and touch stack is disabled");
            Check(StylusProperty("CurrentStylusLogic") == null, "rendered chat has not created the WPF tablet event processor");
            Check(Tablet.TabletDevices.Count == 0 && StylusProperty("CurrentStylusLogic") == null, "querying devices does not re-enable WPF tablet processing");
            Check(TabletCompatibility.Configure() && !String.IsNullOrEmpty(TabletCompatibility.Status), "repeat initialization preserves mouse compatibility and diagnostic status");
            foreach (string language in new[] { "en", "ko", "ja" })
            {
                var window = new ChatWindow(true, null, true, language);
                var grid = (Grid)window.Content;
                var composer = (TextBox)grid.FindName("Composer");
                composer.Text = "한글 English 日本語";
                var languages = (ComboBox)grid.FindName("Language");
                window.Snapshot(Path.Combine(root, "tablet-" + language + ".png"));
                Check(composer.Text == "한글 English 日本語" && !composer.IsReadOnly && languages.Items.Count == 3
                    && StylusProperty("CurrentStylusLogic") == null, language + " chat controls and multilingual text work without a tablet processor");
                window.Close();
            }
            Check(Control() == before, "a separate process keeps its original pen mode; no system-wide setting changes");
            Console.WriteLine("All " + checks + " tablet compatibility checks passed."); return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}