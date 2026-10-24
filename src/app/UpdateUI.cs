using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace BongoChat
{
    public sealed partial class ChatWindow
    {
        private bool updateEnabled, updateBusy, updateShowNotice, updateLastFailed, updateStartupChecked;
        private UpdateResult updateResult;
        private Button updateCheckButton, updateOpenButton;
        private TextBlock updateText;
        private Border updateOffer;

        private Func<UpdateResult> updateProbe;
        private Action<string> updateOpen;
        private void InitializeUpdates(Grid root)
        {
            updateCheckButton = (Button)root.FindName("CheckUpdate");
            updateOpenButton = (Button)root.FindName("OpenRelease");
            updateText = (TextBlock)root.FindName("UpdateText");
            updateOffer = (Border)root.FindName("UpdateOffer");
            updateCheckButton.Click += delegate { CheckForUpdates(true); };
            updateOpenButton.Click += delegate
            {
                string target = updateResult != null ? updateResult.PageUrl : UpdateService.LatestPage;
                try { updateOpen(target); }
                catch { MessageBox.Show(this, L("브라우저를 열지 못했어요. 아래 주소를 확인해 주세요.") + "\n\n" + target, Title); }
            };
            RefreshUpdateLabels();
        }
        public void EnableUpdateNotifications(Func<UpdateResult> check = null, Action<string> open = null)
        {
            if (demo || updateEnabled) return;
            updateEnabled = true;
            updateProbe = check ?? delegate { return UpdateService.Check(typeof(ChatWindow).Assembly.GetName().Version); };
            updateOpen = open ?? delegate(string url) { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); };


            Loaded += delegate { CheckStartupUpdate(); };

            if (IsLoaded) CheckStartupUpdate();
            RefreshUpdateLabels();
        }
        private void CheckStartupUpdate()
        {
            if (updateStartupChecked) return;
            updateStartupChecked = true;
            CheckForUpdates(false);
        }
        private void CheckForUpdates(bool manual)
        {
            if (demo || !updateEnabled || updateBusy) return;
            updateBusy = true; RefreshUpdateLabels();
            Task.Factory.StartNew(delegate
            {
                UpdateResult result;
                try { result = updateProbe() ?? UpdateResult.Failure(false); }
                catch { result = UpdateResult.Failure(false); }
                OnSetupUI(delegate
                {
                    updateBusy = false;
                    bool failed = result.Kind == UpdateKind.Failed || result.Kind == UpdateKind.RateLimited;
                    if (failed && updateResult != null && updateResult.Kind == UpdateKind.Available)
                        updateLastFailed = true; // Preserve a previously found download link while offline.
                    else { updateResult = result; updateLastFailed = false; }
                    updateShowNotice = updateShowNotice || manual || result.Kind == UpdateKind.Available;
                    RefreshUpdateLabels();
                });
            });
        }
        private void RefreshUpdateLabels()
        {
            if (updateCheckButton == null) return;
            updateCheckButton.Content = L(updateBusy ? "확인 중…" : "업데이트 확인");
            updateCheckButton.ToolTip = L("GitHub의 새 정식 버전을 확인합니다. 파일은 자동으로 설치하지 않습니다.");
            updateCheckButton.IsEnabled = !demo && updateEnabled && !updateBusy;
            updateOpenButton.Content = L("다운로드 페이지");
            updateOpenButton.ToolTip = updateResult == null ? UpdateService.LatestPage : updateResult.PageUrl;
            updateOffer.Visibility = updateShowNotice ? Visibility.Visible : Visibility.Collapsed;
            MinHeight = updateShowNotice ? 600 : 560;
            bool available = updateResult != null && updateResult.Kind == UpdateKind.Available;
            updateOffer.Background = Brush(available ? "#FFF3CA" : "#D3E0E9");
            updateText.Foreground = Brush(available ? "#775D25" : "#506471");
            if (updateResult == null) updateText.Text = "";
            else if (available)
                updateText.Text = F("최신 업데이트가 있습니다! {0}", updateResult.VersionText) + (updateLastFailed ? L(" · 재확인 실패") : "");
            else if (updateResult.Kind == UpdateKind.Current)
                updateText.Text = F("최신 버전을 사용 중입니다. {0}", Program.DisplayVersion);
            else if (updateResult.Kind == UpdateKind.NoPackage)
                updateText.Text = L("공개된 정식 업데이트 파일이 아직 없어요.");
            else if (updateResult.Kind == UpdateKind.RateLimited)
                updateText.Text = L("GitHub 조회 제한에 도달했어요. 잠시 후 다시 확인해 주세요.");
            else updateText.Text = L("업데이트를 확인하지 못했어요. 인터넷 연결을 확인하거나 다운로드 페이지를 열어 주세요.");
            updateOpenButton.Visibility = updateResult != null && updateResult.Kind != UpdateKind.Current ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}