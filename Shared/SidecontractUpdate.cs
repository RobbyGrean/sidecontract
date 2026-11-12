using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Sidecontract
{
    internal sealed class UpdateManifest
    {
        public int schemaVersion { get; set; }
        public string version { get; set; }
        public string installerUrl { get; set; }
        public string installerSha256 { get; set; }
        public string databaseSha256 { get; set; }
    }

    internal sealed class PendingUpdate
    {
        public string version { get; set; }
        public string databaseSha256 { get; set; }
        public string appDirectory { get; set; }
    }

    internal static class SidecontractUpdate
    {
        public const string ManifestUrl = "https://raw.githubusercontent.com/RobbyGrean/sidecontract/main/assets/downloads/update.json";
        public const string InstallerUrl = "https://raw.githubusercontent.com/RobbyGrean/sidecontract/main/assets/downloads/sidecontract-Setup.exe";
        public static Version CurrentVersion { get { return typeof(SidecontractUpdate).Assembly.GetName().Version; } }
        public static string VersionText { get { return CurrentVersion.ToString(3); } }
        public static string DatabasePath { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_database.json"); } }
        private static string PendingPath { get { return Path.Combine(SidecontractPaths.DataRoot, "pending-update.json"); } }

        private sealed class UpdateClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                request.Timeout = 30000;
                var http = request as HttpWebRequest;
                if (http != null) http.ReadWriteTimeout = 30000;
                return request;
            }
        }

        private static WebClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var client = new UpdateClient();
            client.Headers[HttpRequestHeader.UserAgent] = "sidecontract/" + VersionText;
            client.Headers[HttpRequestHeader.CacheControl] = "no-cache";
            return client;
        }

        public static async Task<UpdateManifest> CheckAsync()
        {
            using (var client = CreateClient())
            {
                var download = client.DownloadStringTaskAsync(ManifestUrl + "?check=" + Guid.NewGuid().ToString("N"));
                if (await Task.WhenAny(download, Task.Delay(30000)) != download)
                {
                    client.CancelAsync();
                    throw new TimeoutException("ตรวจสอบอัปเดตเกินเวลาที่กำหนด กรุณาลองใหม่ภายหลัง");
                }
                var json = await download;
                return ParseManifest(json);
            }
        }

        public static UpdateManifest ParseManifest(string json)
        {
            var manifest = new JavaScriptSerializer().Deserialize<UpdateManifest>(json);
            Version version;
            if (manifest == null || manifest.schemaVersion != 1
                || !Version.TryParse(manifest.version, out version)
                || version.Build < 0 || version.Revision > 0
                || manifest.installerUrl != InstallerUrl
                || !IsHash(manifest.installerSha256) || !IsHash(manifest.databaseSha256))
                throw new InvalidDataException("ข้อมูลอัปเดตไม่ถูกต้อง กรุณาติดต่อผู้ดูแลโปรแกรม");
            manifest.version = version.ToString(3);
            return manifest;
        }

        private static bool IsHash(string value)
        {
            return value != null && Regex.IsMatch(value, "\\A[0-9a-fA-F]{64}\\z");
        }

        public static string HashFile(string path)
        {
            using (var input = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "");
        }

        public static bool MatchesDatabase(string expected)
        {
            return File.Exists(DatabasePath) && string.Equals(HashFile(DatabasePath), expected, StringComparison.OrdinalIgnoreCase);
        }

        public static bool NeedsUpdate(UpdateManifest manifest)
        {
            var available = new Version(manifest.version);
            var current = new Version(VersionText);
            return available > current || (available == current && !MatchesDatabase(manifest.databaseSha256));
        }

        public static async Task<string> DownloadAsync(UpdateManifest manifest, Action<int> progress)
        {
            var directory = Path.Combine(Path.GetTempPath(), "sidecontract-Updates", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "sidecontract-Setup.exe");
            try
            {
                using (var client = CreateClient())
                {
                    client.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs e) { progress(e.ProgressPercentage); };
                    var download = client.DownloadFileTaskAsync(manifest.installerUrl + "?version=" + Uri.EscapeDataString(manifest.version) + "&download=" + Guid.NewGuid().ToString("N"), path);
                    if (await Task.WhenAny(download, Task.Delay(300000)) != download)
                    {
                        client.CancelAsync();
                        try { await download; } catch (WebException) { } catch (TaskCanceledException) { }
                        throw new TimeoutException("ดาวน์โหลดเกินเวลาที่กำหนด กรุณาลองใหม่ภายหลัง");
                    }
                    await download;
                }
                VerifyInstaller(path, manifest.installerSha256);
                return path;
            }
            catch
            {
                if (File.Exists(path)) File.Delete(path);
                throw;
            }
        }

        public static void VerifyInstaller(string path, string expectedHash)
        {
            if (!IsHash(expectedHash) || !string.Equals(HashFile(path), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("ไฟล์ติดตั้งไม่ตรงกับข้อมูลอัปเดต กรุณาลองใหม่ภายหลัง");
        }

        public static void StartInstaller(string path, UpdateManifest manifest)
        {
            // Recheck immediately before launch; the download may have been waiting for the save dialog.
            VerifyInstaller(path, manifest.installerSha256);
            Directory.CreateDirectory(SidecontractPaths.DataRoot);
            var pending = new PendingUpdate { version = manifest.version, databaseSha256 = manifest.databaseSha256, appDirectory = AppDomain.CurrentDomain.BaseDirectory };
            File.WriteAllText(PendingPath, new JavaScriptSerializer().Serialize(pending));
            Process.Start(new ProcessStartInfo(path, "/DIR=\"" + AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\') + "\"") { UseShellExecute = true });
        }

        public static string ReadUpdateResult()
        {
            if (!File.Exists(PendingPath)) return null;
            var pending = new JavaScriptSerializer().Deserialize<PendingUpdate>(File.ReadAllText(PendingPath));
            if (pending == null || !string.Equals(pending.appDirectory, AppDomain.CurrentDomain.BaseDirectory, StringComparison.OrdinalIgnoreCase)) return null;
            Version expected;
            if (!Version.TryParse(pending.version, out expected) || !IsHash(pending.databaseSha256))
                throw new InvalidDataException("ไม่สามารถตรวจผลการอัปเดตได้ กรุณากดอัปเดตโปรแกรมอีกครั้ง");
            if (new Version(VersionText) < expected || !MatchesDatabase(pending.databaseSha256))
                return "การอัปเดตยังไม่ครบ หรือยกเลิกการติดตั้งไว้ กรุณากดอัปเดตโปรแกรมอีกครั้ง\n\nไฟล์ที่เปิดอยู่: " + AppDomain.CurrentDomain.BaseDirectory;
            File.Delete(PendingPath);
            return "อัปเดตเป็นรุ่น " + VersionText + " สำเร็จ และตรวจสอบตารางวันที่ส่งเบิกแล้ว";
        }
    }
}
