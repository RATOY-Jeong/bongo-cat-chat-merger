using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace BongoChat
{
    public sealed partial class ChatWindow
    {
        private Button reconnectButton;
        private DispatcherTimer reconnectTimer;
        private DateTime reconnectDeadline;
        private void InitializeReconnect(Grid root)
        {
            reconnectButton = (Button)root.FindName("Reconnect");
            reconnectButton.IsEnabled = !demo;
            reconnectButton.Click += delegate { Reconnect(); };
            reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
            reconnectTimer.Tick += delegate { CheckReconnect(); };
        }

        private void CheckReconnect()
        {
            Poll();
            if (reconnectTimer.IsEnabled && DateTime.UtcNow >= reconnectDeadline) FinishReconnect(false);
        }

        private void Reconnect()
        {
            if (demo || setupBusy || reconnectTimer.IsEnabled) return;
            if (automaticSetupEnabled && !HasLiveBridge()) { BeginSetup(false); return; }
            // A reconnect never replays an uncertain chat request. Keep the draft
            // and invalidate the old request even if an acknowledgement is late.
            if (pendingRequest != null) CheckPending(null);
            // Keep a valid explicitly selected installation. Otherwise rediscover
            // Steam libraries, including a game moved since the app was opened.
            if (!File.Exists(Path.Combine(gamePath, "BongoCat.exe"))) FindGame();
            if (Journal.RoomIdValid(currentRoom)) currentFile = Path.Combine(BridgeFolder, "room_" + currentRoom + ".jsonl");
            currentLength = -1; currentAssetRevision = -1; emoteImages.Clear();
            reconnectDeadline = DateTime.UtcNow.AddSeconds(12);
            reconnectButton.IsEnabled = false;
            reconnectButton.Content = L("확인 중…");
            sendNotice = "연결을 다시 확인하고 있어요. 입력한 글과 기록은 유지돼요.";
            reconnectTimer.Start();
            Poll();
        }

        private void FinishReconnect(bool connected)
        {
            reconnectTimer.Stop();
            reconnectButton.IsEnabled = !demo;
            reconnectButton.Content = L("재연결");
            sendNotice = connected ? "다시 연결됐어요. 입력한 글을 확인한 뒤 전송하세요." :
                "연결되지 않았어요. 봉고캣을 종료한 뒤 Steam 라이브러리에서 다시 실행해 주세요.";
            RefreshComposer();
            if (!connected)
                SetStatus("연결 신호가 없어요 · 봉고캣을 Steam에서 다시 실행하고 방에 들어가 주세요. 계속 안 되면 Steam도 완전히 종료 후 다시 실행해 주세요.", false);
        }
    }
}
