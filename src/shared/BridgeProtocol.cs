using System;
using System.Globalization;
using System.Text;

namespace BongoChat.Protocol
{
    public sealed class SendRequest
    {
        public string id { get; set; }
        public string session { get; set; }
        public string room { get; set; }
        public string createdUtc { get; set; }
        public string kind { get; set; }
        public string text { get; set; }
    }

    public sealed class SendResult
    {
        public string id { get; set; }
        public string session { get; set; }
        public string room { get; set; }
        public bool accepted { get; set; }
        public string error { get; set; }
    }

    public static class RequestPolicy
    {
        public const int MaxCharacters = 300;
        public static string Validate(SendRequest request, string session, string room, DateTime now, int limit)
        {
            Guid id;
            if (request == null || !Guid.TryParseExact(request.id, "N", out id)) return "잘못된 전송 요청입니다.";
            if (request.kind != "send_chat") return "지원하지 않는 요청입니다.";
            if (request.session != session) return "게임이 다시 실행되어 전송을 취소했어요.";
            ulong roomId;
            if (String.IsNullOrEmpty(room) || !UInt64.TryParse(room, out roomId) || roomId == 0 || request.room != room)
                return "채팅방이 바뀌어 전송을 취소했어요.";
            DateTime created;
            if (!DateTime.TryParse(request.createdUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out created))
                return "전송 요청 시간이 올바르지 않아요.";
            double age = (now.ToUniversalTime() - created.ToUniversalTime()).TotalSeconds;
            if (age < -5 || age > 20) return "전송 시간이 지나 취소했어요. 다시 보내 주세요.";
            if (String.IsNullOrWhiteSpace(request.text)) return "메시지를 입력해 주세요.";
            if (request.text.Length > Math.Min(MaxCharacters, Math.Max(1, limit))) return "메시지가 너무 길어요.";
            foreach (char ch in request.text)
                if (Char.IsControl(ch) && ch != '\n' && ch != '\r' && ch != '\t') return "지원하지 않는 제어 문자가 있어요.";
            if (Encoding.UTF8.GetByteCount(request.text) > 1800) return "메시지 용량이 너무 커요.";
            return null;
        }
    }
}
