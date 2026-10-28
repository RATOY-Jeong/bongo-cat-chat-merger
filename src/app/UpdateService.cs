using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace BongoChat
{
    public enum UpdateKind { Available, Current, NoPackage, Failed, RateLimited, Unavailable }
    public sealed class UpdateResult
    {
        public UpdateKind Kind { get; private set; }
        public string VersionText { get; private set; }
        public string PageUrl { get; private set; }
        private UpdateResult(UpdateKind kind, string version, string page)
        { Kind = kind; VersionText = version; PageUrl = page; }
        public static UpdateResult Failure(bool rateLimited)
        { return new UpdateResult(rateLimited ? UpdateKind.RateLimited : UpdateKind.Failed, "", UpdateService.LatestPage); }
        public static UpdateResult Unavailable() { return new UpdateResult(UpdateKind.Unavailable, "", UpdateService.LatestPage); }
        internal static UpdateResult Create(UpdateKind kind, string version, string page)
        { return new UpdateResult(kind, version, page); }
    }
    public static class UpdateService
    {
        public const string Repository = "https://github.com/RATOY-Jeong/bongo-cat-chat-merger";
        public const string LatestPage = Repository + "#readme";
        public const string Endpoint = "https://api.github.com/repos/RATOY-Jeong/bongo-cat-chat-merger/releases/latest";
        public const string ReleasesEndpoint = "https://api.github.com/repos/RATOY-Jeong/bongo-cat-chat-merger/releases?per_page=20";
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
                string page = LatestPage; // Public README has the current download; never use a login or owner-only edit URL.
                return UpdateResult.Create(remote > local ? UpdateKind.Available : UpdateKind.Current, remote.ToString(3), page);
            }
            catch (Exception) { return UpdateResult.Failure(false); }
        }
        // Do not rely solely on GitHub's manually assigned "Latest" label.
        public static UpdateResult ParseReleases(string json, Version installed)
        {
            if (String.IsNullOrWhiteSpace(json) || json.Length > 1048576) return UpdateResult.Failure(false);
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = 1048576 };
                var releases = serializer.Deserialize<Release[]>(json);
                if (releases == null) return UpdateResult.Failure(false);
                var ready = releases.Where(r => r != null && !r.draft && !r.prerelease && ParseVersion(r.tag_name) != null
                    && r.assets != null && r.assets.Any(a => ReadyInstaller(a,r.tag_name)))
                    .OrderByDescending(r => ParseVersion(r.tag_name)).FirstOrDefault();
                if (ready == null) return UpdateResult.Create(UpdateKind.NoPackage,"",LatestPage);
                return Parse(serializer.Serialize(ready),installed);
            }
            catch { return UpdateResult.Failure(false); }
        }
        public static UpdateResult Check(Version installed) { return Check(installed,FetchMetadata); }
        public static UpdateResult Check(Version installed, Func<string,string> fetch)
        {
            // At most two bounded public metadata requests, only at startup or by explicit click.
            UpdateResult first = Probe(ReleasesEndpoint,installed,fetch,true);
            if (first.Kind == UpdateKind.Available || first.Kind == UpdateKind.Current || first.Kind == UpdateKind.NoPackage
                || first.Kind == UpdateKind.RateLimited) return first;
            UpdateResult fallback = Probe(Endpoint,installed,fetch,false);
            return fallback.Kind == UpdateKind.Unavailable && first.Kind == UpdateKind.Failed ? first : fallback;
        }
        private static UpdateResult Probe(string endpoint, Version installed, Func<string,string> fetch, bool list)
        {
            try { string json = fetch(endpoint); return list ? ParseReleases(json,installed) : Parse(json,installed); }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                int code = response == null ? 0 : (int)response.StatusCode;
                if (response != null) response.Dispose();
                return code == 404 ? UpdateResult.Unavailable() : UpdateResult.Failure(code == 403 || code == 429);
            }
            catch { return UpdateResult.Failure(false); }
        }
        private static string FetchMetadata(string endpoint)
        {
            // Public metadata only. No token, cookies, chat data, binary downloads or execution.
            if ((int)ServicePointManager.SecurityProtocol != 0) ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(endpoint);
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
                if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > 1048576) throw new IOException("Invalid release metadata response.");
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    char[] buffer = new char[1048577]; int count = 0, read;
                    while (count < buffer.Length && (read = reader.Read(buffer,count,buffer.Length-count)) > 0) count += read;
                    if (count > 1048576) throw new IOException("Release metadata exceeds limit.");
                    return new string(buffer,0,count);
                }
            }
        }
    }
}