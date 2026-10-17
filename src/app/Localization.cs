using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace BongoChat
{
    public static class UiText
    {
        public static readonly string PreferencePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "language.txt");
        public static bool IsLanguage(string code) { return code == "en" || code == "ko" || code == "ja"; }
        public static string ReadPreference()
        {
            try { string value = File.ReadAllText(PreferencePath).Trim(); return IsLanguage(value) ? value : null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
        public static void SavePreference(string code)
        {
            if (!IsLanguage(code)) throw new ArgumentException("Unsupported language.");
            File.WriteAllText(PreferencePath, code, new UTF8Encoding(false));
        }
        public static CultureInfo Culture(string code) { return CultureInfo.GetCultureInfo(code == "ja" ? "ja-JP" : code == "ko" ? "ko-KR" : "en-US"); }
        // Korean keys are also the bridge's existing error messages; its protocol stays unchanged.
        private static readonly string[][] Rows = {
            new[] { "다크 모드", "Dark mode", "ダークモード" },
            new[] { "항상 위", "Always on top", "最前面に表示" },
            new[] { "이모티콘 표시", "Show emotes", "絵文字を表示" },
            new[] { "끄면 이모티콘을 대화 목록과 검색에서 숨깁니다. 기록은 보존됩니다.", "Hide emotes from chat and search. Saved records are kept.", "チャットと検索で絵文字を非表示にします。記録は保持されます。" },
            new[] { "재연결", "Reconnect", "再接続" },
            new[] { "확인 중…", "Checking…", "確認中…" },
            new[] { "게임 폴더와 연결 상태를 다시 확인합니다.", "Check the game folder and connection again.", "ゲームフォルダーと接続状態を再確認します。" },
            new[] { "검색", "Search", "検索" },
            new[] { "메시지 또는 닉네임 검색", "Search messages or names", "メッセージ・名前を検索" },
            new[] { "전송", "Send", "送信" },
            new[] { "게임 폴더", "Game folder", "ゲームフォルダー" },
            new[] { "대화 저장", "Save chat", "会話を保存" },
            new[] { "투명도", "Opacity", "不透明度" },
            new[] { "문의 · ", "Contact · ", "お問い合わせ · " },
            new[] { "기본 메일 앱에서 문의 메일 작성", "Write an email in your default mail app", "既定のメールアプリで作成" },
            new[] { "Enter로 전송 · Shift+Enter 줄바꿈", "Enter to send · Shift+Enter for a new line", "Enterで送信 · Shift+Enterで改行" },
            new[] { "봉고캣 채팅방", "Bongo Cat room", "Bongo Catのルーム" },
            new[] { "같은 방의 대화를 한곳에", "Your room's chat in one place", "同じルームの会話をひとつに" },
            new[] { "참여 중 {0}명 · 실시간 대화", "{0} members · Live chat", "参加者 {0}人 · ライブチャット" },
            new[] { "참여 중 4명 · 화면 미리보기", "4 members · Preview", "参加者 4人 · プレビュー" },
            new[] { "연결 플러그인을 기다리고 있어요", "Waiting for the bridge", "接続プラグインを待っています" },
            new[] { "게임 폴더에서 BongoCat.exe를 선택해 주세요", "Choose BongoCat.exe in Game folder", "ゲームフォルダーからBongoCat.exeを選択してください" },
            new[] { "연결 플러그인 설치 후 봉고캣을 실행해 주세요", "Install the bridge, then start Bongo Cat", "接続プラグインをインストールしてBongo Catを起動してください" },
            new[] { "연결 플러그인 버전을 확인해 주세요", "Check your bridge version", "接続プラグインのバージョンを確認してください" },
            new[] { "봉고캣 연결이 끊겼어요 · 기록은 남아 있어요", "Disconnected from Bongo Cat · Records are kept", "Bongo Catとの接続が切れました · 記録は保持されています" },
            new[] { "연결 오류: ", "Connection error: ", "接続エラー: " },
            new[] { "플러그인을 확인해 주세요", "Check the bridge", "接続プラグインを確認してください" },
            new[] { "봉고캣 실행 중 · 채팅방에 들어가 주세요", "Bongo Cat is running · Join a room", "Bongo Catは起動中です · ルームに参加してください" },
            new[] { "● 연결됨 · 봉고캣 방의 대화를 받고 있어요", "● Connected · Receiving your room's chat", "● 接続済み · ルームの会話を受信中" },
            new[] { "기록을 읽지 못했어요: ", "Could not read records: ", "記録を読み込めませんでした: " },
            new[] { "미리보기 · 아래 대화는 화면 확인용 예시입니다", "Preview · The messages below are examples", "プレビュー · 以下は表示確認用のサンプルです" },
            new[] { "화면 미리보기 · 실제 메시지는 전송하지 않아요", "Preview · No real messages will be sent", "プレビュー · 実際のメッセージは送信しません" },
            new[] { "게임에 전송 요청 중…", "Sending request to the game…", "ゲームに送信をリクエスト中…" },
            new[] { "채팅방 연결과 최신 연결 플러그인이 준비되면 전송할 수 있어요", "Join a room with an up-to-date bridge to send messages", "最新の接続プラグインでルームに参加すると送信できます" },
            new[] { "Enter 전송 · Shift+Enter 줄바꿈 · {0}/{1}", "Enter to send · Shift+Enter for a new line · {0}/{1}", "Enterで送信 · Shift+Enterで改行 · {0}/{1}" },
            new[] { "전송 요청을 만들지 못했어요: ", "Could not create a send request: ", "送信リクエストを作成できませんでした: " },
            new[] { "게임에 전달했어요 · 수신한 메시지가 대화에 표시돼요", "Sent to the game · Received messages appear in chat", "ゲームに渡しました · 受信したメッセージが表示されます" },
            new[] { "게임에서 전송 요청을 처리하지 못했어요.", "The game could not process the send request.", "ゲームが送信リクエストを処理できませんでした。" },
            new[] { "게임 또는 방이 바뀌어 전송을 취소했어요. 입력한 글은 남겨 뒀어요.", "Game or room changed. Send cancelled; your draft is kept.", "ゲームまたはルームが変わったため送信を中止しました。下書きは保持されています。" },
            new[] { "전송 결과를 확인하지 못했어요. 대화에 표시됐는지 확인한 뒤 다시 보내 주세요.", "No send result received. Check the chat before trying again.", "送信結果を確認できませんでした。会話に表示されたか確認してから再送してください。" },
            new[] { "연결을 다시 확인하고 있어요. 입력한 글과 기록은 유지돼요.", "Checking the connection. Draft and records are kept.", "接続を再確認しています。下書きと記録は保持されます。" },
            new[] { "다시 연결됐어요. 입력한 글을 확인한 뒤 전송하세요.", "Reconnected. Review your draft before sending.", "再接続しました。下書きを確認してから送信してください。" },
            new[] { "연결되지 않았어요. 봉고캣을 종료한 뒤 Steam 라이브러리에서 다시 실행해 주세요.", "Could not connect. Close Bongo Cat and restart it from your Steam library.", "接続できませんでした。Bongo Catを終了し、Steamライブラリから再起動してください。" },
            new[] { "연결 신호가 없어요 · 봉고캣을 Steam에서 다시 실행하고 방에 들어가 주세요. 계속 안 되면 Steam도 완전히 종료 후 다시 실행해 주세요.", "No connection signal. Restart Bongo Cat from Steam and join a room. If it still fails, fully exit and restart Steam too.", "接続信号がありません。SteamからBongo Catを再起動し、ルームに参加してください。改善しない場合はSteamも完全に終了して再起動してください。" },
            new[] { "항상 위는 적용됐지만 설정 파일을 저장하지 못했어요.", "Always-on-top applied, but the preference could not be saved.", "最前面表示は適用されましたが、設定を保存できませんでした。" },
            new[] { "이모티콘 표시는 적용됐지만 설정 파일을 저장하지 못했어요.", "Emote visibility applied, but the preference could not be saved.", "絵文字の表示設定は適用されましたが、保存できませんでした。" },
            new[] { "테마는 적용됐지만 설정 파일을 저장하지 못했어요.", "Theme applied, but the preference could not be saved.", "テーマは適用されましたが、設定を保存できませんでした。" },
            new[] { "언어는 적용됐지만 설정 파일을 저장하지 못했어요.", "Language applied, but the preference could not be saved.", "言語は適用されましたが、設定を保存できませんでした。" },
            new[] { "미리보기 데이터 · {0}개 표시 / 최근 {1}개", "Preview · Showing {0} / {1} recent messages", "サンプル · {0}件表示 / 最近{1}件" },
            new[] { "내 PC에 기록 · {0}개 표시 / 최근 {1}개", "Saved locally · Showing {0} / {1} recent messages", "PCに記録 · {0}件表示 / 最近{1}件" },
            new[] { "이모티콘을 숨기고 있어요", "Emotes are hidden", "絵文字を非表示にしています" },
            new[] { "봉고캣 대화가 여기에 모여요", "Your Bongo Cat chat appears here", "Bongo Catの会話がここに集まります" },
            new[] { "검색 결과가 없어요", "No results", "検索結果がありません" },
            new[] { "일반 채팅은 계속 표시돼요.\n상단 이모티콘 표시를 켜면 다시 볼 수 있어요.", "Text chat is still visible.\nTurn on Show emotes to see them again.", "通常のチャットは引き続き表示されます。\n「絵文字を表示」をオンにすると再表示されます。" },
            new[] { "방에 들어가 채팅을 보내보세요.\n연결 후 받은 대화부터 기록됩니다.", "Join a room and send a message.\nOnly messages received after connecting are recorded.", "ルームに参加してメッセージを送ってみましょう。\n接続後に受信した会話から記録されます。" },
            new[] { "다른 메시지나 닉네임을 찾아보세요.", "Try a different message or name.", "別のメッセージや名前で検索してください。" },
            new[] { "이모티콘 · ", "Emote · ", "絵文字 · " },
            new[] { "이모티콘 #", "Emote #", "絵文字 #" },
            new[] { "고양이", "Cat", "ねこ" },
            new[] { "저장할 대화가 아직 없어요.", "There are no messages to save yet.", "保存する会話がまだありません。" },
            new[] { "현재 방의 최근 대화 저장", "Save recent messages from this room", "現在のルームの最近の会話を保存" },
            new[] { "텍스트 파일|*.txt", "Text files|*.txt", "テキストファイル|*.txt" },
            new[] { "[미리보기 데이터]", "[Preview data]", "[サンプルデータ]" },
            new[] { "최근 {0}개 메시지", "{0} recent messages", "最近{0}件のメッセージ" },
            new[] { " (나)", " (me)", " (自分)" },
            new[] { "[이모티콘 #", "[Emote #", "[絵文字 #" },
            new[] { "대화를 저장했어요", "Chat saved", "会話を保存しました" },
            new[] { "대화를 저장하지 못했어요", "Could not save chat", "会話を保存できませんでした" },
            new[] { "봉고캣 설치 폴더의 BongoCat.exe를 선택하세요", "Select BongoCat.exe in the game folder", "インストール先のBongoCat.exeを選択してください" },
            new[] { "폴더 설정을 저장하지 못했어요", "Could not save the game folder", "フォルダー設定を保存できませんでした" },
            new[] { "메일 앱을 열지 못했어요. 아래 주소로 문의해 주세요.\n\nparabellum0208@gmail.com", "Could not open your mail app. Contact:\n\nparabellum0208@gmail.com", "メールアプリを開けませんでした。お問い合わせ先:\n\nparabellum0208@gmail.com" },
            new[] { "Bongo Cat Chat Merger 문의", "Bongo Cat Chat Merger contact", "Bongo Cat Chat Merger お問い合わせ" },
            new[] { "Bongo Cat Chat Merger 오류", "Bongo Cat Chat Merger error", "Bongo Cat Chat Merger エラー" },
            new[] { "잘못된 전송 요청입니다.", "Invalid send request.", "無効な送信リクエストです。" },
            new[] { "지원하지 않는 요청입니다.", "Unsupported request.", "対応していないリクエストです。" },
            new[] { "게임이 다시 실행되어 전송을 취소했어요.", "Send cancelled because the game restarted.", "ゲームが再起動したため送信を中止しました。" },
            new[] { "채팅방이 바뀌어 전송을 취소했어요.", "Send cancelled because the room changed.", "ルームが変わったため送信を中止しました。" },
            new[] { "전송 요청 시간이 올바르지 않아요.", "Invalid request timestamp.", "送信リクエストの時刻が無効です。" },
            new[] { "전송 시간이 지나 취소했어요. 다시 보내 주세요.", "Send request expired. Please try again.", "送信期限が切れました。もう一度送信してください。" },
            new[] { "메시지를 입력해 주세요.", "Enter a message.", "メッセージを入力してください。" },
            new[] { "메시지가 너무 길어요.", "Message is too long.", "メッセージが長すぎます。" },
            new[] { "지원하지 않는 제어 문자가 있어요.", "Message contains unsupported control characters.", "対応していない制御文字が含まれています。" },
            new[] { "메시지 용량이 너무 커요.", "Message is too large.", "メッセージのサイズが大きすぎます。" },
            new[] { "전송 요청 ID가 올바르지 않아요.", "Invalid send request ID.", "送信リクエストIDが無効です。" },
            new[] { "이미 처리한 전송 요청입니다.", "This send request was already processed.", "この送信リクエストは処理済みです。" },
            new[] { "게임의 채팅 입력이 준비되지 않았어요.", "The game's chat input is not ready.", "ゲームのチャット入力の準備ができていません。" },
            new[] { "잠시 후 다시 보내 주세요.", "Please wait before sending again.", "少し待ってから再送してください。" },
            new[] { "게임의 채팅 필터가 전송을 허용하지 않았어요.", "The game's chat filter did not allow this message.", "ゲームのチャットフィルターが送信を許可しませんでした。" },
            new[] { "게임에 전송하지 못했어요: ", "Could not send to the game: ", "ゲームに送信できませんでした: " }
        };
        private static readonly Dictionary<string, string[]> Catalog = CreateCatalog();
        private static Dictionary<string, string[]> CreateCatalog()
        {
            var result = new Dictionary<string, string[]>();
            foreach (string[] row in Rows) result.Add(row[0], row);
            return result;
        }
        public static string Get(string code, string text)
        {
            if (text == null) return "";
            if (code == "ko") return text;
            string[] row; int index = code == "ja" ? 2 : 1;
            if (Catalog.TryGetValue(text, out row)) return row[index];
            // Only known UI prefixes are translated; user messages and room names never pass here.
            foreach (string[] prefix in Rows)
                if ((prefix[0].EndsWith(": ") || prefix[0] == "이모티콘 #") && text.StartsWith(prefix[0], StringComparison.Ordinal))
                    return prefix[index] + Get(code, text.Substring(prefix[0].Length));
            return text;
        }
    }

    public sealed class LanguagePicker : Window
    {
        public readonly ComboBox Languages = new ComboBox { MinHeight = 34, Margin = new Thickness(0, 18, 0, 18) };
        public readonly Button ContinueButton = new Button { MinHeight = 36, IsDefault = true };
        private readonly TextBlock error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
        public string SelectedLanguage { get { return ((ComboBoxItem)Languages.SelectedItem).Tag as string; } }
        public LanguagePicker()
        {
            Title = "Bongo Cat Chat Merger"; Width = 420; SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Yu Gothic UI, Malgun Gothic");
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "Choose your language\n언어 선택 / 言語を選択", FontSize = 19, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Black });
            Populate(Languages, "en");
            Languages.SelectionChanged += delegate { ContinueButton.Content = SelectedLanguage == "ko" ? "시작하기" : SelectedLanguage == "ja" ? "はじめる" : "Continue"; };
            ContinueButton.Content = "Continue";
            ContinueButton.Click += delegate
            {
                try { UiText.SavePreference(SelectedLanguage); DialogResult = true; }
                catch (IOException) { error.Text = SaveError(); }
                catch (UnauthorizedAccessException) { error.Text = SaveError(); }
            };
            panel.Children.Add(Languages); panel.Children.Add(ContinueButton); panel.Children.Add(error);
            Content = new Border { Background = System.Windows.Media.Brushes.White, Padding = new Thickness(26), Child = panel };
        }
        private string SaveError()
        {
            return SelectedLanguage == "ko" ? "언어 설정을 저장하지 못했어요. 앱을 쓰기 가능한 폴더로 옮긴 뒤 다시 실행해 주세요." :
                SelectedLanguage == "ja" ? "言語設定を保存できませんでした。書き込み可能なフォルダーにアプリを移動して再起動してください。" :
                "Could not save your language. Move the app to a writable folder and restart it.";
        }
        public static void Populate(ComboBox box, string selected)
        {
            string[] codes = { "en", "ko", "ja" }, labels = { "English", "한국어", "日本語" };
            for (int i = 0; i < codes.Length; i++) { var item = new ComboBoxItem { Content = labels[i], Tag = codes[i] }; box.Items.Add(item); if (codes[i] == selected) box.SelectedItem = item; }
        }
    }

    public sealed partial class ChatWindow
    {
        private string language, statusMessage = "연결 플러그인을 기다리고 있어요";
        private ComboBox languageBox;
        private string L(string text) { return UiText.Get(language, text); }
        private string F(string text, params object[] values) { return String.Format(UiText.Culture(language), L(text), values); }
        private void InitializeLanguage(Grid root)
        {
            languageBox = (ComboBox)root.FindName("Language");
            LanguagePicker.Populate(languageBox, language);
            languageBox.SelectionChanged += delegate
            {
                string selected = ((ComboBoxItem)languageBox.SelectedItem).Tag as string;
                if (selected == language) return;
                language = selected; ApplyLanguage();
                try { UiText.SavePreference(language); }
                catch (IOException) { sendNotice = "언어는 적용됐지만 설정 파일을 저장하지 못했어요."; RefreshComposer(); }
                catch (UnauthorizedAccessException) { sendNotice = "언어는 적용됐지만 설정 파일을 저장하지 못했어요."; RefreshComposer(); }
            };
            ApplyLanguage();
        }
        private void ApplyLanguage()
        {
            foreach (string[] item in new[] { new[] { "DarkMode", "다크 모드" }, new[] { "Pin", "항상 위" }, new[] { "ShowEmotes", "이모티콘 표시" }, new[] { "Send", "전송" }, new[] { "Choose", "게임 폴더" }, new[] { "Export", "대화 저장" } })
                ((ContentControl)rootGrid.FindName(item[0])).Content = L(item[1]);
            ((TextBlock)rootGrid.FindName("SearchLabel")).Text = L("검색");
            ((TextBlock)rootGrid.FindName("OpacityLabel")).Text = L("투명도");
            ((Run)rootGrid.FindName("ContactLabel")).Text = L("문의 · ");
            ((Hyperlink)rootGrid.FindName("ContactEmail")).ToolTip = L("기본 메일 앱에서 문의 메일 작성");
            ((CheckBox)rootGrid.FindName("ShowEmotes")).ToolTip = L("끄면 이모티콘을 대화 목록과 검색에서 숨깁니다. 기록은 보존됩니다.");
            search.ToolTip = L("메시지 또는 닉네임 검색"); composer.ToolTip = L("Enter로 전송 · Shift+Enter 줄바꿈");
            reconnectButton.Content = L(reconnectTimer.IsEnabled ? "확인 중…" : "재연결");
            reconnectButton.ToolTip = L("게임 폴더와 연결 상태를 다시 확인합니다.");
            languageBox.ToolTip = "Language / 언어 / 言語";
            System.Windows.Automation.AutomationProperties.SetName(languageBox, "Language / 언어 / 言語");
            statusText.Text = L(statusMessage);
            if (demo) roomInfo.Text = L("참여 중 4명 · 화면 미리보기");
            else
            {
                roomTitle.Text = liveState != null && Journal.RoomIdValid(currentRoom) && !String.IsNullOrWhiteSpace(liveState.name) ? liveState.name : L("봉고캣 채팅방");
                roomInfo.Text = Journal.RoomIdValid(currentRoom) && liveState != null ? F("참여 중 {0}명 · 실시간 대화", liveState.count) : L("같은 방의 대화를 한곳에");
            }
            RenderMessages(false); RefreshComposer();
        }
    }
}
