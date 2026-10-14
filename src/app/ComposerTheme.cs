using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BongoChat.Protocol;

namespace BongoChat
{
    public sealed partial class ChatWindow
    {
        private bool dark, statusConnected, canSend, imeComposing, showEmotes;
        private Grid rootGrid;
        private TextBox composer;
        private Button sendButton;
        private TextBlock composerHint;
        private BridgeState liveState;
        private SendRequest pendingRequest;
        private DateTime pendingSince;
        private int currentAssetRevision = -1;
        private readonly Dictionary<int, BitmapSource> emoteImages = new Dictionary<int, BitmapSource>();
        private readonly string themePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appearance.txt");
        private readonly string pinPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "always-on-top.txt");
        private readonly string emotePreferencePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "show-emotes.txt");
        private string sendNotice = "";

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private static SolidColorBrush RawBrush(string color) { return (SolidColorBrush)new BrushConverter().ConvertFromString(color); }
        private bool ReadDarkPreference()
        {
            try { return !File.Exists(themePath) || File.ReadAllText(themePath).Trim() != "light"; }
            catch { return true; }
        }
        private bool ReadPinPreference()
        {
            try { return !File.Exists(pinPath) || File.ReadAllText(pinPath).Trim() != "false"; }
            catch { return true; }
        }
        private void SavePinPreference(bool enabled)
        {
            try { File.WriteAllText(pinPath, enabled ? "true" : "false", new UTF8Encoding(false)); }
            catch { if (composerHint != null) composerHint.Text = "항상 위는 적용됐지만 설정 파일을 저장하지 못했어요."; }
        }
        private bool ReadEmotePreference()
        {
            try { return !File.Exists(emotePreferencePath) || File.ReadAllText(emotePreferencePath).Trim() != "false"; }
            catch { return true; }
        }
        private void ChangeEmoteVisibility(bool enabled)
        {
            showEmotes = enabled;
            RenderMessages(false);
            try { File.WriteAllText(emotePreferencePath, enabled ? "true" : "false", new UTF8Encoding(false)); }
            catch { composerHint.Text = "이모티콘 표시는 적용됐지만 설정 파일을 저장하지 못했어요."; }
        }
        private string ThemeColor(string color)
        {
            if (!dark) return color;
            switch (color.ToUpperInvariant())
            {
                case "#BACDDC": return "#131922";
                case "#FCFCF8": return "#1D2530";
                case "#FFFFFF": return "#2A3544";
                case "#D3E0E9": return "#253140";
                case "#DEE5E8": case "#E3E9ED": case "#DAE4E9": return "#354354";
                case "#263A46": case "#314A5A": case "#293B46": return "#EEF3F8";
                case "#394754": case "#486477": case "#506471": case "#526675": return "#CBD6E1";
                case "#667682": case "#677E8E": case "#718FA3": case "#718799": case "#718795": return "#A4B5C6";
                case "#84909A": case "#8B918D": case "#91A0A8": case "#9AA5AC": return "#93A5B7";
                case "#A7BFCE": return "#293746";
                case "#496373": return "#B9CBD9";
                case "#E2EFEA": return "#203D35";
                case "#38725A": return "#A0DFC3";
                case "#FFF3CA": return "#423A25";
                case "#775D25": return "#F3D995";
                default: return color;
            }
        }
        private static string ThemedLayout()
        {
            return Regex.Replace(Layout, "#[0-9A-Fa-f]{6}", delegate(Match match) { return "{DynamicResource Color" + match.Value.Substring(1).ToUpperInvariant() + "}"; });
        }
        private void InitializeComposerAndTheme(Grid root)
        {
            rootGrid = root;
            composer = (TextBox)root.FindName("Composer");
            sendButton = (Button)root.FindName("Send");
            composerHint = (TextBlock)root.FindName("ComposerHint");
            var toggle = (CheckBox)root.FindName("DarkMode");
            toggle.IsChecked = dark;
            toggle.Checked += delegate { ChangeTheme(true); };
            toggle.Unchecked += delegate { ChangeTheme(false); };
            var emoteToggle = (CheckBox)root.FindName("ShowEmotes");
            emoteToggle.IsChecked = showEmotes;
            emoteToggle.Checked += delegate { ChangeEmoteVisibility(true); };
            emoteToggle.Unchecked += delegate { ChangeEmoteVisibility(false); };
            sendButton.Click += delegate { SendComposer(); };
            composer.TextChanged += delegate { if (pendingRequest == null) sendNotice = ""; RefreshComposer(); };
            TextCompositionManager.AddPreviewTextInputStartHandler(composer, delegate { imeComposing = true; });
            TextCompositionManager.AddPreviewTextInputHandler(composer, delegate { imeComposing = false; });
            composer.LostKeyboardFocus += delegate { imeComposing = false; };
            composer.PreviewKeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.Key == Key.Return && (Keyboard.Modifiers & ModifierKeys.Shift) == 0 && !imeComposing)
                { args.Handled = true; SendComposer(); }
            };
            SourceInitialized += delegate { UpdateTitleBar(); };
            ApplyTheme();
            RefreshComposer();
        }
        private void ChangeTheme(bool enabled)
        {
            dark = enabled; ApplyTheme(); RenderMessages(false);
            try { File.WriteAllText(themePath, dark ? "dark" : "light", new UTF8Encoding(false)); }
            catch { composerHint.Text = "테마는 적용됐지만 설정 파일을 저장하지 못했어요."; }
        }
        private void ApplyTheme()
        {
            foreach (Match match in Regex.Matches(Layout, "#[0-9A-Fa-f]{6}"))
                rootGrid.Resources["Color" + match.Value.Substring(1).ToUpperInvariant()] = Brush(match.Value);
            sendButton.Foreground = RawBrush("#293B46");
            SetStatus(statusText.Text, statusConnected);
            UpdateTitleBar();
        }
        private void UpdateTitleBar()
        {
            try { int flag = dark ? 1 : 0; IntPtr hwnd = new WindowInteropHelper(this).Handle; if (hwnd != IntPtr.Zero) DwmSetWindowAttribute(hwnd, 20, ref flag, 4); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        private void RefreshComposer()
        {
            if (composer == null) return;
            composer.IsReadOnly = pendingRequest != null;
            sendButton.IsEnabled = !demo && canSend && pendingRequest == null && !String.IsNullOrWhiteSpace(composer.Text);
            composerHint.Text = demo ? "화면 미리보기 · 실제 메시지는 전송하지 않아요" :
                pendingRequest != null ? "게임에 전송 요청 중…" :
                !String.IsNullOrEmpty(sendNotice) ? sendNotice :
                !canSend ? "채팅방 연결과 최신 연결 플러그인이 준비되면 전송할 수 있어요" :
                "Enter 전송 · Shift+Enter 줄바꿈 · " + composer.Text.Length + "/" + composer.MaxLength;
        }
        private void SendComposer()
        {
            if (demo || !canSend || pendingRequest != null || liveState == null) return;
            var request = new SendRequest { id = Guid.NewGuid().ToString("N"), session = liveState.session, room = liveState.room,
                createdUtc = DateTime.UtcNow.ToString("o"), kind = "send_chat", text = composer.Text.Trim() };
            string problem = RequestPolicy.Validate(request, liveState.session, liveState.room, DateTime.UtcNow, composer.MaxLength);
            if (problem != null) { sendNotice = problem; RefreshComposer(); return; }
            try
            {
                string folder = Path.Combine(BridgeFolder, "commands"); Directory.CreateDirectory(folder);
                string destination = Path.Combine(folder, request.id + ".request.json");
                File.WriteAllText(destination + ".tmp", json.Serialize(request), new UTF8Encoding(false));
                File.Move(destination + ".tmp", destination);
                pendingRequest = request; pendingSince = DateTime.UtcNow; sendNotice = "";
            }
            catch (Exception ex) { sendNotice = "전송 요청을 만들지 못했어요: " + ex.Message; }
            RefreshComposer();
        }
        private void CheckPending(BridgeState state)
        {
            if (pendingRequest == null) return;
            bool changed = state == null || state.session != pendingRequest.session || state.room != pendingRequest.room || state.stopped;
            string resultPath = Path.Combine(BridgeFolder, "commands", "results", pendingRequest.id + ".json");
            if (!changed && File.Exists(resultPath))
            {
                SendResult result = json.Deserialize<SendResult>(Journal.ReadShared(resultPath));
                if (result == null || result.id != pendingRequest.id || result.session != pendingRequest.session || result.room != pendingRequest.room) return;
                if (result.accepted)
                {
                    if (composer.Text.Trim() == pendingRequest.text) composer.Clear();
                    sendNotice = "게임에 전달했어요 · 수신한 메시지가 대화에 표시돼요";
                }
                else sendNotice = result.error ?? "게임에서 전송 요청을 처리하지 못했어요.";
                pendingRequest = null;
                try { File.Delete(resultPath); } catch { }
            }
            else if (changed || (DateTime.UtcNow - pendingSince).TotalSeconds > 22)
            {
                sendNotice = changed ? "게임 또는 방이 바뀌어 전송을 취소했어요. 입력한 글은 남겨 뒀어요." :
                    "전송 결과를 확인하지 못했어요. 대화에 표시됐는지 확인한 뒤 다시 보내 주세요.";
                try { File.Delete(Path.Combine(BridgeFolder, "commands", pendingRequest.id + ".request.json")); } catch { }
                pendingRequest = null;
            }
        }
        private UIElement EmoteContent(ChatEntry entry)
        {
            var panel = new StackPanel();
            BitmapSource image;
            if (!emoteImages.TryGetValue(entry.emoteId, out image) && !String.IsNullOrEmpty(gamePath))
            {
                string path = Path.Combine(BridgeFolder, "emotes", entry.emoteId + ".png");
                try
                {
                    if (File.Exists(path) && new FileInfo(path).Length <= 2 * 1024 * 1024)
                    {
                        using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.DecodePixelWidth = 128; bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze();
                            image = bitmap; emoteImages[entry.emoteId] = image;
                        }
                    }
                }
                catch (IOException) { }
                catch (NotSupportedException) { }
                catch (FileFormatException) { }
            }
            if (image != null) panel.Children.Add(new Image { Source = image, Width = 82, Height = 82, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 6) });
            else panel.Children.Add(new TextBlock { Text = demo ? "💛" : "✦", FontSize = 42, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(6) });
            panel.Children.Add(new TextBlock { Text = "이모티콘 · " + entry.text, FontSize = 10, TextWrapping = TextWrapping.Wrap,
                Foreground = entry.mine ? RawBrush("#293B46") : Brush("#506471") });
            return panel;
        }
    }
}
