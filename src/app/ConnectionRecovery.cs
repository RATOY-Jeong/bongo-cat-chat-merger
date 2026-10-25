using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace BongoChat
{
    public static partial class BridgeSetup
    {
        private const string PausedLoader = "winhttp.dll.bongochat-paused";
        public static bool IsConnectionPaused(string game)
        { return !String.IsNullOrEmpty(game) && File.Exists(Path.Combine(game, PausedLoader)); }
        public static void SetConnectionPaused(string folder, bool pause)
        {
            string game = NormalizeGame(folder), mutexId;
            using (var input = new MemoryStream(Encoding.UTF8.GetBytes(game.ToUpperInvariant()))) mutexId = Hash(input).Substring(0,24);
            using (var mutex = new Mutex(false,"Local\\BongoChat.Setup." + mutexId))
            {
                bool locked = false;
                try
                {
                    try { locked = mutex.WaitOne(0); } catch (AbandonedMutexException) { locked = true; }
                    if (!locked) throw new IOException("다른 창에서 연결을 준비하고 있어요. 잠시 후 다시 눌러 주세요.");
                    if (GameLocator.IsRunning(game)) throw new IOException("연결 준비를 위해 봉고캣을 먼저 종료해 주세요.");
                    using (var input = Payload()) using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
                    {
                        var plan = ReadPlan(zip); var manifest = ReadManifest(game,plan);
                        if (manifest == null || manifest.pending) throw new IOException("기존 설치 정보를 확인할 수 없어요.");
                        string active = Destination(game,"winhttp.dll"), paused = Destination(game,PausedLoader);
                        if (File.Exists(active) && File.Exists(paused)) throw new IOException("연결 파일이 충돌해요. 기존 파일은 그대로 보존했어요.");
                        string source = pause ? active : paused, target = pause ? paused : active;
                        if (!File.Exists(source))
                        {
                            if (!File.Exists(target)) throw new IOException("연결 파일 검증에 실패했어요.");
                            source = target;
                        }
                        string hash = FileHash(source);
                        var record = manifest.files.FirstOrDefault(f => String.Equals(f.relative,"winhttp.dll",StringComparison.OrdinalIgnoreCase));
                        var expected = plan.First(f => f.Relative == "winhttp.dll");
                        if (record == null || !(String.Equals(hash,record.sha256,StringComparison.OrdinalIgnoreCase) || String.Equals(hash,expected.Hash,StringComparison.OrdinalIgnoreCase)))
                            throw new IOException("설치 파일이 다른 프로그램에서 변경됐어요. 기존 파일을 보존했어요: winhttp.dll");
                        if (GameLocator.IsRunning(game)) throw new IOException("연결 준비를 위해 봉고캣을 먼저 종료해 주세요.");
                        if (source != target) File.Move(source,target);
                    }
                }
                finally { if (locked) mutex.ReleaseMutex(); }
            }
        }
    }
    public sealed partial class ChatWindow
    {
        private bool connectionPaused;
        private MenuItem pauseConnectionItem;
        private const string PausedNotice = "연결 기능을 껐어요. 봉고캣만 실행해 증상을 확인해 주세요. 연결 도우미를 누르면 복원해요.";
        private void InitializeConnectionRecovery(ContextMenu menu)
        {
            menu.Items.Add(new Separator());
            pauseConnectionItem = new MenuItem();
            pauseConnectionItem.Click += delegate { BeginConnectionRecovery(true); };
            menu.Items.Add(pauseConnectionItem);
        }
        private void RefreshConnectionRecovery()
        {
            if (pauseConnectionItem == null) return;
            pauseConnectionItem.Header = L("연결 기능 끄고 게임 재시작");
            pauseConnectionItem.IsEnabled = !demo && !setupBusy && !connectionPaused && GameLocator.IsGame(gamePath);
        }
        private void BeginConnectionRecovery(bool pause)
        {
            if (demo || setupBusy || !GameLocator.IsGame(gamePath)) return;
            string prompt = pause ? "게임을 종료하고 채팅 연결 기능을 끈 상태로 다시 실행합니다. 게임 데이터와 대화 기록은 보존됩니다. 진행할까요?"
                : "게임을 종료하고 채팅 연결 기능을 복원한 뒤 다시 실행합니다. 진행할까요?";
            if (MessageBox.Show(this,L(prompt),"Bongo Cat Chat Merger",MessageBoxButton.YesNo,MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (pendingRequest != null) CheckPending(null);
            setupBusy = true; canSend = false; reconnectTimer.Stop(); reconnectButton.IsEnabled = false;
            SetupMessage("봉고캣을 종료하고 연결을 준비하고 있어요. 게임은 다시 실행해 드려요.","retry"); RefreshComposer();
            string game = gamePath;
            Task.Factory.StartNew(delegate
            {
                try
                {
                    if (GameLocator.IsRunning(game))
                    {
                        GameLocator.CloseGame(game); DateTime until = DateTime.UtcNow.AddSeconds(20);
                        while (GameLocator.IsRunning(game) && DateTime.UtcNow < until) Thread.Sleep(250);
                        if (GameLocator.IsRunning(game)) throw new IOException("봉고캣이 종료되지 않았어요. 게임 트레이 메뉴에서 종료한 뒤 연결 도우미를 눌러 주세요.");
                    }
                    try { BridgeSetup.SetConnectionPaused(game,pause); }
                    catch (UnauthorizedAccessException)
                    { if (!DesktopInstall.ConnectionRecoveryElevated(game,pause)) throw new IOException("설치 권한을 받지 못했어요. 연결 도우미를 눌러 다시 시도할 수 있어요."); }
                    if (!pause) BridgeSetup.Install(game,null);
                    bool actualPause = BridgeSetup.IsConnectionPaused(game);
                    if (!setupClosed) StartGame(game);
                    OnSetupUI(delegate
                    {
                        connectionPaused = actualPause; FinishSetupTask();
                        SetupMessage(actualPause ? PausedNotice : "연결 준비가 끝났어요. 봉고캣이 열리면 원하는 방에 들어가 주세요.",actualPause ? "resume" : "retry");
                        Poll();
                    });
                }
                catch (Exception ex)
                {
                    OnSetupUI(delegate
                    {
                        connectionPaused = BridgeSetup.IsConnectionPaused(game); lastSetupError = ex.ToString(); FinishSetupTask();
                        SetupMessage("자동 연결을 완료하지 못했어요: " + ex.Message,connectionPaused ? "resume" : "retry");
                    });
                }
            });
        }
    }
}