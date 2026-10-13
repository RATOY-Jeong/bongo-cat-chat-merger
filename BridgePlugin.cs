using System;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using HarmonyLib;
using Heathen.SteamworksIntegration;
using Newtonsoft.Json;

namespace BongoChatBridge
{
    [BepInPlugin("local.bongochat.bridge", "Bongo Chat Bridge", "0.1.0")]
    public sealed class BridgePlugin : BaseUnityPlugin
    {
        [Serializable]
        private sealed class ChatEnvelope
        {
            public int Channel;
            public string Message;
        }
        internal static BridgePlugin Active;
        private Harmony harmony;
        private string directory;
        private float nextHeartbeat;
        private string error = "";
        private readonly string session = Guid.NewGuid().ToString("N");

        private void Awake()
        {
            Active = this;
            directory = Path.Combine(Paths.BepInExRootPath, "BongoChat");
            try
            {
                Directory.CreateDirectory(directory);
                Type multiplayer = Type.GetType("BongoCat.Multiplayer.SteamMultiplayer, Assembly-CSharp", true);
                MethodInfo receive = AccessTools.Method(multiplayer, "OnLobbyChatMsg", new[] { typeof(LobbyChatMsg) });
                if (receive == null) throw new MissingMethodException("SteamMultiplayer.OnLobbyChatMsg");
                harmony = new Harmony("local.bongochat.bridge");
                harmony.Patch(receive, prefix: new HarmonyMethod(typeof(BridgePlugin), "ReceivePrefix"));
                Logger.LogInfo("Bongo Chat Bridge ready (receive only). Directory: " + directory);
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                Logger.LogError(error);
            }
            WriteState(false);
        }

        // A void prefix observes the original message without changing or skipping game logic.
        private static void ReceivePrefix(LobbyChatMsg __0)
        {
            if (Active != null) Active.Capture(__0);
        }

        private void Capture(LobbyChatMsg msg)
        {
            try
            {
                // Verified against game build 25723683: channel 3 contains chat text.
                if (msg.data == null || msg.data.Length > 65536) return;
                ChatEnvelope envelope = UnityEngine.JsonUtility.FromJson<ChatEnvelope>(msg.Message);
                if (envelope == null || envelope.Channel != 3) return;
                string body = envelope.Message;
                if (String.IsNullOrWhiteSpace(body)) return;
                string room = msg.lobby.SteamId.m_SteamID.ToString();
                if (room == "0") return;
                var entry = new
                {
                    kind = "chat", id = Guid.NewGuid().ToString("N"), session = session,
                    room = room, sender = msg.sender.SteamId.ToString(),
                    name = msg.sender.Name ?? "고양이", mine = msg.sender.IsMe,
                    text = body, utc = DateTime.UtcNow.ToString("o")
                };
                File.AppendAllText(Path.Combine(directory, "room_" + room + ".jsonl"),
                    JsonConvert.SerializeObject(entry) + "\n", new UTF8Encoding(false));
                error = "";
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                Logger.LogWarning("Could not capture chat: " + error);
            }
        }

        private void Update()
        {
            if (UnityEngine.Time.realtimeSinceStartup < nextHeartbeat) return;
            nextHeartbeat = UnityEngine.Time.realtimeSinceStartup + 1.0f;
            WriteState(false);
        }

        private void WriteState(bool stopped)
        {
            try
            {
                LobbyData lobby = LobbyData.Current;
                bool valid = !stopped && lobby.IsValid;
                var state = new
                {
                    protocol = 1, version = "0.1.0", utc = DateTime.UtcNow.ToString("o"),
                    session = session, stopped = stopped, ready = harmony != null && String.IsNullOrEmpty(error),
                    room = valid ? lobby.SteamId.m_SteamID.ToString() : "",
                    name = valid ? lobby.Name : "", code = valid ? lobby.LobbyCode : "",
                    count = valid ? lobby.MemberCount : 0, error = error
                };
                string temporary = Path.Combine(directory, "state.tmp");
                string destination = Path.Combine(directory, "state.json");
                File.WriteAllText(temporary, JsonConvert.SerializeObject(state), new UTF8Encoding(false));
                // Readers open with delete sharing; a replacement keeps the state JSON complete.
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            catch (Exception ex) { Logger.LogWarning("Could not update bridge state: " + ex.Message); }
        }

        private void OnDestroy()
        {
            WriteState(true);
            if (harmony != null) harmony.UnpatchSelf();
            Active = null;
        }
    }
}
