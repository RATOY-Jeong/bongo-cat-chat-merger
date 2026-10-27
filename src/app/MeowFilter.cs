using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Controls;
namespace BongoChat
{
    public static class MeowMessages
    {
        // Exact phrases verified in the installed game's 27 locale tables.
        // Do not trim, normalize, case-fold, split words or match substrings.
        private static readonly HashSet<string> Phrases = new HashSet<string>(StringComparer.Ordinal) {
            "Miaou Miaou",
            "喵~喵~",
            "喵嗚～",
            "Mňau Mňau",
            "Mjav Mjau",
            "Miauw Miauw",
            "Meow Meow",
            "Miu Mau",
            "Miau Miau",
            "Νιάου Νιάου",
            "מיאו מיאו",
            "Miao Miao",
            "にゃんにゃ～ん",
            "야옹 야옹",
            "Mjau Mjau",
            "Miau miau",
            "Мяу Мяу",
            "Miyav Miyav",
            "Няв Няв",
        };
        public static bool IsMeow(string text) { return text != null && Phrases.Contains(text); }
    }
    public sealed partial class ChatWindow
    {
        private bool hideMeows;
        private readonly string meowPreferencePath = AppStorage.PathFor("hide-meows.txt");
        private bool ReadMeowPreference()
        {
            try { return File.Exists(meowPreferencePath) && File.ReadAllText(meowPreferencePath).Trim() == "true"; }
            catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
        }
        private void InitializeMeowFilter(Grid root)
        {
            var toggle = (CheckBox)root.FindName("HideMeows");
            toggle.IsChecked = hideMeows;
            toggle.Checked += delegate { ChangeMeowFilter(true); };
            toggle.Unchecked += delegate { ChangeMeowFilter(false); };
        }
        private void ChangeMeowFilter(bool enabled)
        {
            hideMeows = enabled;
            RenderMessages(false);
            try { File.WriteAllText(meowPreferencePath,enabled ? "true" : "false",new UTF8Encoding(false)); }
            catch (IOException) { sendNotice = "야옹 숨기기는 적용됐지만 설정 파일을 저장하지 못했어요."; RefreshComposer(); }
            catch (UnauthorizedAccessException) { sendNotice = "야옹 숨기기는 적용됐지만 설정 파일을 저장하지 못했어요."; RefreshComposer(); }
        }
    }
}