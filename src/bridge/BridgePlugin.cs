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
    public static class CatalogCompatibility
    {
        // The shipped game uses Mono's legacy string hash. The full Mono
        // libraries needed by the loader use a randomized string hash instead.
        // Restore only Addressables' catalog marker, leaving its validation
        // and the process-wide string hashing behavior intact.
        public static int LegacyCatalogMagic()
        {
            const string value = "ContentCatalogData";
            unchecked
            {
                int first = 5381, second = 5381;
                for (int i = 0; i < value.Length; i += 2)
                {
                    first = ((first << 5) + first) ^ value[i];
                    if (i + 1 < value.Length) second = ((second << 5) + second) ^ value[i + 1];
                }
                return first + second * 1566083941;
            }
        }

        public static void Prepare(Type catalogType, string catalogPath)
        {
            FieldInfo magic = catalogType.GetField("kMagic", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo version = catalogType.GetField("kVersion", BindingFlags.Static | BindingFlags.NonPublic);
            if (magic == null || magic.FieldType != typeof(int) || version == null || !version.IsLiteral)
                throw new NotSupportedException("Addressables catalog fields changed.");
            int expected = LegacyCatalogMagic();
            using (var reader = new BinaryReader(File.OpenRead(catalogPath)))
            {
                if (reader.BaseStream.Length < 8 || reader.ReadInt32() != expected ||
                    reader.ReadInt32() != (int)version.GetRawConstantValue())
                    throw new NotSupportedException("Addressables catalog header is unsupported.");
            }
            // GetValue completes the original type initializer before setting
            // the marker to the deterministic value used by the shipped game.
            if ((int)magic.GetValue(null) != expected) magic.SetValue(null, expected);
        }
    }

    public static class StartupEnvironment
    {
        public static void PrepareChildProcesses()
        {
            // Doorstop 4.5 sets DISABLE after installing its hooks and INITIALIZED
            // before calling the managed entry point. Both guards would otherwise
            // leak into Steam and disable a later Steam-managed game process.
            // This runs only after this loader successfully loaded our plugin;
            // it never overrides a user's disabled loader or persistent settings.
            Environment.SetEnvironmentVariable("DOORSTOP_DISABLE", null, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("DOORSTOP_INITIALIZED", null, EnvironmentVariableTarget.Process);
        }
    }

    [BepInPlugin("local.bongochat.bridge", "Bongo Chat Bridge", "0.2.2")]
    public sealed partial class BridgePlugin : BaseUnityPlugin
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
            try { StartupEnvironment.PrepareChildProcesses(); }
            catch (Exception ex) { Logger.LogWarning("Could not clear the inherited startup guard: " + ex.Message); }
            Active = this;
            directory = Path.Combine(Paths.BepInExRootPath, "BongoChat");
            try
            {
                CatalogCompatibility.Prepare(
                    Type.GetType("UnityEngine.AddressableAssets.ResourceLocators.ContentCatalogData, Unity.Addressables", true),
                    Path.Combine(Paths.GameRootPath, "BongoCat_Data", "StreamingAssets", "aa", "catalog.bin"));
                Logger.LogInfo("Addressables catalog compatibility ready (legacy marker restored).");
                Directory.CreateDirectory(directory);
                Type multiplayer = Type.GetType("BongoCat.Multiplayer.SteamMultiplayer, Assembly-CSharp", true);
                MethodInfo receive = AccessTools.Method(multiplayer, "OnLobbyChatMsg", new[] { typeof(LobbyChatMsg) });
                if (receive == null) throw new MissingMethodException("SteamMultiplayer.OnLobbyChatMsg");
                harmony = new Harmony("local.bongochat.bridge");
                harmony.Patch(receive, prefix: new HarmonyMethod(typeof(BridgePlugin), "ReceivePrefix"));
                InitializeFeatures(multiplayer);
                Logger.LogInfo("Bongo Chat Bridge ready (chat, emotes and composer). Directory: " + directory);
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
                if (envelope == null) return;
                if (envelope.Channel == 6) { CaptureEmote(msg, envelope.Message); return; }
                if (envelope.Channel != 3) return;
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
            UpdateFeatures();
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
                    protocol = 1, version = "0.2.2", utc = DateTime.UtcNow.ToString("o"),
                    session = session, stopped = stopped, ready = harmony != null && String.IsNullOrEmpty(error),
                    room = valid ? lobby.SteamId.m_SteamID.ToString() : "",
                    name = valid ? lobby.Name : "", code = valid ? lobby.LobbyCode : "",
                    count = valid ? lobby.MemberCount : 0, error = error,
                    canSend = valid && speech != null && harmony != null && String.IsNullOrEmpty(error),
                    maxMessageLength = messageLimit, assetRevision = assetRevision
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
