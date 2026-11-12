using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Sidecontract;

namespace Sidecontract
{
    // Isolate update receipts from the real user's Data folder.
    internal static class SidecontractPaths
    {
        public static string DataRoot { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-data"); } }
    }
}

internal static class UpdaterRegression
{
    private static int passed;
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
        passed++;
    }

    private static void Reject(Action action, string name)
    {
        try { action(); }
        catch (InvalidDataException) { Assert(true, name); return; }
        throw new Exception("Expected rejection: " + name);
    }

    private static string Json(UpdateManifest manifest) { return new JavaScriptSerializer().Serialize(manifest); }

    private static void Main()
    {
        var directory = AppDomain.CurrentDomain.BaseDirectory;
        var database = Path.Combine(directory, "app_database.json");
        File.WriteAllText(database, "test-database");
        var payload = Path.Combine(directory, "test-installer.bin");
        File.WriteAllText(payload, "test-payload");
        var manifest = new UpdateManifest { schemaVersion = 1, version = SidecontractUpdate.VersionText, installerUrl = SidecontractUpdate.InstallerUrl, installerSha256 = SidecontractUpdate.HashFile(payload), databaseSha256 = SidecontractUpdate.HashFile(database) };
        Assert(!SidecontractUpdate.NeedsUpdate(SidecontractUpdate.ParseManifest(Json(manifest))), "current version + correct table needs no update");
        manifest.version = "99.0.0";
        Assert(SidecontractUpdate.NeedsUpdate(manifest), "new version is offered");
        manifest.version = "0.0.1";
        Assert(!SidecontractUpdate.NeedsUpdate(manifest), "older release cannot downgrade");
        manifest.version = SidecontractUpdate.VersionText + ".0";
        manifest = SidecontractUpdate.ParseManifest(Json(manifest));
        Assert(manifest.version == SidecontractUpdate.VersionText, "three/four component versions compare equally");
        File.WriteAllText(database, "stale-table");
        Assert(SidecontractUpdate.NeedsUpdate(manifest), "same version with stale table offers repair");
        File.Delete(database);
        Assert(SidecontractUpdate.NeedsUpdate(manifest), "same version with missing table offers repair");
        File.WriteAllText(database, "test-database");
        manifest.installerUrl = "https://example.com/wrong.exe";
        Reject(delegate { SidecontractUpdate.ParseManifest(Json(manifest)); }, "unapproved installer URL rejected");
        manifest.installerUrl = SidecontractUpdate.InstallerUrl;
        manifest.installerSha256 = "bad";
        Reject(delegate { SidecontractUpdate.ParseManifest(Json(manifest)); }, "invalid checksum rejected");
        manifest.installerSha256 = SidecontractUpdate.HashFile(payload);
        manifest.version = "not-a-version";
        Reject(delegate { SidecontractUpdate.ParseManifest(Json(manifest)); }, "invalid version rejected");
        manifest.version = SidecontractUpdate.VersionText;
        manifest.schemaVersion = 2;
        Reject(delegate { SidecontractUpdate.ParseManifest(Json(manifest)); }, "unknown manifest schema rejected");
        manifest.schemaVersion = 1;
        SidecontractUpdate.VerifyInstaller(payload, manifest.installerSha256);
        Assert(true, "matching download checksum accepted");
        File.WriteAllText(payload, "corrupted");
        Reject(delegate { SidecontractUpdate.VerifyInstaller(payload, manifest.installerSha256); }, "corrupted download rejected");
        Reject(delegate { SidecontractUpdate.StartInstaller(payload, manifest); }, "installer rechecked before receipt write/launch");

        Directory.CreateDirectory(SidecontractPaths.DataRoot);
        var receiptPath = Path.Combine(SidecontractPaths.DataRoot, "pending-update.json");
        var receipt = new PendingUpdate { version = SidecontractUpdate.VersionText, databaseSha256 = manifest.databaseSha256, appDirectory = directory };
        File.WriteAllText(receiptPath, new JavaScriptSerializer().Serialize(receipt));
        File.WriteAllText(database, "stale-table");
        Assert(SidecontractUpdate.ReadUpdateResult() != null && File.Exists(receiptPath), "stale table cannot be reported as successful update");
        receipt.appDirectory = Path.Combine(directory, "other-copy");
        File.WriteAllText(receiptPath, new JavaScriptSerializer().Serialize(receipt));
        Assert(SidecontractUpdate.ReadUpdateResult() == null && File.Exists(receiptPath), "another installation cannot consume update receipt");
        receipt.appDirectory = directory;
        receipt.version = "99.0.0";
        File.WriteAllText(receiptPath, new JavaScriptSerializer().Serialize(receipt));
        File.WriteAllText(database, "test-database");
        Assert(SidecontractUpdate.ReadUpdateResult() != null && File.Exists(receiptPath), "old executable cannot report successful update");
        receipt.version = SidecontractUpdate.VersionText;
        File.WriteAllText(receiptPath, new JavaScriptSerializer().Serialize(receipt));
        Assert(SidecontractUpdate.ReadUpdateResult() != null && !File.Exists(receiptPath), "correct version + table confirms update once");
        Assert(SidecontractUpdate.ReadUpdateResult() == null, "success notification is not repeated");

        // Exercise the real WebClient async flow with a local in-memory transport.
        MockRequest.Manifest = Json(manifest);
        MockRequest.Payload = Encoding.UTF8.GetBytes("test-payload");
        Assert(WebRequest.RegisterPrefix("https://raw.githubusercontent.com/", new MockFactory()), "test transport registered");
        var checkedManifest = SidecontractUpdate.CheckAsync().GetAwaiter().GetResult();
        Assert(checkedManifest.version == manifest.version, "async manifest download and parse");
        var downloaded = SidecontractUpdate.DownloadAsync(checkedManifest, delegate { }).GetAwaiter().GetResult();
        Assert(SidecontractUpdate.HashFile(downloaded) == manifest.installerSha256, "async installer download verifies checksum");
        File.Delete(downloaded);
        MockRequest.Payload = Encoding.UTF8.GetBytes("wrong-payload");
        Reject(delegate { SidecontractUpdate.DownloadAsync(checkedManifest, delegate { }).GetAwaiter().GetResult(); }, "async download rejects mismatched payload");
        Console.WriteLine("Updater checks passed: " + passed);
    }

    private sealed class MockFactory : IWebRequestCreate
    {
        public WebRequest Create(Uri uri) { return new MockRequest(uri); }
    }
    private sealed class MockRequest : WebRequest
    {
        public static string Manifest;
        public static byte[] Payload;
        private readonly Uri uri;
        public MockRequest(Uri uri) { this.uri = uri; }
        public override WebHeaderCollection Headers { get; set; }
        public override string Method { get; set; }
        public override int Timeout { get; set; }
        public override Uri RequestUri { get { return uri; } }
        public override IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
        {
            var completion = new TaskCompletionSource<WebResponse>(state);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                completion.SetResult(new MockResponse(uri, uri.AbsolutePath.EndsWith("update.json") ? Encoding.UTF8.GetBytes(Manifest) : Payload));
                if (callback != null) callback(completion.Task);
            });
            return completion.Task;
        }
        public override WebResponse EndGetResponse(IAsyncResult result) { return ((Task<WebResponse>)result).Result; }
    }
    private sealed class MockResponse : WebResponse
    {
        private readonly Uri uri;
        private readonly byte[] bytes;
        public MockResponse(Uri uri, byte[] bytes) { this.uri = uri; this.bytes = bytes; }
        public override long ContentLength { get { return bytes.Length; } set { } }
        public override Uri ResponseUri { get { return uri; } }
        public override WebHeaderCollection Headers { get { return new WebHeaderCollection(); } }
        public override Stream GetResponseStream() { return new MemoryStream(bytes); }
    }
}
