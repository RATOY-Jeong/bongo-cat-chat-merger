using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace BongoChat
{
    public enum UpdateKind { Available, Current, NoPackage, Failed, RateLimited }
    public sealed class UpdateResult
    {
        public UpdateKind Kind { get; private set; }
        public string VersionText { get; private set; }
        public string PageUrl { get; private set; }
        private UpdateResult(UpdateKind kind, string version, string page)
        { Kind = kind; VersionText = version; PageUrl = page; }
        public static UpdateResult Failure(bool rateLimited)
        { return new UpdateResult(rateLimited ? UpdateKind.RateLimited : UpdateKind.Failed, "", UpdateService.LatestPage); }
        internal static UpdateResult Create(UpdateKind kind, string version, string page)
        { return new UpdateResult(kind, version, page); }
    }
    public static class UpdateService
    {
        public const string Repository = "https://github.com/RATOY-Jeong/bongo-cat-chat-merger";
        public const string LatestPage = Repository + "/releases/latest";
        public const string Endpoint = "https://api.github.com/repos/RATOY-Jeong/bongo-cat-chat-merger/releases/latest";
        private sealed class Asset
        {
            public string name { get; set; }
            public string state { get; set; }
            public string browser_download_url { get; set; }
            public long size { get; set; }
        }
        private sealed class Release
        {
            public string tag_name { get; set; }
            public bool draft { get; set; }
            public bool prerelease { get; set; }
            public Asset[] assets { get; set; }
        }
        public static Version ParseVersion(string tag)
        {
            if (tag == null || tag.Length > 64 || !Regex.IsMatch(tag, @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")) return null;
            Version parsed;
            return Version.TryParse(tag.StartsWith("v") ? tag.Substring(1) : tag, out parsed) ? parsed : null;
        }
        private static bool ReadyInstaller(Asset asset, string tag)
        {
            if (asset == null || asset.state != "uploaded" || asset.size <= 0 || String.IsNullOrEmpty(asset.name)
                || !asset.name.StartsWith("BongoCatChatMerger", StringComparison.Ordinal)
                || !asset.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
            Uri uri;
            string expected = "/RATOY-Jeong/bongo-cat-chat-merger/releases/download/" + Uri.EscapeDataString(tag) + "/";
            return Uri.TryCreate(asset.browser_download_url, UriKind.Absolute, out uri)
                && uri.Scheme == Uri.UriSchemeHttps && uri.Host == "github.com" && uri.IsDefaultPort
                && String.IsNullOrEmpty(uri.UserInfo) && uri.AbsolutePath.StartsWith(expected, StringComparison.Ordinal)
                && uri.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }
        public static UpdateResult Parse(string json, Version installed)
        {
            if (String.IsNullOrWhiteSpace(json) || json.Length > 262144) return UpdateResult.Failure(false);
            try
            {
                var release = new JavaScriptSerializer { MaxJsonLength = 262144 }.Deserialize<Release>(json);
                Version remote = release == null ? null : ParseVersion(release.tag_name);
                if (remote == null) return UpdateResult.Failure(false);
                if (release.draft || release.prerelease || release.assets == null || !release.assets.Any(a => ReadyInstaller(a, release.tag_name)))
                    return UpdateResult.Create(UpdateKind.NoPackage, remote.ToString(3), LatestPage);
                var local = new Version(installed.Major, installed.Minor, Math.Max(0, installed.Build));
                string page = Repository + "/releases/tag/" + Uri.EscapeDataString(release.tag_name);
                return UpdateResult.Create(remote > local ? UpdateKind.Available : UpdateKind.Current, remote.ToString(3), page);
            }
            catch (Exception) { return UpdateResult.Failure(false); }
        }
        public static UpdateResult Check(Version installed)
        {
            // Public metadata only. No token, cookies, chat data, binary downloads or execution.
            try
            {
                if ((int)ServicePointManager.SecurityProtocol != 0) ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var request = (HttpWebRequest)WebRequest.Create(Endpoint);
                request.Method = "GET";
                request.UserAgent = "BongoCatChatMerger/" + Program.DisplayVersion;
                request.Accept = "application/vnd.github+json";
                request.Headers["X-GitHub-Api-Version"] = "2026-03-10";
                request.Timeout = 6000; request.ReadWriteTimeout = 6000;
                request.AllowAutoRedirect = false; request.UseDefaultCredentials = false;
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
                using (var deadline = new System.Threading.Timer(delegate { request.Abort(); }, null, 8000, System.Threading.Timeout.Infinite))
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > 262144) return UpdateResult.Failure(false);
                    using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        char[] buffer = new char[262145]; int count = 0, read;
                        while (count < buffer.Length && (read = reader.Read(buffer, count, buffer.Length - count)) > 0) count += read;
                        if (count > 262144) return UpdateResult.Failure(false);
                        return Parse(new string(buffer, 0, count), installed);
                    }
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                bool limited = response != null && ((int)response.StatusCode == 403 || (int)response.StatusCode == 429);
                if (response != null) response.Dispose();
                return UpdateResult.Failure(limited);
            }
            catch (Exception) { return UpdateResult.Failure(false); }
        }
    }
}