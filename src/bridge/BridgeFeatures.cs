using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BongoChat.Protocol;
using BepInEx;
using HarmonyLib;
using Heathen.SteamworksIntegration;
using Newtonsoft.Json;
using UnityEngine;

namespace BongoChatBridge
{
    public sealed partial class BridgePlugin
    {
        private Type speechType, inventoryType;
        private Component speech;
        private MethodInfo sendSpeech;
        private int messageLimit = RequestPolicy.MaxCharacters;
        private int assetRevision;
        private float nextFeatureTick, nextSpeechSearch, nextAssetAttempt;
        private float lastSend = -10;
        private bool sending, observedSend;
        private readonly Queue<int> pendingIcons = new Queue<int>();
        private readonly HashSet<int> queuedIcons = new HashSet<int>();
        private readonly HashSet<int> failedIcons = new HashSet<int>();
        private readonly HashSet<string> processedCommands = new HashSet<string>();
        private string commandFolder, resultFolder;
        private string iconsRoom = "";
        private sealed class EmoteRecord
        {
            public string kind;
            public string room;
            public int emoteId;
        }

        private void InitializeFeatures(Type multiplayer)
        {
            speechType = Type.GetType("Vfx.CatSpeech, Assembly-CSharp", true);
            inventoryType = Type.GetType("BongoCat.CatInventory, Assembly-CSharp", true);
            sendSpeech = AccessTools.Method(speechType, "SendSpeech", new[] { typeof(string) });
            if (sendSpeech == null) throw new MissingMethodException("CatSpeech.SendSpeech");
            commandFolder = Path.Combine(directory, "commands");
            resultFolder = Path.Combine(commandFolder, "results");
            Directory.CreateDirectory(resultFolder);
            Directory.CreateDirectory(Path.Combine(directory, "emotes"));
            // Observe whether the game's language filter allowed the send.
            harmony.Patch(AccessTools.Method(multiplayer, "SendSpeech", new[] { typeof(string) }),
                prefix: new HarmonyMethod(typeof(BridgePlugin), "ObserveSend"));
        }

        private static void ObserveSend()
        {
            if (Active != null && Active.sending) Active.observedSend = true;
        }

        private void UpdateFeatures()
        {
            if (commandFolder == null) return;
            float now = Time.realtimeSinceStartup;
            if (now < nextFeatureTick) return;
            nextFeatureTick = now + 0.15f;
            try
            {
                if (now >= nextSpeechSearch)
                {
                    nextSpeechSearch = now + 2;
                    speech = null;
                    foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(speechType))
                    {
                        var component = obj as Component;
                        if (component != null &&
                            (bool)AccessTools.Field(speechType, "_isMainCat").GetValue(component))
                        {
                            speech = component;
                            int gameLimit = (int)AccessTools.Field(speechType, "_maxChars").GetValue(component);
                            messageLimit = gameLimit > 0 ? Math.Min(gameLimit, RequestPolicy.MaxCharacters) : RequestPolicy.MaxCharacters;
                            break;
                        }
                    }
                }
                ProcessCommands(now);
                QueueRecentEmotes();
                if (pendingIcons.Count > 0 && now >= nextAssetAttempt)
                {
                    nextAssetAttempt = now + 0.25f;
                    int id = pendingIcons.Dequeue(); queuedIcons.Remove(id);
                    ExportIcon(id);
                }
            }
            catch (Exception ex) { Logger.LogWarning("Feature update failed: " + ex.GetType().Name + ": " + ex.Message); }
        }

        private object FindItem(int id)
        {
            object inventory = AccessTools.Field(inventoryType, "Instance").GetValue(null);
            if (inventory == null) return null;
            var items = AccessTools.Field(inventoryType, "Items").GetValue(inventory) as IEnumerable;
            if (items == null) return null;
            foreach (object item in items)
                if (item != null && (int)AccessTools.Property(item.GetType(), "Id").GetValue(item, null) == id) return item;
            return null;
        }

        private void CaptureEmote(LobbyChatMsg msg, string value)
        {
            int id;
            if (!Int32.TryParse(value, out id) || id <= 0) return;
            string room = msg.lobby.SteamId.m_SteamID.ToString();
            if (room == "0") return;
            object item = FindItem(id);
            string name = item == null ? "이모티콘 #" + id : (string)AccessTools.Property(item.GetType(), "ItemName").GetValue(item, null);
            var entry = new
            {
                kind = "emote", id = Guid.NewGuid().ToString("N"), session = session,
                room = room, sender = msg.sender.SteamId.ToString(), name = msg.sender.Name ?? "고양이",
                mine = msg.sender.IsMe, text = name, emoteId = id,
                utc = DateTime.UtcNow.ToString("o")
            };
            File.AppendAllText(Path.Combine(directory, "room_" + room + ".jsonl"),
                JsonConvert.SerializeObject(entry) + "\n", new UTF8Encoding(false));
            if (!File.Exists(Path.Combine(directory, "emotes", id + ".png")) && !failedIcons.Contains(id) && queuedIcons.Add(id))
                pendingIcons.Enqueue(id);
        }

        private void ExportIcon(int id)
        {
            try
            {
                object item = FindItem(id);
                if (item == null) return;
                var sprite = AccessTools.Field(item.GetType(), "Icon").GetValue(item) as Sprite;
                if (sprite == null) return;
                byte[] png = SpriteExporter.Encode(sprite);
                string destination = Path.Combine(directory, "emotes", id + ".png");
                File.WriteAllBytes(destination + ".tmp", png);
                File.Move(destination + ".tmp", destination);
                assetRevision++;
                Logger.LogInfo("Emote icon cached: " + id);
            }
            catch (Exception ex)
            {
                failedIcons.Add(id);
                Logger.LogWarning("Emote preview unavailable: " + ex.Message);
            }
        }

        private void QueueRecentEmotes()
        {
            LobbyData lobby = LobbyData.Current;
            if (!lobby.IsValid || speech == null) return;
            string room = lobby.SteamId.m_SteamID.ToString();
            if (room == iconsRoom) return;
            iconsRoom = room;
            string file = Path.Combine(directory, "room_" + room + ".jsonl");
            if (!File.Exists(file)) return;
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                bool tail = stream.Length > 65536;
                if (tail) stream.Seek(-65536, SeekOrigin.End);
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    if (tail) reader.ReadLine();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            var record = JsonConvert.DeserializeObject<EmoteRecord>(line);
                            if (record == null || record.kind != "emote" || record.room != room) continue;
                            int id = record.emoteId;
                            if (id > 0 && !File.Exists(Path.Combine(directory, "emotes", id + ".png")) && queuedIcons.Count < 32 && queuedIcons.Add(id))
                                pendingIcons.Enqueue(id);
                        }
                        catch (JsonException) { }
                    }
                }
            }
        }

        private void ProcessCommands(float now)
        {
            foreach (string file in Directory.EnumerateFiles(commandFolder, "*.request.json").Take(4))
            {
                string id = Path.GetFileName(file).Replace(".request.json", "");
                Guid parsed;
                if (!Guid.TryParseExact(id, "N", out parsed)) continue;
                string claimed = Path.Combine(commandFolder, id + ".working");
                SendRequest request = null;
                string problem = null;
                bool accepted = false;
                try
                {
                    File.Move(file, claimed);
                    if (new FileInfo(claimed).Length > 8192) throw new InvalidDataException("Request is too large.");
                    request = JsonConvert.DeserializeObject<SendRequest>(File.ReadAllText(claimed, Encoding.UTF8));
                    LobbyData lobby = LobbyData.Current;
                    string room = lobby.IsValid ? lobby.SteamId.m_SteamID.ToString() : "";
                    problem = RequestPolicy.Validate(request, session, room, DateTime.UtcNow, messageLimit);
                    if (request == null || request.id != id) problem = "전송 요청 ID가 올바르지 않아요.";
                    if (problem == null && !processedCommands.Add(id)) problem = "이미 처리한 전송 요청입니다.";
                    if (problem == null && speech == null) problem = "게임의 채팅 입력이 준비되지 않았어요.";
                    if (problem == null && now - lastSend < 0.8f) problem = "잠시 후 다시 보내 주세요.";
                    if (problem == null)
                    {
                        lastSend = now;
                        observedSend = false; sending = true;
                        try { sendSpeech.Invoke(speech, new object[] { request.text }); }
                        finally { sending = false; }
                        accepted = observedSend;
                        if (!accepted) problem = "게임의 채팅 필터가 전송을 허용하지 않았어요.";
                    }
                }
                catch (Exception ex) { problem = "게임에 전송하지 못했어요: " + (ex.InnerException ?? ex).Message; }
                try
                {
                    var result = new SendResult { id = id, session = session, room = request == null ? "" : request.room,
                        accepted = accepted, error = problem };
                    string resultPath = Path.Combine(resultFolder, id + ".json");
                    File.WriteAllText(resultPath + ".tmp", JsonConvert.SerializeObject(result), new UTF8Encoding(false));
                    File.Move(resultPath + ".tmp", resultPath);
                }
                catch (Exception ex) { Logger.LogWarning("Could not acknowledge send: " + ex.Message); }
                finally { try { if (File.Exists(claimed)) File.Delete(claimed); } catch { } }
            }
        }
    }
}
