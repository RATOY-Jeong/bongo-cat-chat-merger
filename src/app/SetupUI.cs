using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace BongoChat
{
    public sealed partial class ChatWindow
    {
        private bool automaticSetupEnabled, setupBusy, setupClosed;
        private string setupNotice;
        private DateTime setupLaunchDeadline;
        private string setupAction = "retry";
        private Button setupButton;
        public void EnableAutomaticSetup()
        {
            automaticSetupEnabled = true;
            Loaded += delegate { BeginSetup(false); };
        }
        private void InitializeSetup(Grid root)
        {
            setupButton = (Button)root.FindName("Choose");
            setupButton.Click += delegate { BeginSetup(setupAction == "restart"); };
            var menu = new ContextMenu();
            var choose = new MenuItem();
            choose.Click += delegate { setupNotice = null; setupAction = "retry"; ChooseGame(); if (!demo) BeginSetup(false); };
            var diagnostic = new MenuItem();
            diagnostic.Click += delegate
            {
                try { Clipboard.SetText("Bongo Cat Chat Merger " + typeof(ChatWindow).Assembly.GetName().Version + "\r\n" + statusText.Text + "\r\n" + (lastSetupError ?? "") + "\r\nUI health: " + UiHealthMonitor.LastReport() + "\r\nBackground IO: " + backgroundIo + ", read pending: " + pollReading + ", send pending: " + (pendingRequest != null) + "\r\nInput mode: " + (TabletCompatibility.Status ?? "Not configured") + "\r\nGame found: " + GameLocator.IsGame(gamePath) + "\r\nGame running: " + GameLocator.IsRunning(gamePath)); }
                catch { }
            };
            menu.Items.Add(choose); menu.Items.Add(diagnostic); setupButton.ContextMenu = menu; InitializeConnectionRecovery(menu);
            Closed += delegate { setupClosed = true; };
            RefreshSetupLabels();
        }
        private string lastSetupError = "";
        private void RefreshSetupLabels()
        {
            if (setupButton == null) return;
            RefreshConnectionRecovery();
            string label = setupBusy ? "준비 중…" : setupAction == "resume" ? "연결 복원하고 게임 재시작" : setupAction == "restart" ? "설치하고 게임 재시작" : setupAction == "steam" ? "Steam에서 봉고캣 열기" : "연결 도우미";
            setupButton.Content = L(label);
            setupButton.IsEnabled = !demo && !setupBusy;
            setupButton.ToolTip = L("게임 위치와 연결 파일을 자동으로 확인하고 복구합니다.");
            ((MenuItem)setupButton.ContextMenu.Items[0]).Header = L("게임 위치 직접 선택");
            ((MenuItem)setupButton.ContextMenu.Items[1]).Header = L("진단 내용 복사");
        }
        private void SetupMessage(string notice, string action)
        {
            setupNotice = notice; setupAction = action;
            SetStatus(notice, false); RefreshSetupLabels();
        }
        private void OnSetupUI(Action action)
        {
            if (!setupClosed && !Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(delegate { if (!setupClosed) action(); }));
        }
        private bool HasLiveBridge()
        {
            try
            {
                var state = json.Deserialize<BridgeState>(Journal.ReadShared(Path.Combine(BridgeFolder, "state.json")));
                DateTime time;
                return state != null && state.protocol == 1 && state.ready && !state.stopped
                    && DateTime.TryParse(state.utc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out time)
                    && Math.Abs((DateTime.UtcNow - time.ToUniversalTime()).TotalSeconds) < 6;
            }
            catch { return false; }
        }
        private void BeginSetup(bool restartConfirmed)
        {
            if (demo || setupBusy) return;
            if (setupAction == "resume") { BeginConnectionRecovery(false); return; }
            setupLaunchDeadline = default(DateTime);
            if (setupAction == "steam" && !GameLocator.IsGame(gamePath))
            {
                try { Process.Start(new ProcessStartInfo("steam://store/3419430") { UseShellExecute = true }); } catch { }
            }
            if (pendingRequest != null) CheckPending(null);
            reconnectTimer.Stop(); reconnectButton.Content = L("재연결"); reconnectButton.IsEnabled = false;
            setupBusy = true; canSend = false; lastSetupError = "";
            SetupMessage("봉고캣 위치와 연결 파일을 자동으로 확인하고 있어요.", "retry"); RefreshComposer();
            Task.Factory.StartNew(delegate
            {
                try
                {
                    string preferred = gamePath;
                    if (String.IsNullOrEmpty(preferred)) try { preferred = File.ReadAllText(settingsPath).Trim(); } catch { }
                    string found = GameLocator.Find(preferred);
                    if (String.IsNullOrEmpty(found))
                    {
                        OnSetupUI(delegate { FinishSetupTask(); SetupMessage("봉고캣을 찾지 못했어요. Steam에서 설치한 뒤 연결 도우미를 눌러 주세요.", "steam"); });
                        return;
                    }
                    gamePath = found;
                    try { File.WriteAllText(settingsPath, found); } catch { }
                    if (BridgeSetup.IsConnectionPaused(found))
                    {
                        OnSetupUI(delegate { connectionPaused = true; FinishSetupTask(); SetupMessage(PausedNotice,"resume"); });
                        return;
                    }
                    connectionPaused = false;
                    if (GameLocator.IsRunning(found) && HasLiveBridge())
                    {
                        OnSetupUI(delegate { setupNotice = null; setupAction = "retry"; FinishSetupTask(); Poll(); });
                        return;
                    }
                    var check = BridgeSetup.Inspect(found);
                    if (check.Problem != null) throw new IOException(check.Problem);
                    bool running = GameLocator.IsRunning(found);
                    if (running && !restartConfirmed)
                    {
                        OnSetupUI(delegate { FinishSetupTask(); SetupMessage("처음 연결을 준비하려면 봉고캣을 다시 시작해야 해요. 아래 버튼을 누르면 자동으로 진행해요.", "restart"); });
                        return;
                    }
                    if (running)
                    {
                        OnSetupUI(delegate { SetupMessage("봉고캣을 종료하고 연결을 준비하고 있어요. 게임은 다시 실행해 드려요.", "retry"); });
                        GameLocator.CloseGame(found);
                        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
                        while (GameLocator.IsRunning(found) && DateTime.UtcNow < deadline) Thread.Sleep(250);
                        if (GameLocator.IsRunning(found)) throw new IOException("봉고캣이 종료되지 않았어요. 게임 트레이 메뉴에서 종료한 뒤 연결 도우미를 눌러 주세요.");
                    }
                    if (check.NeedsInstall)
                    {
                        OnSetupUI(delegate { SetupMessage("필요한 연결 파일을 설치하고 있어요.", "retry"); });
                        try { BridgeSetup.Install(found, null); }
                        catch (UnauthorizedAccessException)
                        {
                            OnSetupUI(delegate { SetupMessage("Windows 권한 창이 뜨면 예를 눌러 주세요. 연결에 필요한 파일만 설치해요.", "retry"); });
                            if (!DesktopInstall.InstallElevated(found)) throw new IOException("설치 권한을 받지 못했어요. 연결 도우미를 눌러 다시 시도할 수 있어요.");
                        }
                    }
                    var after = BridgeSetup.Inspect(found);
                    if (after.NeedsInstall || after.Problem != null) throw new IOException(after.Problem ?? "연결 파일 검증에 실패했어요.");
                    if (setupClosed) return;
                    StartGame(found);
                    OnSetupUI(delegate { FinishSetupTask(); setupLaunchDeadline = DateTime.UtcNow.AddSeconds(45); SetupMessage("연결 준비가 끝났어요. 봉고캣이 열리면 원하는 방에 들어가 주세요.", "retry"); Poll(); });
                }
                catch (Exception ex)
                {
                    lastSetupError = ex.ToString();
                    try { File.WriteAllText(AppStorage.PathFor("setup-error.log"), lastSetupError); } catch { }
                    OnSetupUI(delegate
                    {
                        FinishSetupTask();
                        string message = ex is System.ComponentModel.Win32Exception && ((System.ComponentModel.Win32Exception)ex).NativeErrorCode == 1223
                            ? "설치 권한을 받지 못했어요. 연결 도우미를 눌러 다시 시도할 수 있어요."
                            : "자동 연결을 완료하지 못했어요: " + ex.Message;
                        SetupMessage(message, "retry");
                    });
                }
            });
        }
        private void FinishSetupTask()
        {
            setupBusy = false; reconnectButton.IsEnabled = !demo; RefreshSetupLabels(); RefreshComposer();
        }
        public static ProcessStartInfo GameStartInfo(string game)
        {
            var info = new ProcessStartInfo(Path.Combine(game, "BongoCat.exe")) { UseShellExecute = false, WorkingDirectory = game };
            info.EnvironmentVariables.Remove("DOORSTOP_DISABLE"); info.EnvironmentVariables.Remove("DOORSTOP_INITIALIZED");
            return info;
        }
        private static void StartGame(string game) { if (!GameLocator.IsRunning(game)) using (Process.Start(GameStartInfo(game))) { } }
    }
}