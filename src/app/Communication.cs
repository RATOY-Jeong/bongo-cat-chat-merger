using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace BongoChat
{
    internal sealed class PollRead
    {
        public string Game, Room, RequestId;
        public long Length;
        public int Assets;
    }
    internal sealed class PollSnapshot
    {
        public string Game, ReadError;
        public bool StateExists;
        public BridgeState State;
        public Protocol.SendResult Result;
        public List<ChatEntry> Entries;
        public long Length;
    }
    public sealed partial class ChatWindow
    {
        private bool backgroundIo, pollReading;
        private UiHealthMonitor uiHealth;
        private CancellationTokenSource sendCancellation;
        public void EnableBackgroundCommunication()
        {
            if (backgroundIo || demo) return;
            backgroundIo = true;
            uiHealth = new UiHealthMonitor(Dispatcher, AppStorage.PathFor("ui-health.log"));
            Closed += delegate { if (uiHealth != null) uiHealth.Dispose(); if (sendCancellation != null) sendCancellation.Cancel(); };
        }
        private void UiPhase(string phase) { if (uiHealth != null) uiHealth.Phase(phase); }
        private PollRead CapturePollRead()
        {
            return new PollRead { Game = gamePath, Room = currentRoom, Length = currentLength,
                Assets = currentAssetRevision, RequestId = pendingRequest == null ? null : pendingRequest.id };
        }
        private static PollSnapshot ReadPollSnapshot(PollRead read)
        {
            var view = new PollSnapshot { Game = read.Game, Length = -1 };
            if (String.IsNullOrEmpty(read.Game)) return view;
            try
            {
                string folder = Path.Combine(read.Game, "BepInEx", "BongoChat");
                string statePath = Path.Combine(folder, "state.json");
                view.StateExists = File.Exists(statePath);
                if (!view.StateExists) return view;
                var serializer = new JavaScriptSerializer { MaxJsonLength = 1048576 };
                view.State = serializer.Deserialize<BridgeState>(Journal.ReadShared(statePath, 65536));
                if (read.RequestId != null)
                {
                    try
                    {
                        string result = Path.Combine(folder, "commands", "results", read.RequestId + ".json");
                        if (File.Exists(result)) view.Result = serializer.Deserialize<Protocol.SendResult>(Journal.ReadShared(result, 16384));
                    }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
                    catch (ArgumentException) { } catch (InvalidOperationException) { }
                }
                BridgeState state = view.State;
                DateTime stamp;
                if (state == null || state.protocol != 1 || !state.ready || state.stopped || !Journal.RoomIdValid(state.room)
                    || !DateTime.TryParse(state.utc, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.RoundtripKind, out stamp)
                    || Math.Abs((DateTime.UtcNow - stamp.ToUniversalTime()).TotalSeconds) >= 6) return view;
                string journal = Path.Combine(folder, "room_" + state.room + ".jsonl");
                view.Length = File.Exists(journal) ? new FileInfo(journal).Length : 0;
                if (state.room != read.Room || view.Length != read.Length || state.assetRevision != read.Assets)
                    view.Entries = Journal.ReadRecent(journal, 500);
            }
            catch (Exception ex) { view.ReadError = ex.GetType().Name; }
            return view;
        }
        private void ReadPollInBackground()
        {
            if (pollReading) { RefreshComposer(); return; }
            pollReading = true;
            PollRead read = CapturePollRead();
            Task.Factory.StartNew(delegate
            {
                PollSnapshot snapshot = ReadPollSnapshot(read);
                OnSetupUI(delegate
                {
                    pollReading = false;
                    if (!String.Equals(gamePath, snapshot.Game, StringComparison.OrdinalIgnoreCase)) return;
                    ApplyPollSnapshot(snapshot);
                });
            });
        }
        private void RemoveCommandFile(string file)
        {
            Action remove = delegate { try { File.Delete(file); } catch { } };
            if (backgroundIo) Task.Factory.StartNew(remove); else remove();
        }
        private void WriteSendRequest(Protocol.SendRequest request)
        {
            string folder = Path.Combine(BridgeFolder, "commands");
            string destination = Path.Combine(folder, request.id + ".request.json");
            string text = new JavaScriptSerializer().Serialize(request);
            sendCancellation = new CancellationTokenSource();
            CancellationToken cancel = sendCancellation.Token;
            Action write = delegate
            {
                try
                {
                    Directory.CreateDirectory(folder);
                    if (cancel.IsCancellationRequested) return;
                    File.WriteAllText(destination + ".tmp", text, new System.Text.UTF8Encoding(false));
                    if (cancel.IsCancellationRequested) { try { File.Delete(destination + ".tmp"); } catch { } return; }
                    File.Move(destination + ".tmp", destination);
                    if (cancel.IsCancellationRequested) { try { File.Delete(destination); } catch { } }
                }
                catch (Exception ex)
                {
                    Action failed = delegate
                    {
                        if (pendingRequest == null || pendingRequest.id != request.id) return;
                        pendingRequest = null;
                        PreservePendingDraft();
                        sendNotice = "전송 요청을 만들지 못했어요: " + ex.GetType().Name;
                        RefreshComposer();
                    };
                    if (backgroundIo) OnSetupUI(failed); else failed();
                }
            };
            if (backgroundIo) Task.Factory.StartNew(write); else write();
        }
    }
}