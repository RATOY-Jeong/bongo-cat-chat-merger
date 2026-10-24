using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using BongoChat;
using BongoChat.Protocol;

public static class LocalizationTests
{
    static int checks;
    static void Check(bool value, string description) { if (!value) throw new Exception(description); checks++; Console.WriteLine("PASS " + description); }
    static object Field(object target, string name) { return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }
    static void Call(object target, string name) { target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null); }
    static void Select(ChatWindow window, string code)
    {
        var box = (ComboBox)((Grid)window.Content).FindName("Language");
        foreach (ComboBoxItem item in box.Items) if ((string)item.Tag == code) { box.SelectedItem = item; return; }
        throw new Exception("Language missing");
    }
    [STAThread] public static int Main(string[] args)
    {
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Check(!File.Exists(UiText.PreferencePath) && UiText.ReadPreference() == null, "fresh install needs language selection");
            var picker = new LanguagePicker { ShowActivated = false, Left = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
            Check(picker.Languages.Items.Count == 3 && picker.SelectedLanguage == "en", "first-run picker offers all three languages");
            Capture((FrameworkElement)picker.Content, 420, 270, "first-run-language.png");
            picker.Languages.SelectedIndex = 2;
            Check((string)picker.ContinueButton.Content == "はじめる", "picker action follows selected language");
            picker.Loaded += delegate { picker.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { picker.ContinueButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); })); };
            Check(picker.ShowDialog() == true && UiText.ReadPreference() == "ja", "first-run confirmation persists the selection");
            var reopened = new ChatWindow(true);
            Check((string)Field(reopened, "language") == "ja" && (string)((Button)((Grid)reopened.Content).FindName("Send")).Content == "送信", "main window opens after picker and uses the saved language"); reopened.Close();
            var cancelled = new LanguagePicker { ShowActivated = false, Left = -10000, WindowStartupLocation = WindowStartupLocation.Manual };
            cancelled.Languages.SelectedIndex = 1;
            cancelled.Loaded += delegate { cancelled.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(delegate { cancelled.Close(); })); };
            Check(cancelled.ShowDialog() != true && UiText.ReadPreference() == "ja", "closing the picker does not overwrite a preference");

            string game = Path.GetFullPath(args[0]); Directory.CreateDirectory(game); File.WriteAllText(Path.Combine(game, "BongoCat.exe"), "");
            string folder = Path.Combine(game, "BepInEx", "BongoChat"); Directory.CreateDirectory(folder);
            var state = new BridgeState { protocol = 1, ready = true, canSend = true, session = Guid.NewGuid().ToString("N"), room = "123", name = "Original 방名", utc = DateTime.UtcNow.ToString("o"), maxMessageLength = 50 };
            string stateFile = Path.Combine(folder, "state.json"); File.WriteAllText(stateFile, Journal.Json.Serialize(state));
            string journal = Path.Combine(folder, "room_123.jsonl");
            var entry = new ChatEntry { kind = "chat", id = "original", room = "123", name = "Original 名前", text = "原文 그대로 / original", utc = DateTime.UtcNow.ToString("o") };
            File.WriteAllText(journal, Journal.Json.Serialize(entry) + "\n"); string original = File.ReadAllText(journal);
            var w = new ChatWindow(false, game, true, "en"); var root = (Grid)w.Content;
            var composer = (TextBox)Field(w, "composer"); var status = (TextBlock)Field(w, "statusText");
            composer.Text = "보존할 日本語 draft"; ((TextBox)Field(w, "search")).Text = "original";
            Select(w, "ja");
            Check(composer.Text == "보존할 日本語 draft" && ((TextBox)Field(w, "search")).Text == "original", "switching language preserves draft and search");
            Check(File.ReadAllText(journal) == original && ((TextBlock)Field(w, "roomTitle")).Text == "Original 방名", "switching language never translates room names or edits the journal");
            Check(status.Text.StartsWith("● 接続済み") && ((TextBlock)Field(w, "resultText")).Text.Contains("1件表示"), "live status and message counts switch immediately");
            var windowAgain = new ChatWindow(true); Check((string)Field(windowAgain, "language") == "ja", "dropdown language survives a new window"); windowAgain.Close();
            Call(w, "SendComposer"); var pending = (SendRequest)Field(w, "pendingRequest"); string requestPath = Path.Combine(folder, "commands", pending.id + ".request.json"); string requestBefore = File.ReadAllText(requestPath);
            Select(w, "en");
            Check(Field(w, "pendingRequest") == pending && File.ReadAllText(requestPath) == requestBefore && composer.IsReadOnly, "language switch keeps one pending request without replay or cancellation");
            Check(((TextBlock)Field(w, "composerHint")).Text == "Sending request to the game…", "pending-send hint changes language immediately");
            Directory.CreateDirectory(Path.Combine(folder, "commands", "results"));
            File.WriteAllText(Path.Combine(folder, "commands", "results", pending.id + ".json"), Journal.Json.Serialize(new SendResult { id = pending.id, session = pending.session, room = pending.room, accepted = false, error = "게임의 채팅 필터가 전송을 허용하지 않았어요." }));
            w.Poll(); Check(((TextBlock)Field(w, "composerHint")).Text.Contains("chat filter") && composer.Text == "보존할 日本語 draft", "bridge rejection is localized and keeps the draft");
            Select(w, "ko"); Check(((TextBlock)Field(w, "composerHint")).Text == "게임의 채팅 필터가 전송을 허용하지 않았어요.", "an existing notice switches back to Korean without being lost");
            state.utc = DateTime.UtcNow.AddMinutes(-1).ToString("o"); File.WriteAllText(stateFile, Journal.Json.Serialize(state));
            Call(w, "Reconnect"); Select(w, "ja");
            Check((string)((Button)Field(w, "reconnectButton")).Content == "確認中…" && !((Button)Field(w, "reconnectButton")).IsEnabled, "language switch retains an active reconnect attempt");
            typeof(ChatWindow).GetField("reconnectDeadline", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(w, DateTime.UtcNow.AddSeconds(-1)); Call(w, "CheckReconnect");
            Check(status.Text.Contains("接続信号") && (string)((Button)Field(w, "reconnectButton")).Content == "再接続", "reconnect timeout and reset button are localized");
            w.Close();
            foreach (string code in new[] { "en", "ko", "ja" })
            {
                var demo = new ChatWindow(true, null, true, code); var box = (ComboBox)((Grid)demo.Content).FindName("Language");
                Check(box.Items.Count == 3 && (string)((ComboBoxItem)box.SelectedItem).Tag == code, code + " dropdown reflects the active language");
                demo.Snapshot(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-" + code + ".png"));
                demo.Width = 420; Capture((FrameworkElement)demo.Content, 420, 750, "minimum-" + code + ".png");
                box.ApplyTemplate(); box.IsDropDownOpen = true; demo.UpdateLayout();
                var toggle = FindToggle(box); Check(toggle != null && toggle.Foreground.ToString() == "#FFEEF3F8", code + " dropdown selected text follows the dark palette"); box.IsDropDownOpen = false; demo.Close();
            }
            Check(UiText.Get("en", "연결 오류: 게임의 채팅 입력이 준비되지 않았어요.") == "Connection error: The game's chat input is not ready.", "nested bridge error prefixes are localized");
            UiText.SavePreference("ko"); Check(UiText.ReadPreference() == "ko", "explicit Korean preference is valid on subsequent starts");
            File.WriteAllText(UiText.PreferencePath, "bad-language"); Check(UiText.ReadPreference() == null, "invalid stored language returns to first-run selection");
            Console.WriteLine("All " + checks + " localization checks passed."); return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
    static System.Windows.Controls.Primitives.ToggleButton FindToggle(System.Windows.DependencyObject node)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); i++) { var child = System.Windows.Media.VisualTreeHelper.GetChild(node, i); var toggle = child as System.Windows.Controls.Primitives.ToggleButton; if (toggle != null) return toggle; var found = FindToggle(child); if (found != null) return found; } return null;
    }
    static void Capture(FrameworkElement root, int width, int height, string file)
    {
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        root.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var output = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file))) encoder.Save(output);
    }
}
