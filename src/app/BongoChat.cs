using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.5")]
[assembly: System.Reflection.AssemblyTitle("Bongo Cat Chat Merger")]
[assembly: System.Reflection.AssemblyProduct("Bongo Cat Chat Merger")]
[assembly: System.Reflection.AssemblyVersion("0.4.7.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.4.7.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("0.4.7")]

namespace BongoChat
{
    public sealed class ChatEntry
    {
        public string kind { get; set; }
        public string id { get; set; }
        public string room { get; set; }
        public string sender { get; set; }
        public string session { get; set; }
        public string delivery { get; set; }
        public string name { get; set; }
        public bool mine { get; set; }
        public string text { get; set; }
        public string utc { get; set; }
        public int emoteId { get; set; }
        public long meowCount { get; set; }
        public string meowLastUtc { get; set; }
        public bool meowMoreAuthors { get; set; }
        public List<MeowAuthor> meowAuthors { get; set; }
    }
    public sealed class BridgeState
    {
        public int protocol { get; set; }
        public string utc { get; set; }
        public bool ready { get; set; }
        public bool stopped { get; set; }
        public string room { get; set; }
        public string name { get; set; }
        public string code { get; set; }
        public int count { get; set; }
        public string error { get; set; }
        public string version { get; set; }
        public string session { get; set; }
        public bool canSend { get; set; }
        public int maxMessageLength { get; set; }
        public int assetRevision { get; set; }
    }

    public static class Journal
    {
        public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1048576 };
        public static string ReadShared(string path, int maxBytes = 1048576)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length > maxBytes) throw new InvalidDataException("Bridge file exceeds its size limit.");
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    char[] buffer = new char[maxBytes + 1];
                    int count = 0, read;
                    while (count < buffer.Length && (read = reader.Read(buffer, count, buffer.Length - count)) > 0) count += read;
                    if (count > maxBytes) throw new InvalidDataException("Bridge file exceeds its size limit.");
                    return new string(buffer, 0, count);
                }
            }
        }
        public static List<ChatEntry> ReadRecent(string path, int limit)
        {
            var list = new List<ChatEntry>();
            var readerJson = new JavaScriptSerializer { MaxJsonLength = 1048576 };
            if (!File.Exists(path)) return list;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                const long tailBytes = 2 * 1024 * 1024;
                bool tail = stream.Length > tailBytes;
                if (tail) stream.Seek(-tailBytes, SeekOrigin.End);
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    if (tail) reader.ReadLine();
                    string line;
                    var seen = new HashSet<string>();
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            var entry = readerJson.Deserialize<ChatEntry>(line);
                            if (entry != null && (entry.kind == "chat" || (entry.kind == "emote" && entry.emoteId > 0)) && !String.IsNullOrEmpty(entry.id)
                                && entry.text != null && seen.Add(entry.id)) list.Add(entry);
                        }
                        catch (ArgumentException) { }
                        catch (InvalidOperationException) { }
                    }
                }
            }
            return list.Skip(Math.Max(0, list.Count - limit)).ToList();
        }
        public static bool RoomIdValid(string room)
        {
            ulong value;
            return !String.IsNullOrEmpty(room) && ulong.TryParse(room, out value) && value != 0;
        }
    }

    public sealed partial class ChatWindow : Window
    {
        private readonly JavaScriptSerializer json = Journal.Json;
        private readonly string settingsPath = AppStorage.PathFor("game-path.txt");
        private readonly bool demo;
        private string gamePath = "";
        private string currentRoom = "";
        private string currentFile = "";
        private long currentLength = -1;
        private long currentWriteTicks = -1;
        private List<ChatEntry> entries = new List<ChatEntry>();
        private StackPanel messages;
        private ScrollViewer scroll;
        private TextBlock roomTitle, roomInfo, statusText, resultText;
        private Border status;
        private TextBox search;
        private DispatcherTimer timer;
        private string lastError = "";

        private const string Layout = @"
<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='#BACDDC'>
  <Grid.Resources>
    <Style TargetType='ComboBoxItem'>
      <Setter Property='Background' Value='#FFFFFF'/><Setter Property='Foreground' Value='#293B46'/><Setter Property='Padding' Value='10,6'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBoxItem'><Border x:Name='ItemBorder' Background='{TemplateBinding Background}' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsHighlighted' Value='True'><Setter TargetName='ItemBorder' Property='Background' Value='#A7BFCE'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='ItemBorder' Property='Background' Value='#D3E0E9'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType='ComboBox'>
      <Setter Property='Background' Value='#FFFFFF'/><Setter Property='Foreground' Value='#293B46'/><Setter Property='BorderBrush' Value='#DAE4E9'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'>
        <Grid><ToggleButton Focusable='False' Foreground='#293B46' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
          <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border Background='#FFFFFF' BorderBrush='#DAE4E9' BorderThickness='1' CornerRadius='5'><Grid><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='22'/></Grid.ColumnDefinitions><ContentPresenter Content='{Binding SelectionBoxItem, RelativeSource={RelativeSource AncestorType=ComboBox}}' Margin='9,4,0,4' VerticalAlignment='Center'/><TextBlock Grid.Column='1' Text='▾' VerticalAlignment='Center' HorizontalAlignment='Center'/></Grid></Border></ControlTemplate></ToggleButton.Template>
        </ToggleButton><Popup x:Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False'><Border Background='#FFFFFF' BorderBrush='#DAE4E9' BorderThickness='1' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'><ScrollViewer CanContentScroll='True'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer></Border></Popup></Grid>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType='Button'>
      <Setter Property='Cursor' Value='Hand'/><Setter Property='Padding' Value='12,8'/>
      <Setter Property='Background' Value='#FFFFFF'/><Setter Property='Foreground' Value='#394754'/>
      <Setter Property='BorderBrush' Value='#E3E9ED'/><Setter Property='BorderThickness' Value='1'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'>
        <Border Background='{TemplateBinding Background}' CornerRadius='8' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'>
          <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
        </Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.8'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType='ScrollBar'>
      <Setter Property='Width' Value='10'/><Setter Property='Background' Value='#BACDDC'/>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'>
        <Grid Background='{TemplateBinding Background}'>
          <Track x:Name='PART_Track' Orientation='{TemplateBinding Orientation}' IsDirectionReversed='True' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}'>
            <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton>
            <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='#718795' CornerRadius='4' Margin='2' Opacity='0.6'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
            <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton>
          </Track>
        </Grid>
      </ControlTemplate></Setter.Value></Setter>
    </Style>
  </Grid.Resources>
  <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='*'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>
  <Border Grid.Row='0' Background='#FCFCF8' Padding='22,18,22,16' BorderBrush='#DEE5E8' BorderThickness='0,0,0,1'>
    <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
      <Border Width='46' Height='46' Background='#FFE36B' CornerRadius='15' Margin='0,0,13,0'>
        <TextBlock Text='냥' Foreground='#41391C' FontSize='22' FontWeight='Bold' VerticalAlignment='Center' HorizontalAlignment='Center'/>
      </Border>
      <StackPanel Grid.Column='1' VerticalAlignment='Center'><TextBlock Text='BONGO CAT CHAT MERGER' FontSize='9' FontWeight='Bold' Foreground='#8B918D' Margin='0,0,0,4' TextTrimming='CharacterEllipsis' ToolTip='Bongo Cat Chat Merger'/>
        <TextBlock x:Name='RoomTitle' Text='봉고캣 채팅방' FontSize='18' FontWeight='SemiBold' Foreground='#263A46' TextTrimming='CharacterEllipsis'/>
        <TextBlock x:Name='RoomInfo' Text='같은 방의 대화를 한곳에' Foreground='#84909A' FontSize='11' Margin='0,5,0,0'/>
      </StackPanel>
      <StackPanel Grid.Column='2' VerticalAlignment='Center' Margin='14,0,0,0'>
        <CheckBox x:Name='DarkMode' Content='다크 모드' Foreground='#667682' Margin='0,0,0,10'/>
        <CheckBox x:Name='Pin' Content='항상 위' Foreground='#667682' Margin='0,0,0,10'/>
        <CheckBox x:Name='ShowEmotes' Content='이모티콘 표시' Foreground='#667682' ToolTip='끄면 이모티콘을 대화 목록과 검색에서 숨깁니다. 기록은 보존됩니다.'/>
        <CheckBox x:Name='HideMeows' Content='야옹 메시지 숨기기' Foreground='#667682' Margin='0,10,0,0' ToolTip='게임의 언어별 야옹 문구와 메시지 전체가 같을 때만 숨깁니다. 원본 기록은 보존됩니다.'/>
        <ComboBox x:Name='Language' Width='120' FontSize='11' Margin='0,9,0,0' HorizontalAlignment='Left'/>
      </StackPanel>
    </Grid>
  </Border>
  <Border x:Name='Status' Grid.Row='1' Background='#FFF3CA' Padding='20,11'>
    <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
      <TextBlock x:Name='StatusText' Text='연결 플러그인을 기다리고 있어요' FontSize='12' Foreground='#775D25' TextWrapping='Wrap' VerticalAlignment='Center' Margin='0,0,10,0'/>
      <Button x:Name='Reconnect' Grid.Column='1' Content='재연결' Padding='10,6' FontSize='11' ToolTip='게임 폴더와 연결 상태를 다시 확인합니다.'/>
    </Grid>
  </Border>
  <Grid Grid.Row='2' Margin='20,14,20,4'>
    <Border Background='#D3E0E9' CornerRadius='9' Padding='12,9'>
      <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
        <TextBlock x:Name='SearchLabel' Text='검색' FontSize='12' Foreground='#677E8E' VerticalAlignment='Center' Margin='0,0,12,0'/>
        <TextBox x:Name='Search' Grid.Column='1' BorderThickness='0' Background='Transparent' Foreground='#314A5A' FontSize='12' ToolTip='메시지 또는 닉네임 검색'/>
      </Grid>
    </Border>
  </Grid>
  <ScrollViewer x:Name='Scroll' Grid.Row='3' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled' Padding='20,12,20,20'>
    <StackPanel x:Name='Messages'/>
  </ScrollViewer>
  <Border Grid.Row='4' Background='#FCFCF8' Padding='20,15,20,16' BorderBrush='#DAE4E9' BorderThickness='0,1,0,0'>
    <StackPanel>
      <Grid Margin='0,0,0,8'><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
        <TextBox x:Name='Composer' AcceptsReturn='True' TextWrapping='Wrap' MinHeight='46' MaxHeight='94' MaxLength='300' VerticalScrollBarVisibility='Auto' Padding='10,9' Background='#FFFFFF' Foreground='#293B46' BorderBrush='#DAE4E9' FontSize='13' ToolTip='Enter로 전송 · Shift+Enter 줄바꿈'/>
        <Button x:Name='Send' Grid.Column='1' Content='전송' Margin='8,0,0,0' Width='62' IsEnabled='False' Background='#FFE36B' Foreground='#293B46'/>
      </Grid>
      <Button x:Name='RecoverSend' Content='이전 전송문 복원' Visibility='Collapsed' HorizontalAlignment='Left' FontSize='10' Padding='8,4' Margin='0,0,0,6'/>
      <TextBlock x:Name='ComposerHint' Text='연결 후 이곳에서 채팅을 보낼 수 있어요' FontSize='10' Foreground='#526675' Margin='0,0,0,12' TextWrapping='Wrap'/>
      <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
        <Button x:Name='Choose' Content='게임 폴더' FontSize='11' Margin='0,0,7,0'/>
        <Button x:Name='Export' Grid.Column='1' Content='대화 저장' FontSize='11'/>
        <StackPanel Grid.Column='3' Orientation='Horizontal' VerticalAlignment='Center' Margin='12,0,0,0'>
          <TextBlock x:Name='OpacityLabel' Text='투명도' FontSize='10' Foreground='#91A0A8' VerticalAlignment='Center' Margin='0,0,7,0'/>
          <Slider x:Name='OpacityControl' Minimum='0.4' Maximum='1' Value='1' Width='62' VerticalAlignment='Center'/>
        </StackPanel>
      </Grid>
      <TextBlock x:Name='ResultText' Text='내 PC에 기록 · 최근 500개 표시' FontSize='10' Foreground='#9AA5AC' Margin='0,12,0,0'/>
      <Grid Margin='0,7,0,0'><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
        <TextBlock FontSize='10' Foreground='#718795' TextWrapping='Wrap' VerticalAlignment='Center'><Run x:Name='ContactLabel' Text='문의 · '/><Hyperlink x:Name='ContactEmail' NavigateUri='mailto:parabellum0208@gmail.com' Foreground='#718795' ToolTip='기본 메일 앱에서 문의 메일 작성'>parabellum0208@gmail.com</Hyperlink></TextBlock>
        <Button x:Name='CheckUpdate' Grid.Column='1' Content='업데이트 확인' FontSize='10' Padding='8,4' Margin='8,0,0,0'/>
      </Grid>
      <Border x:Name='UpdateOffer' Background='#D3E0E9' CornerRadius='6' Padding='8' Margin='0,6,0,0' Visibility='Collapsed'>
        <Grid><Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>
          <TextBlock x:Name='UpdateText' FontSize='10' Foreground='#506471' TextWrapping='Wrap' VerticalAlignment='Center' Margin='0,0,8,0'/>
          <Button x:Name='OpenRelease' Grid.Column='1' Content='다운로드 페이지' FontSize='10' Padding='8,5'/>
        </Grid>
      </Border>
    </StackPanel>
  </Border>
</Grid>";

        public ChatWindow(bool demonstration, string folderOverride = null, bool? darkOverride = null, string languageOverride = null, bool backgroundCommunication = false)
        {
            demo = demonstration;
            language = UiText.IsLanguage(languageOverride) ? languageOverride : UiText.ReadPreference() ?? "en";
            Title = "Bongo Cat Chat Merger " + Program.DisplayVersion;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.CanResizeWithGrip;
            Width = 510; Height = 790; MinWidth = 420; MinHeight = 560;
            FontFamily = new FontFamily("Malgun Gothic");
            FontSize = 13;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dark = darkOverride ?? ReadDarkPreference();
            showEmotes = ReadEmotePreference();
            hideMeows = ReadMeowPreference();
            var root = (Grid)System.Windows.Markup.XamlReader.Parse(ThemedLayout());
            Content = root;
            roomTitle = (TextBlock)root.FindName("RoomTitle"); roomInfo = (TextBlock)root.FindName("RoomInfo");
            status = (Border)root.FindName("Status"); statusText = (TextBlock)root.FindName("StatusText");
            resultText = (TextBlock)root.FindName("ResultText");
            messages = (StackPanel)root.FindName("Messages"); scroll = (ScrollViewer)root.FindName("Scroll");
            search = (TextBox)root.FindName("Search");
            search.TextChanged += delegate { RenderMessages(false); };
            SizeChanged += delegate { if (entries.Count > 0) RenderMessages(false); };
            var pin = (CheckBox)root.FindName("Pin");
            Topmost = ReadPinPreference(); pin.IsChecked = Topmost;
            pin.Checked += delegate { Topmost = true; SavePinPreference(true); };
            pin.Unchecked += delegate { Topmost = false; SavePinPreference(false); };
            ((Slider)root.FindName("OpacityControl")).ValueChanged += delegate(object sender, RoutedPropertyChangedEventArgs<double> args) { SetWindowOpacity(args.NewValue); };

            ((Button)root.FindName("Export")).Click += delegate { Export(); };
            ((Hyperlink)root.FindName("ContactEmail")).Click += delegate
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("mailto:parabellum0208@gmail.com") { UseShellExecute = true }); }
                catch (System.ComponentModel.Win32Exception) { ShowContactAddress(); }
                catch (InvalidOperationException) { ShowContactAddress(); }
            };
            InitializeComposerAndTheme(root);
            InitializeReconnect(root);
            InitializeSetup(root);
            InitializeTransparentFrame(root);
            InitializeUpdates(root);
            InitializeLanguage(root);
            if (demo) LoadDemo();
            else
            {
                if (folderOverride != null) gamePath = folderOverride;
                else FindGame();
                connectionPaused = BridgeSetup.IsConnectionPaused(gamePath);
                if (backgroundCommunication) EnableBackgroundCommunication();
                timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
                timer.Tick += delegate { Poll(); };
                timer.Start();
                Poll();
            }
            Closed += delegate { if (timer != null) timer.Stop(); if (reconnectTimer != null) reconnectTimer.Stop(); };
        }

        private SolidColorBrush Brush(string color) { return RawBrush(ThemeColor(color)); }
        private void ShowContactAddress()
        {
            MessageBox.Show(this, L("메일 앱을 열지 못했어요. 아래 주소로 문의해 주세요.\n\nparabellum0208@gmail.com"), L("Bongo Cat Chat Merger 문의"));
        }
        private string BridgeFolder { get { return Path.Combine(gamePath, "BepInEx", "BongoChat"); } }
        private void FindGame()
        {
            string preferred = gamePath;
            try { if (File.Exists(settingsPath)) preferred = File.ReadAllText(settingsPath).Trim(); } catch { }
            gamePath = GameLocator.Find(preferred);
        }
        private void ChooseGame()
        {
            var picker = new OpenFileDialog { Title = L("봉고캣 설치 폴더의 BongoCat.exe를 선택하세요"), Filter = "Bongo Cat|BongoCat.exe" };
            if (picker.ShowDialog(this) != true) return;
            gamePath = Path.GetDirectoryName(picker.FileName);
            try { File.WriteAllText(settingsPath, gamePath, new UTF8Encoding(false)); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L("폴더 설정을 저장하지 못했어요")); }
            currentRoom = ""; currentFile = ""; currentLength = -1; entries.Clear(); receivedEntries.Clear();
            RenderMessages(true);
            if (!demo) Poll();
        }

        private void SetStatus(string message, bool connected)
        {
            statusConnected = connected;
            statusMessage = message;
            statusText.Text = L(message);
            status.Background = Brush(connected ? "#E2EFEA" : "#FFF3CA");
            statusText.Foreground = Brush(connected ? "#38725A" : "#775D25");
            if (connected && reconnectTimer != null && reconnectTimer.IsEnabled) FinishReconnect(true);
        }

        public void Poll()
        {
            if (connectionPaused) { canSend = false; SetStatus(PausedNotice,false); RefreshComposer(); return; }
            if (setupBusy) { RefreshComposer(); return; }
            ExpirePendingRequest();
            if (setupLaunchDeadline != default(DateTime) && DateTime.UtcNow > setupLaunchDeadline)
            {
                setupLaunchDeadline = default(DateTime);
                SetupMessage("게임에서 연결 파일을 불러오지 못했어요. 연결 도우미에서 다시 확인할 수 있어요.", "retry");
            }
            if (backgroundIo) ReadPollInBackground();
            else ApplyPollSnapshot(ReadPollSnapshot(CapturePollRead()));
        }

        private void ApplyPollSnapshot(PollSnapshot snapshot)
        {
            if (connectionPaused) { canSend = false; SetStatus(PausedNotice,false); RefreshComposer(); return; }
            if (setupBusy) { RefreshComposer(); return; }
            UiPhase("apply-state");
            canSend = false;
            liveState = null;
            try
            {
                if (String.IsNullOrEmpty(gamePath)) { SetStatus(setupNotice ?? "게임 폴더에서 BongoCat.exe를 선택해 주세요", false); RenderEmptyIfNeeded(); return; }
                if (snapshot.ReadError != null) { SetStatus("기록을 읽지 못했어요: " + snapshot.ReadError, false); return; }
                if (!snapshot.StateExists) { SetStatus(setupNotice ?? "연결 플러그인 설치 후 봉고캣을 실행해 주세요", false); RenderEmptyIfNeeded(); return; }
                BridgeState state = snapshot.State;
                liveState = state;
                if (state == null || state.protocol != 1) { SetStatus("연결 플러그인 버전을 확인해 주세요", false); return; }
                CheckPending(state, snapshot.Result);
                DateTime timestamp;
                bool fresh = DateTime.TryParse(state.utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out timestamp)
                    && Math.Abs((DateTime.UtcNow - timestamp.ToUniversalTime()).TotalSeconds) < 6;
                if (!fresh || state.stopped) { SetStatus(setupNotice ?? "봉고캣 연결이 끊겼어요 · 기록은 남아 있어요", false); return; }
                if (!state.ready) { SetStatus("연결 오류: " + (state.error ?? "플러그인을 확인해 주세요"), false); return; }
                if (setupNotice != null) { setupNotice = null; setupLaunchDeadline = default(DateTime); setupAction = "retry"; RefreshSetupLabels(); }
                if (!Journal.RoomIdValid(state.room))
                {
                    if (currentRoom != "") { currentRoom = ""; entries.Clear(); receivedEntries.Clear(); currentFile = ""; currentLength = -1; RenderMessages(true); }
                    roomTitle.Text = L("봉고캣 채팅방"); roomInfo.Text = L("같은 방의 대화를 한곳에");
                    SetStatus("봉고캣 실행 중 · 채팅방에 들어가 주세요", false); RenderEmptyIfNeeded(); return;
                }
                SetStatus("● 연결됨 · 봉고캣 방의 대화를 받고 있어요", true);
                canSend = state.canSend && !String.IsNullOrEmpty(state.session);
                UpdateComposerLimit(state.maxMessageLength > 0 ? Math.Min(300, state.maxMessageLength) : 300);
                roomTitle.Text = String.IsNullOrWhiteSpace(state.name) ? L("봉고캣 채팅방") : state.name;
                roomInfo.Text = F("참여 중 {0}명 · 실시간 대화", state.count);
                if (currentRoom != state.room)
                {
                    currentRoom = state.room;
                    currentFile = Path.Combine(BridgeFolder, "room_" + currentRoom + ".jsonl");
                    currentLength = -1; entries.Clear(); receivedEntries.Clear();
                }
                if (snapshot.Entries != null)
                {
                    receivedEntries = snapshot.Entries.Where(e => e.room == currentRoom).ToList();
                    currentLength = snapshot.Length; currentWriteTicks = snapshot.WriteTicks; currentAssetRevision = state.assetRevision;
                    UiPhase("render-messages");
                }
                MergeOwnSends(); RenderMessages(false);
                lastError = "";
            }
            catch (Exception ex)
            {
                if (lastError != ex.GetType().Name) { SetStatus("기록을 읽지 못했어요: " + ex.GetType().Name, false); lastError = ex.GetType().Name; }
            }
            finally { ExpirePendingRequest(); RefreshComposer(); UiPhase("idle"); }
        }

        private List<ChatEntry> renderedVisible = new List<ChatEntry>();
        private string renderedContext = "", renderedDate = "";
        private static bool SameRenderedEntry(ChatEntry a, ChatEntry b)
        {
            return a.id == b.id && a.kind == b.kind && a.utc == b.utc && a.text == b.text
                && a.name == b.name && a.mine == b.mine && a.emoteId == b.emoteId && a.delivery == b.delivery
                && a.meowCount == b.meowCount && a.meowLastUtc == b.meowLastUtc && a.meowMoreAuthors == b.meowMoreAuthors;
        }
        private void RenderEmptyIfNeeded() { if (messages.Children.Count == 0) RenderMessages(true); }
        public void RenderMessages(bool forceScroll)
        {
            if (imeComposing) { deferredRender = true; deferredScroll |= forceScroll; return; }
            bool atBottom = forceScroll || scroll.ScrollableHeight - scroll.VerticalOffset < 35;
            double offset = scroll.VerticalOffset;
            string query = search.Text.Trim();
            var visible = entries.Where(e => showEmotes || e.kind != "emote").Where(e => !hideMeows || (e.kind != "meow_group" && (e.kind != "chat" || !MeowMessages.IsMeow(e.text)))).Where(e => query.Length == 0 || (e.text ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || (e.name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            string context = language + "|" + dark + "|" + showEmotes + "|" + hideMeows + "|" + (ActualWidth > 0 ? ActualWidth : Width).ToString("R", CultureInfo.InvariantCulture) + "|" + currentAssetRevision + "|" + query;
            RefreshLastMeowGroup(visible,context);
            bool append = renderedContext == context && renderedVisible.Count > 0 && renderedVisible.Count <= visible.Count;
            if (append) for (int i = 0; i < renderedVisible.Count; i++)
                if (!SameRenderedEntry(renderedVisible[i], visible[i])) { append = false; break; }
            int first = append ? renderedVisible.Count : 0;
            if (!append) messages.Children.Clear();
            renderedVisible = visible; renderedContext = context;
            resultText.Text = F(demo ? "미리보기 데이터 · {0}개 표시 / 최근 {1}개" : "내 PC에 기록 · {0}개 표시 / 최근 {1}개", visible.Count, entries.Count);
            ShowGroupedCount(visible);
            if (visible.Count == 0)
            {
                var empty = new StackPanel { Margin = new Thickness(20, 105, 20, 50), HorizontalAlignment = HorizontalAlignment.Center };
                empty.Children.Add(new TextBlock { Text = query.Length == 0 ? "냥" : "…", FontSize = 38, Foreground = Brush("#718FA3"), HorizontalAlignment = HorizontalAlignment.Center });
                bool hiddenOnly = query.Length == 0 && entries.Count > 0;
                empty.Children.Add(new TextBlock { Text = L(query.Length == 0 ? hiddenOnly ? "표시 설정으로 메시지를 숨기고 있어요" : "봉고캣 대화가 여기에 모여요" : "검색 결과가 없어요"), FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brush("#486477"), Margin = new Thickness(0, 18, 0, 8), HorizontalAlignment = HorizontalAlignment.Center });
                empty.Children.Add(new TextBlock { Text = L(query.Length == 0 ? hiddenOnly ? "상단의 이모티콘·야옹 표시 설정을 바꾸면 다시 볼 수 있어요." : "방에 들어가 채팅을 보내보세요.\n연결 후 받은 대화부터 기록됩니다." : "다른 메시지나 닉네임을 찾아보세요."), Foreground = Brush("#718799"), FontSize = 12, TextAlignment = TextAlignment.Center, LineHeight = 22, MaxWidth = Math.Max(200, Width - 100), TextWrapping = TextWrapping.Wrap });
                messages.Children.Add(empty); return;
            }
            string previousDate = append ? renderedDate : "";
            foreach (ChatEntry entry in visible.Skip(first))
            {
                DateTime time;
                if (!DateTime.TryParse(entry.utc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out time)) time = DateTime.Now;
                time = time.ToLocalTime();
                string date = time.ToString("D", UiText.Culture(language));
                if (date != previousDate)
                {
                    var divider = new Border { Background = Brush("#A7BFCE"), CornerRadius = new CornerRadius(14), Padding = new Thickness(14, 6, 14, 6), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 20) };
                    divider.Child = new TextBlock { Text = date, Foreground = Brush("#496373"), FontSize = 10 };
                    messages.Children.Add(divider); previousDate = date;
                }
                messages.Children.Add(MessageBubble(entry, time));
            }
            renderedDate = previousDate;
            scroll.UpdateLayout();
            if (atBottom) scroll.ScrollToEnd(); else scroll.ScrollToVerticalOffset(offset);
        }

        private UIElement MessageBubble(ChatEntry entry, DateTime time)
        {
            if (entry.kind == "meow_group") return MeowGroupBubble(entry,time);
            var row = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (!entry.mine)
            {
                int hash = (entry.sender ?? "").Aggregate(0, (value, ch) => (value * 31 + ch) & 0x7fffffff);
                string[] colors = { "#F4D9C8", "#DDE5C9", "#DAD5ED", "#F7E6AD" };
                var avatar = new Border { Width = 35, Height = 35, Background = Brush(colors[hash % colors.Length]), CornerRadius = new CornerRadius(12), VerticalAlignment = VerticalAlignment.Top };
                string initial = String.IsNullOrEmpty(entry.name) ? "냥" : StringInfo.GetNextTextElement(entry.name);
                avatar.Child = new TextBlock { Text = initial, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Brush("#6D645C"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(avatar);
            }
            var content = new StackPanel { HorizontalAlignment = entry.mine ? HorizontalAlignment.Right : HorizontalAlignment.Left };
            Grid.SetColumn(content, entry.mine ? 0 : 1);
            if (entry.mine) Grid.SetColumnSpan(content, 2);
            if (!entry.mine) content.Children.Add(new TextBlock { Text = entry.name ?? L("고양이"), Foreground = Brush("#506471"), FontSize = 11, Margin = new Thickness(0, 0, 0, 6), MaxWidth = 280, TextTrimming = TextTrimming.CharacterEllipsis });
            if(entry.kind=="own_send") content.Children.Add(new TextBlock{Text=OwnSendLabel(entry),FontSize=10,Foreground=Brush(entry.delivery=="rejected"?"#775D25":"#506471"),Margin=new Thickness(0,0,0,5),TextWrapping=TextWrapping.Wrap});
            var line = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = entry.mine ? HorizontalAlignment.Right : HorizontalAlignment.Left };
            double bubbleWidth = Math.Min(275, Math.Max(140, (ActualWidth > 0 ? ActualWidth : Width) - 185));
            var bubble = new Border { Background = Brush(entry.mine ? "#FFE36B" : "#FFFFFF"), CornerRadius = new CornerRadius(3, 12, 12, 12), Padding = new Thickness(12, 10, 12, 10), MaxWidth = bubbleWidth };
            if (entry.mine) bubble.CornerRadius = new CornerRadius(12, 3, 12, 12);
            if (entry.kind == "emote") bubble.Child = EmoteContent(entry);
            else bubble.Child = SelectableMessageText(entry);

            var stamp = new TextBlock { Text = time.ToString("t", UiText.Culture(language)), FontSize = 9, Foreground = Brush("#718795"), VerticalAlignment = VerticalAlignment.Bottom, Margin = entry.mine ? new Thickness(0, 0, 6, 2) : new Thickness(6, 0, 0, 2) };
            if (entry.mine) { line.Children.Add(stamp); line.Children.Add(bubble); }
            else { line.Children.Add(bubble); line.Children.Add(stamp); }
            content.Children.Add(line); row.Children.Add(content); return row;
        }

        private void Export()
        {
            if (entries.Count == 0) { MessageBox.Show(this, L("저장할 대화가 아직 없어요."), "Bongo Cat Chat Merger"); return; }
            var save = new SaveFileDialog { Title = L("현재 방의 최근 대화 저장"), Filter = L("텍스트 파일|*.txt"), FileName = "BongoCatChatMerger-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".txt" };
            if (save.ShowDialog(this) != true) return;
            try
            {
                var text = new StringBuilder("Bongo Cat Chat Merger · " + roomTitle.Text + "\r\n");
                if (demo) text.AppendLine(L("[미리보기 데이터]"));
                text.AppendLine((entries.Any(e => e.kind == "meow_group") ? F("최근 대화 {0}개 · 야옹 별도 묶음", entries.Count(e => e.kind != "meow_group")) : F("최근 {0}개 메시지", entries.Count)) + "\r\n");
                foreach (ChatEntry entry in entries)
                {
                    DateTime time; DateTime.TryParse(entry.utc, out time);
                    text.AppendLine("[" + time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + "] " + (entry.kind == "meow_group" ? L("야옹 묶음") : entry.name) + (entry.mine ? L(" (나)") : "") + ": " + (entry.kind == "meow_group" ? MeowGroupLabel(entry) + " [" + MeowAuthorSummary(entry).Replace("\n", "; ") + "]" : (entry.kind == "emote" ? L("[이모티콘 #") + entry.emoteId + "] " : entry.kind == "own_send" ? "[" + OwnSendLabel(entry) + "] " : "") + entry.text));
                }
                File.WriteAllText(save.FileName, text.ToString(), new UTF8Encoding(true));
                resultText.Text = L("대화를 저장했어요");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, L("대화를 저장하지 못했어요")); }
        }

        private void LoadDemo()
        {
            roomTitle.Text = "오후의 봉고 모임"; roomInfo.Text = L("참여 중 4명 · 화면 미리보기");
            SetStatus("미리보기 · 아래 대화는 화면 확인용 예시입니다", false);
            DateTime start = DateTime.Now.Date.AddHours(14).AddMinutes(32);
            string[] names = { "구름냥", "나", "치즈", "구름냥", "나", "모카" };
            string[] texts = { "다들 안녕하세요! 오늘도 같이 봉고 쳐요 🐾", "안녕하세요!\n오늘은 작업하면서 켜 두려고요.", "고양이들 모여 있으니까 너무 귀엽다 ㅋㅋ", "이제 대화가 안 사라지고 모여서 좋네요", "맞아요 😺\n아까 놓친 말도 여기서 볼 수 있어요.", "저도 왔어요. 다들 좋은 오후 보내세요!" };
            for (int i = 0; i < names.Length; i++) entries.Add(new ChatEntry { kind = "chat", id = "demo" + i, room = "123", sender = names[i], name = names[i], mine = names[i] == "나", text = texts[i], utc = start.AddMinutes(i).ToUniversalTime().ToString("o") });
            entries.Insert(3, new ChatEntry { kind = "emote", id = "demo-emote", room = "123", sender = "치즈", name = "치즈", text = "하트", emoteId = 1, utc = start.AddMinutes(2).ToUniversalTime().ToString("o") });
            entries.Insert(5,DemoMeowGroup(start.AddMinutes(3)));
            RenderMessages(true);
        }

        public void Snapshot(string path)
        {
            // Render offscreen on an STA thread for visual verification without opening a window.
            var root = (FrameworkElement)Content;
            root.Measure(new Size(510, 750)); root.Arrange(new Rect(0, 0, 510, 750)); root.UpdateLayout();
            Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
            scroll.ScrollToEnd(); root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(510, 750, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(path)) encoder.Save(file);
        }
    }

    public static class Program
    {
        public const string DisplayVersion = "0.4.7";
        private static System.Threading.Mutex instanceMutex;
        private static System.Threading.EventWaitHandle activateEvent;
        private static bool ownsInstance;
        [STAThread]
        public static int Main(string[] args)
        {
            TabletCompatibility.Configure();
            return Run(args);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int Run(string[] args)
        {
            try
            {
                bool preview = args.Length >= 2 && args[0] == "--preview";
                if (args.Length == 2 && (args[0] == "--pause-connection" || args[0] == "--resume-connection")) return DesktopInstall.RunConnectionRecoveryHelper(args[1],args[0] == "--pause-connection");
                if (args.Length == 2 && args[0] == "--prepare-game") return DesktopInstall.RunSetupHelper(args[1]);
                if (!preview && !args.Contains("--demo") && DesktopInstall.Prepare()) return 0;
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                bool demonstration = preview || args.Contains("--demo");
                if (!demonstration)
                {
                    string id = System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
                    instanceMutex = new System.Threading.Mutex(false, "Local\\BongoChat.Window." + id);
                    activateEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "Local\\BongoChat.Activate." + id);
                    try { ownsInstance = instanceMutex.WaitOne(0); } catch (System.Threading.AbandonedMutexException) { ownsInstance = true; }
                    if (!ownsInstance) { activateEvent.Set(); return 0; }
                    var activationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                    activationTimer.Tick += delegate
                    {
                        if (activateEvent != null && activateEvent.WaitOne(0) && app.MainWindow != null)
                        { app.MainWindow.Show(); app.MainWindow.WindowState = WindowState.Normal; app.MainWindow.Activate(); }
                    };
                    activationTimer.Start();
                    app.Exit += delegate { activationTimer.Stop(); };
                }
                string selected = demonstration ? args.FirstOrDefault(a => a.StartsWith("--lang=")) : null;
                selected = selected == null ? UiText.ReadPreference() : selected.Substring(7);
                if (!demonstration && !UiText.IsLanguage(selected))
                {
                    var picker = new LanguagePicker();
                    if (picker.ShowDialog() != true) { app.Shutdown(); return 0; }
                    selected = picker.SelectedLanguage;
                }
                var window = new ChatWindow(demonstration, null, args.Contains("--dark") ? (bool?)true : args.Contains("--light") ? (bool?)false : null, selected, !demonstration);
                if (preview) { window.Snapshot(args[1]); return 0; }
                if (!demonstration) { window.EnableBackgroundCommunication(); window.EnableAutomaticSetup(); window.EnableUpdateNotifications(); }
                app.MainWindow = window; app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.Run(window); return 0;
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(AppStorage.PathFor("error.log"), ex.ToString()); } catch { }
                if (!args.Contains("--preview")) MessageBox.Show(ex.ToString(), UiText.Get(UiText.ReadPreference() ?? "en", "Bongo Cat Chat Merger 오류"));
                return 1;
            }
            finally
            {
                if (instanceMutex != null) { if (ownsInstance) instanceMutex.ReleaseMutex(); instanceMutex.Dispose(); }
                if (activateEvent != null) { activateEvent.Dispose(); activateEvent = null; }
            }
        }
    }
}