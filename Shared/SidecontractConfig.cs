using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Sidecontract
{
    internal static class SidecontractConfig
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static T Load<T>(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("ไม่พบไฟล์ config", path);
            var json = File.ReadAllText(path, Encoding.UTF8);
            var result = Serializer.Deserialize<T>(json);
            if (result == null) throw new InvalidDataException("ไฟล์ config ว่าง: " + path);
            return result;
        }
    }

    internal sealed class ProcurementManifest
    {
        public int schemaVersion { get; set; }
        public string module { get; set; }
        public string root { get; set; }
        public ProcurementManifestDocument[] centralDocuments { get; set; }
        public ProcurementManifestRoute[] routes { get; set; }
    }

    internal sealed class ProcurementManifestDocument
    {
        public string id { get; set; }
        public string relativePath { get; set; }
        public int businessTagCount { get; set; }
        public int businessOccurrenceCount { get; set; }
        public string sha256 { get; set; }
    }

    internal sealed class ProcurementManifestRoute
    {
        public string id { get; set; }
        public string positionId { get; set; }
        public bool hasTeaching { get; set; }
        public bool worksAtTwoSchools { get; set; }
        public string tor { get; set; }
        public string quote { get; set; }
        public string[] documents { get; set; }
        public string torSha256 { get; set; }
        public string quoteSha256 { get; set; }
        public int torPages { get; set; }
        public int quotePages { get; set; }
    }

    internal sealed class ProcurementPositionConfig
    {
        public string id { get; set; }
        public string label { get; set; }
        public string salary { get; set; }
        public string salaryText { get; set; }
        public string totalSalary { get; set; }
        public string totalSalaryText { get; set; }
        public int contractMonths { get; set; }
        public bool teachingFlag { get; set; }
        public bool twoSchoolsFlag { get; set; }
    }

    internal sealed class ProcurementPositionCatalog
    {
        public int schemaVersion { get; set; }
        public string module { get; set; }
        public ProcurementPositionConfig[] positions { get; set; }
    }
}
