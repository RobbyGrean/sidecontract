using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Sidecontract
{
    internal sealed class TemplateTransferItem
    {
        public DocumentModule SourceModule;
        public string Id = "";
        public string Name = "";
        public string Note = "";
        public string UpdatedAt = "";
        public string LastGeneratedFiscalMonth = "";
        public string LastGeneratedFiscalYear = "";
        public string LastGeneratedAt = "";
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly HashSet<string> AvailableKeys = new HashSet<string>(StringComparer.Ordinal);

        public string SourceLabel
        {
            get { return SourceModule == DocumentModule.Payroll ? "เบิกเงินเดือน" : "จัดซื้อจัดจ้างและสัญญา"; }
        }

        public override string ToString()
        {
            return Name;
        }
    }

    internal sealed class TemplateImportReport
    {
        public readonly List<string> Imported = new List<string>();
        public readonly List<string> Ignored = new List<string>();
        public readonly List<string> Blank = new List<string>();
        public readonly List<string> Unavailable = new List<string>();

        public string ToDisplayText(TemplateTransferItem item, DocumentModule target)
        {
            var builder = new StringBuilder();
            builder.AppendLine("ต้นทาง: " + item.SourceLabel);
            builder.AppendLine("ปลายทาง: " + (target == DocumentModule.Payroll ? "เบิกเงินเดือน" : "จัดซื้อจัดจ้างและสัญญา"));
            builder.AppendLine();
            builder.AppendLine("ข้อมูลร่วมที่จะเขียนทับทั้งหมด:");
            AppendList(builder, Imported, "  ");
            if (Imported.Count == 0) builder.AppendLine("  - ไม่มีข้อมูลร่วมที่กรอกไว้");
            builder.AppendLine();
            builder.AppendLine("ข้อมูลจากต้นทางที่ไม่โหลดเข้าฟอร์มนี้:");
            AppendList(builder, Ignored, "  ");
            if (Ignored.Count == 0) builder.AppendLine("  - ไม่มี");
            builder.AppendLine();
            builder.AppendLine("ช่องที่ต้นทางว่าง (จะเขียนทับปลายทางเป็นค่าว่าง):");
            AppendList(builder, Blank, "  ");
            if (Blank.Count == 0) builder.AppendLine("  - ไม่มี");
            builder.AppendLine();
            builder.AppendLine("ข้อมูลร่วมที่ไม่มีใน Template ต้นทาง (ปลายทางจะไม่ถูกเปลี่ยน):");
            AppendList(builder, Unavailable, "  ");
            if (Unavailable.Count == 0) builder.AppendLine("  - ไม่มี");
            return builder.ToString();
        }

        private static void AppendList(StringBuilder builder, IEnumerable<string> values, string indent)
        {
            foreach (var value in values) builder.AppendLine(indent + "- " + value);
        }
    }

    internal static class TemplateTransferService
    {
        private static readonly string[] CommonKeys =
        {
            "school.name", "school.district",
            "school.director.prefix", "school.director.given", "school.director.surname",
            "school.supply.prefix", "school.supply.given", "school.supply.surname",
            "school.headSupply.prefix", "school.headSupply.given", "school.headSupply.surname",
            "employee.prefix", "employee.given", "employee.surname", "employee.position", "employee.nationalId",
            "employee.birth.day", "employee.birth.month", "employee.birth.year", "employee.age",
            "employee.nationality", "employee.race", "employee.religion",
            "employee.idIssueDistrict", "employee.idIssueProvince", "employee.idIssue.day", "employee.idIssue.month", "employee.idIssue.year",
            "employee.idExpiry.day", "employee.idExpiry.month", "employee.idExpiry.year",
            "employee.educationLevel", "employee.qualification",
            "employee.houseNumber", "employee.road", "employee.subdistrict", "employee.district", "employee.province",
            "committee.0.prefix", "committee.0.given", "committee.0.surname",
            "committee.1.prefix", "committee.1.given", "committee.1.surname",
            "committee.2.prefix", "committee.2.given", "committee.2.surname"
        };

        private static readonly Dictionary<string, string> ProcurementPositionToPayroll = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "admin-9000", "ธุรการโรงเรียน 9,000" },
            { "admin-15000", "ธุรการโรงเรียน 15,000" },
            { "janitor", "นักการภารโรง" },
            { "disabled-care", "พี่เลี้ยงเด็กพิการ" },
            { "dormitory", "ครูพักนอน" },
            { "dream-teacher", "ครูพี่เลี้ยงสานฝันฯ" }
        };

        private static readonly Dictionary<string, string> PayrollPositionToProcurement =
            ProcurementPositionToPayroll.ToDictionary(x => x.Value, x => x.Key, StringComparer.Ordinal);

        private static readonly Dictionary<string, string> PayrollPositionAliasesToProcurement = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "เจ้าหน้าที่ธุรการ 9,000", "admin-9000" },
            { "เจ้าหน้าที่ธุรการ 15,000", "admin-15000" }
        };

        public static TemplateTransferItem FromPayroll(string id, string name, string note, string updatedAt, string lastMonth, string lastYear, string lastAt, Dictionary<string, string> data)
        {
            var item = new TemplateTransferItem
            {
                SourceModule = DocumentModule.Payroll,
                Id = id ?? "",
                Name = name ?? "",
                Note = note ?? "",
                UpdatedAt = updatedAt ?? "",
                LastGeneratedFiscalMonth = lastMonth ?? "",
                LastGeneratedFiscalYear = lastYear ?? "",
                LastGeneratedAt = lastAt ?? ""
            };
            AddPayrollCommon(item, data);
            AddPayrollCombinedPerson(item, "committee.0", data, "{กรรมการA}");
            AddPayrollCombinedPerson(item, "committee.1", data, "{กรรมการB}");
            AddPayrollCombinedPerson(item, "committee.2", data, "{กรรมการC}");
            Add(item.Values, "payroll.period", Get(data, "__fiscalMonth") + " " + Get(data, "__fiscalYear"));
            Add(item.Values, "payroll.orderNumber", Get(data, "{ใบสั่งจ้าง}"));
            Add(item.Values, "payroll.orderDate", Get(data, "__orderDay") + " " + Get(data, "__orderMonth") + " " + Get(data, "__orderYear"));
            Add(item.Values, "payroll.finance", Get(data, "__hasHeadFinance"));
            Add(item.Values, "payroll.finance.prefix", Get(data, "{คำนำหน้าการเงิน}"));
            Add(item.Values, "payroll.finance.given", Get(data, "{ชื่อการเงิน}"));
            Add(item.Values, "payroll.finance.surname", Get(data, "{นามสกุลการเงิน}"));
            Add(item.Values, "payroll.headFinance", Get(data, "{โซนหัวหน้าการเงิน}"));
            return item;
        }

        public static TemplateTransferItem FromProcurement(SavedTemplateSnapshot snapshot)
        {
            snapshot = snapshot ?? new SavedTemplateSnapshot();
            var record = snapshot.Record ?? WorkingRecord.CreateEmpty();
            var item = new TemplateTransferItem
            {
                SourceModule = DocumentModule.Procurement,
                Id = snapshot.Id ?? "",
                Name = snapshot.Name ?? "",
                Note = snapshot.Note ?? "",
                UpdatedAt = snapshot.UpdatedAt ?? "",
                LastGeneratedFiscalMonth = snapshot.LastGeneratedFiscalMonth ?? "",
                LastGeneratedFiscalYear = snapshot.LastGeneratedFiscalYear ?? "",
                LastGeneratedAt = snapshot.LastGeneratedAt ?? ""
            };
            AddProcurementCommon(item.Values, record);
            foreach (var key in CommonKeys) item.AvailableKeys.Add(key);
            var payrollPosition = MapProcurementPositionToPayroll(record.Procurement == null ? "" : record.Procurement.PositionId);
            Add(item.Values, "employee.position", payrollPosition);
            if (string.IsNullOrWhiteSpace(payrollPosition)) item.AvailableKeys.Remove("employee.position");
            Add(item.Values, "procurement.position", record.Procurement == null ? "" : record.Procurement.PositionId);
            Add(item.Values, "procurement.teaching", record.Procurement != null && record.Procurement.HasTeaching ? "มีงานสอน" : "");
            Add(item.Values, "procurement.twoSchools", record.Procurement != null && record.Procurement.WorksAtTwoSchools ? "ทำงาน 2 โรงเรียน" : "");
            Add(item.Values, "procurement.secondSchool", record.Procurement == null ? "" : record.Procurement.SecondSchoolName);
            Add(item.Values, "procurement.specificationOrder", record.Procurement == null ? "" : record.Procurement.SpecificationOrder);
            if (record.Committee != null)
            {
                for (var i = 0; i < Math.Min(3, record.Committee.Length); i++)
                {
                    var member = record.Committee[i] ?? new CommitteeMemberRecord();
                    Add(item.Values, "committee." + i + ".prefix", member.Prefix);
                    Add(item.Values, "committee." + i + ".given", member.GivenName);
                    Add(item.Values, "committee." + i + ".surname", member.Surname);
                    Add(item.Values, "committee." + i + ".position", member.Position);
                }
            }
            if (record.Procurement != null && record.Procurement.SelectedDocuments != null)
            {
                Add(item.Values, "procurement.documents", string.Join(", ", record.Procurement.SelectedDocuments.ToArray()));
            }
            return item;
        }

        public static List<TemplateTransferItem> LoadPayrollTemplates()
        {
            var result = new List<TemplateTransferItem>();
            var paths = new[]
            {
                SidecontractPaths.SavedTemplatesPathFor(DocumentModule.Payroll),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "saved_templates.json")
            };
            var path = paths.FirstOrDefault(File.Exists);
            if (path == null) return result;
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var root = serializer.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("templates")) return result;
                var templates = root["templates"] as object[];
                if (templates == null) return result;
                foreach (var raw in templates)
                {
                    var template = raw as Dictionary<string, object>;
                    if (template == null) continue;
                    var data = new Dictionary<string, string>(StringComparer.Ordinal);
                    var rawData = template.ContainsKey("data") ? template["data"] as Dictionary<string, object> : null;
                    if (rawData != null)
                    {
                        foreach (var pair in rawData) data[pair.Key] = AsString(pair.Value);
                    }
                    var item = FromPayroll(
                        Get(template, "id"), Get(template, "name"), Get(template, "note"), Get(template, "updatedAt"),
                        Get(template, "lastGeneratedFiscalMonth"), Get(template, "lastGeneratedFiscalYear"), Get(template, "lastGeneratedAt"), data);
                    if (!string.IsNullOrWhiteSpace(item.Name)) result.Add(item);
                }
            }
            catch
            {
                return new List<TemplateTransferItem>();
            }
            return result;
        }

        public static List<TemplateTransferItem> LoadProcurementTemplates()
        {
            try
            {
                var data = new SidecontractStore(DocumentModule.Procurement).Load();
                return (data.Templates ?? new List<SavedTemplateSnapshot>()).Select(FromProcurement).Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToList();
            }
            catch
            {
                return new List<TemplateTransferItem>();
            }
        }

        public static Dictionary<string, string> GetImportableValues(TemplateTransferItem item)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in CommonKeys)
            {
                if (item != null && item.AvailableKeys.Contains(key)) result[key] = item.Values.ContainsKey(key) ? Clean(item.Values[key]) : "";
            }
            return result;
        }

        public static TemplateImportReport BuildReport(TemplateTransferItem item, DocumentModule target)
        {
            var report = new TemplateImportReport();
            var common = GetImportableValues(item);
            foreach (var key in CommonKeys)
            {
                string value;
                if (!common.TryGetValue(key, out value))
                {
                    report.Unavailable.Add(FriendlyLabel(key));
                    continue;
                }
                if (key == "employee.position"
                    && target == DocumentModule.Procurement
                    && item != null
                    && item.SourceModule == DocumentModule.Payroll
                    && value.Length > 0
                    && string.IsNullOrWhiteSpace(MapPayrollPositionToProcurement(value)))
                {
                    report.Ignored.Add(FriendlyLabel(key) + " (" + value + " — ไม่มีรายการรองรับในจัดซื้อฯ)");
                    continue;
                }
                if (value.Length == 0) report.Blank.Add(FriendlyLabel(key));
                else report.Imported.Add(FriendlyLabel(key));
            }
            if (item != null && item.SourceModule != target)
            {
                foreach (var pair in item.Values.Where(x => !IsCommonKey(x.Key)))
                {
                    report.Ignored.Add(FriendlyLabel(pair.Key));
                }
            }
            return report;
        }

        private static void AddPayrollCommon(TemplateTransferItem item, Dictionary<string, string> data)
        {
            AddPayroll(item, "school.name", data, "{ชื่อโรงเรียน}");
            AddPayroll(item, "school.district", data, "{ชื่อเขต}");
            AddPerson(item, "school.director", data, "ผอ");
            AddPerson(item, "school.supply", data, "พัสดุ");
            AddPerson(item, "school.headSupply", data, "หพัสดุ");
            AddPayroll(item, "employee.prefix", data, "{คำนำหน้าลูกจ้าง}");
            AddPayroll(item, "employee.given", data, "{ชื่อลูกจ้าง}");
            AddPayroll(item, "employee.surname", data, "{นามสกุลลูกจ้าง}");
            AddPayroll(item, "employee.position", data, "{ตำแหน่ง}");
            AddPayroll(item, "employee.nationalId", data, "{เลขประจำตัว}");
            AddPayroll(item, "employee.birth.day", data, "{เกิดวันที่}");
            AddPayroll(item, "employee.birth.month", data, "{เดือนเกิด}");
            AddPayroll(item, "employee.birth.year", data, "{ปีเกิด}");
            AddPayroll(item, "employee.age", data, "{อายุลูกจ้าง}");
            AddPayroll(item, "employee.nationality", data, "{สัญชาติ}");
            AddPayroll(item, "employee.race", data, "{เชื้อชาติ}");
            AddPayroll(item, "employee.religion", data, "{ศาสนา}");
            AddPayroll(item, "employee.idIssueDistrict", data, "{ออกอำเภอ}");
            AddPayroll(item, "employee.idIssueProvince", data, "{ออกจังหวัด}");
            AddPayroll(item, "employee.idIssue.day", data, "{วันที่ออกบัตร}");
            AddPayroll(item, "employee.idIssue.month", data, "{เดือนออกบัตร}");
            AddPayroll(item, "employee.idIssue.year", data, "{ปีออกบัตร}");
            AddPayroll(item, "employee.idExpiry.day", data, "{วันบัตรหมดอายุ}");
            AddPayroll(item, "employee.idExpiry.month", data, "{เดือนบัตรหมดอายุ}");
            AddPayroll(item, "employee.idExpiry.year", data, "{ปีบัตรหมดอายุ}");
            AddPayroll(item, "employee.educationLevel", data, "{สำเร็จการศึกษาระดับ}");
            AddPayroll(item, "employee.qualification", data, "{คุณวุฒิการศึกษา}");
            AddPayroll(item, "employee.houseNumber", data, "{บ้านเลขที่ลูกจ้าง}");
            AddPayroll(item, "employee.road", data, "{ถนนลูกจ้าง}");
            AddPayroll(item, "employee.subdistrict", data, "{ตำบลลูกจ้าง}");
            AddPayroll(item, "employee.district", data, "{อำเภอลูกจ้าง}");
            AddPayroll(item, "employee.province", data, "{จังหวัดลูกจ้าง}");
        }

        private static void AddProcurementCommon(Dictionary<string, string> values, WorkingRecord record)
        {
            record = record ?? WorkingRecord.CreateEmpty();
            Add(values, "school.name", record.School == null ? "" : record.School.Name);
            Add(values, "school.district", record.School == null ? "" : record.School.District);
            AddPerson(values, "school.director", record.School == null ? null : record.School.Director);
            AddPerson(values, "school.supply", record.School == null ? null : record.School.SupplyOfficer);
            AddPerson(values, "school.headSupply", record.School == null ? null : record.School.HeadSupplyOfficer);
            var employee = record.Employee ?? new EmployeeRecord();
            Add(values, "employee.prefix", employee.Prefix);
            Add(values, "employee.given", employee.GivenName);
            Add(values, "employee.surname", employee.Surname);
            Add(values, "employee.nationalId", employee.NationalId);
            Add(values, "employee.birth.day", employee.BirthDay);
            Add(values, "employee.birth.month", employee.BirthMonth);
            Add(values, "employee.birth.year", employee.BirthYear);
            Add(values, "employee.age", employee.Age);
            Add(values, "employee.nationality", employee.Nationality);
            Add(values, "employee.race", employee.Race);
            Add(values, "employee.religion", employee.Religion);
            Add(values, "employee.idIssueDistrict", employee.IdIssueDistrict);
            Add(values, "employee.idIssueProvince", employee.IdIssueProvince);
            Add(values, "employee.idIssue.day", employee.IdIssueDay);
            Add(values, "employee.idIssue.month", employee.IdIssueMonth);
            Add(values, "employee.idIssue.year", employee.IdIssueYear);
            Add(values, "employee.idExpiry.day", employee.IdExpiryDay);
            Add(values, "employee.idExpiry.month", employee.IdExpiryMonth);
            Add(values, "employee.idExpiry.year", employee.IdExpiryYear);
            Add(values, "employee.educationLevel", employee.EducationLevel);
            Add(values, "employee.qualification", employee.Qualification);
            Add(values, "employee.houseNumber", employee.HouseNumber);
            Add(values, "employee.road", employee.Road);
            Add(values, "employee.subdistrict", employee.Subdistrict);
            Add(values, "employee.district", employee.District);
            Add(values, "employee.province", employee.Province);
        }

        private static void AddPayroll(TemplateTransferItem item, string key, Dictionary<string, string> data, string tag)
        {
            if (data == null || !data.ContainsKey(tag)) return;
            item.AvailableKeys.Add(key);
            item.Values[key] = Clean(Get(data, tag));
        }

        private static void AddPerson(TemplateTransferItem item, string key, Dictionary<string, string> data, string suffix)
        {
            AddPayroll(item, key + ".prefix", data, "{คำนำหน้า" + suffix + "}");
            AddPayroll(item, key + ".given", data, "{ชื่อ" + suffix + "}");
            AddPayroll(item, key + ".surname", data, "{นามสกุล" + suffix + "}");
        }

        private static void AddPayrollCombinedPerson(TemplateTransferItem item, string key, Dictionary<string, string> data, string tag)
        {
            if (data == null || !data.ContainsKey(tag)) return;
            var person = SplitPersonName(Get(data, tag));
            Add(item.Values, key + ".prefix", person.Prefix);
            Add(item.Values, key + ".given", person.GivenName);
            Add(item.Values, key + ".surname", person.Surname);
            item.AvailableKeys.Add(key + ".prefix");
            item.AvailableKeys.Add(key + ".given");
            item.AvailableKeys.Add(key + ".surname");
        }

        public static string MapProcurementPositionToPayroll(string positionId)
        {
            string label;
            return positionId != null && ProcurementPositionToPayroll.TryGetValue(positionId, out label) ? label : "";
        }

        public static string MapPayrollPositionToProcurement(string position)
        {
            string positionId;
            var cleaned = Clean(position);
            if (PayrollPositionToProcurement.TryGetValue(cleaned, out positionId)) return positionId;
            return PayrollPositionAliasesToProcurement.TryGetValue(cleaned, out positionId) ? positionId : "";
        }

        private static PersonParts SplitPersonName(string value)
        {
            var fullName = Clean(value);
            var prefixes = new[] { "ว่าที่ร้อยตรี", "นางสาว", "ดร.", "นาย", "นาง" };
            var prefix = prefixes.FirstOrDefault(x => fullName.StartsWith(x, StringComparison.Ordinal)) ?? "";
            if (prefix.Length > 0) fullName = fullName.Substring(prefix.Length).TrimStart();
            var parts = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return new PersonParts
            {
                Prefix = prefix,
                GivenName = parts.Length > 0 ? parts[0] : "",
                Surname = parts.Length > 1 ? string.Join(" ", parts.Skip(1).ToArray()) : ""
            };
        }

        private sealed class PersonParts
        {
            public string Prefix;
            public string GivenName;
            public string Surname;
        }

        private static void AddPerson(Dictionary<string, string> values, string key, PersonRecord person)
        {
            person = person ?? new PersonRecord();
            Add(values, key + ".prefix", person.Prefix);
            Add(values, key + ".given", person.GivenName);
            Add(values, key + ".surname", person.Surname);
        }

        private static void Add(Dictionary<string, string> values, string key, string value)
        {
            values[key] = Clean(value);
        }

        private static string Get(Dictionary<string, string> data, string key)
        {
            string value;
            return data != null && data.TryGetValue(key, out value) ? value : "";
        }

        private static string Get(Dictionary<string, object> data, string key)
        {
            object value;
            return data != null && data.TryGetValue(key, out value) ? AsString(value) : "";
        }

        private static string AsString(object value)
        {
            return value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string Clean(string value)
        {
            value = (value ?? "").Trim();
            if (value == "--" || value == "-- ไม่ระบุ --" || value == "-- เลือกตำแหน่ง --" || value.StartsWith("......", StringComparison.Ordinal)) return "";
            return value;
        }

        private static bool IsCommonKey(string key)
        {
            return CommonKeys.Contains(key);
        }

        private static string FriendlyLabel(string key)
        {
            var labels = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "school.name", "ชื่อโรงเรียน" },
                { "school.district", "เขตพื้นที่การศึกษา" },
                { "school.director.prefix", "คำนำหน้าผู้อำนวยการ" },
                { "school.director.given", "ชื่อผู้อำนวยการ" },
                { "school.director.surname", "นามสกุลผู้อำนวยการ" },
                { "school.supply.prefix", "คำนำหน้าเจ้าหน้าที่พัสดุ" },
                { "school.supply.given", "ชื่อเจ้าหน้าที่พัสดุ" },
                { "school.supply.surname", "นามสกุลเจ้าหน้าที่พัสดุ" },
                { "school.headSupply.prefix", "คำนำหน้าหัวหน้าเจ้าหน้าที่พัสดุ" },
                { "school.headSupply.given", "ชื่อหัวหน้าเจ้าหน้าที่พัสดุ" },
                { "school.headSupply.surname", "นามสกุลหัวหน้าเจ้าหน้าที่พัสดุ" },
                { "employee.position", "ตำแหน่ง" },
                { "employee.prefix", "คำนำหน้าลูกจ้าง" },
                { "employee.given", "ชื่อลูกจ้าง" },
                { "employee.surname", "นามสกุลลูกจ้าง" },
                { "employee.nationalId", "เลขประจำตัวประชาชน" },
                { "employee.birth.day", "วันเกิด" },
                { "employee.birth.month", "เดือนเกิด" },
                { "employee.birth.year", "ปีเกิด" },
                { "employee.age", "อายุ" },
                { "employee.nationality", "สัญชาติ" },
                { "employee.race", "เชื้อชาติ" },
                { "employee.religion", "ศาสนา" },
                { "employee.idIssueDistrict", "อำเภอที่ออกบัตร" },
                { "employee.idIssueProvince", "จังหวัดที่ออกบัตร" },
                { "employee.idIssue.day", "วันออกบัตร" },
                { "employee.idIssue.month", "เดือนออกบัตร" },
                { "employee.idIssue.year", "ปีออกบัตร" },
                { "employee.idExpiry.day", "วันบัตรหมดอายุ" },
                { "employee.idExpiry.month", "เดือนบัตรหมดอายุ" },
                { "employee.idExpiry.year", "ปีบัตรหมดอายุ" },
                { "employee.educationLevel", "ระดับการศึกษา" },
                { "employee.qualification", "คุณวุฒิการศึกษา" },
                { "employee.houseNumber", "บ้านเลขที่" },
                { "employee.road", "ถนน" },
                { "employee.subdistrict", "ตำบล" },
                { "employee.district", "อำเภอ" },
                { "employee.province", "จังหวัด" },
                { "payroll.period", "งวด/ปีงบประมาณ Payroll" },
                { "payroll.orderNumber", "เลขที่ใบสั่งจ้าง Payroll" },
                { "payroll.orderDate", "วันที่ใบสั่งจ้าง Payroll" },
                { "payroll.finance", "ผู้อนุมัติการเงิน Payroll" },
                { "payroll.finance.prefix", "คำนำหน้าเจ้าหน้าที่การเงิน Payroll" },
                { "payroll.finance.given", "ชื่อเจ้าหน้าที่การเงิน Payroll" },
                { "payroll.finance.surname", "นามสกุลเจ้าหน้าที่การเงิน Payroll" },
                { "payroll.headFinance", "หัวหน้าการเงิน Payroll" },
                { "procurement.position", "ตำแหน่งจัดซื้อจัดจ้าง" },
                { "procurement.teaching", "ตัวเลือกมีงานสอน" },
                { "procurement.twoSchools", "ตัวเลือกทำงาน 2 โรงเรียน" },
                { "procurement.secondSchool", "ชื่อโรงเรียนที่สอง" },
                { "procurement.specificationOrder", "เลขที่คำสั่งแต่งตั้ง TORและตรวจรับ" },
                { "procurement.documents", "ชุดเอกสารจัดซื้อจัดจ้าง" }
            };
            string label;
            if (labels.TryGetValue(key, out label)) return label;
            if (key.StartsWith("committee.", StringComparison.Ordinal))
            {
                var parts = key.Split('.');
                var index = parts.Length > 1 ? (int.Parse(parts[1], CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture) : "";
                var field = parts.Length > 2 ? parts[2] : "";
                var fieldLabel = field == "prefix" ? "คำนำหน้า" : field == "given" ? "ชื่อ" : field == "surname" ? "นามสกุล" : "ตำแหน่ง";
                return "กรรมการ " + index + " " + fieldLabel;
            }
            return key;
        }
    }

    internal sealed class CrossTemplatePickerDialog : Form
    {
        private readonly ListBox currentList = new ListBox();
        private readonly ListBox otherList = new ListBox();
        private readonly Label preview = new Label();
        private readonly Button useButton = new Button();
        public TemplateTransferItem Selected { get; private set; }

        public CrossTemplatePickerDialog(DocumentModule target, IEnumerable<TemplateTransferItem> procurementTemplates, IEnumerable<TemplateTransferItem> payrollTemplates)
        {
            Text = "โหลด Template : เลือกจากโปรแกรม";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(900, 600);
            BackColor = Color.FromArgb(243, 246, 248);
            Font = new Font("Segoe UI", 10f);

            Controls.Add(new Label { Text = "โหลด Template", Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(20, 16), Size = new Size(300, 32) });
            Controls.Add(new Label { Text = "เลือกข้อมูลเดิมจากโปรแกรมใดก็ได้", ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(22, 48), Size = new Size(500, 22) });

            BuildListCard(currentList, "นำเข้าจากชุดจัดซื้อจัดจ้างและสัญญา", Color.FromArgb(15, 118, 110), new Point(20, 82), procurementTemplates);
            BuildListCard(otherList, "โหลดจากข้อมูลเบิกเงินเดือนเดิม", Color.FromArgb(31, 94, 140), new Point(460, 82), payrollTemplates);
            currentList.SelectedIndexChanged += delegate { if (currentList.SelectedIndex >= 0) otherList.ClearSelected(); UpdatePreview(); };
            otherList.SelectedIndexChanged += delegate { if (otherList.SelectedIndex >= 0) currentList.ClearSelected(); UpdatePreview(); };

            preview.Location = new Point(20, 500);
            preview.Size = new Size(700, 54);
            preview.ForeColor = Color.FromArgb(100, 116, 139);
            preview.AutoEllipsis = true;
            Controls.Add(preview);

            useButton.Text = "เลือก Template";
            useButton.Location = new Point(680, 552);
            useButton.Size = new Size(120, 32);
            useButton.BackColor = Color.FromArgb(31, 94, 140);
            useButton.ForeColor = Color.White;
            useButton.FlatStyle = FlatStyle.Flat;
            useButton.FlatAppearance.BorderSize = 0;
            useButton.Click += delegate { SelectTemplate(); };
            Controls.Add(useButton);
            var cancel = new Button { Text = "ยกเลิก", Location = new Point(810, 552), Size = new Size(70, 32), DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };
            cancel.FlatAppearance.BorderColor = Color.FromArgb(214, 224, 232);
            Controls.Add(cancel);
            AcceptButton = useButton;
            CancelButton = cancel;
            UpdatePreview();
        }

        private void BuildListCard(ListBox list, string title, Color accent, Point location, IEnumerable<TemplateTransferItem> values)
        {
            var card = new Panel { Location = location, Size = new Size(420, 390), BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            card.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(5, 38), BackColor = accent });
            card.Controls.Add(new Label { Text = title, Location = new Point(5, 0), Size = new Size(413, 38), BackColor = Color.FromArgb(248, 250, 252), ForeColor = Color.FromArgb(30, 41, 59), Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Padding = new Padding(14, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft });
            list.Location = new Point(12, 50);
            list.Size = new Size(394, 326);
            list.BorderStyle = BorderStyle.FixedSingle;
            list.Font = new Font("Segoe UI", 10f);
            list.DisplayMember = "Name";
            list.DataSource = (values ?? new List<TemplateTransferItem>()).ToList();
            card.Controls.Add(list);
            Controls.Add(card);
        }

        private void UpdatePreview()
        {
            var item = GetSelected();
            if (item == null)
            {
                preview.Text = "เลือก Template เพื่อดูหมายเหตุและขอบเขตข้อมูลที่จะโหลด";
                useButton.Text = "เลือก Template";
                return;
            }
            preview.Text = item.SourceLabel + "  |  " + (string.IsNullOrWhiteSpace(item.Note) ? "ไม่มีหมายเหตุ" : item.Note);
            useButton.Text = "โหลด Template";
        }

        private TemplateTransferItem GetSelected()
        {
            return currentList.SelectedItem as TemplateTransferItem ?? otherList.SelectedItem as TemplateTransferItem;
        }

        private void SelectTemplate()
        {
            Selected = GetSelected();
            if (Selected == null)
            {
                MessageBox.Show("กรุณาเลือก Template", "ยังไม่ได้เลือก", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class CrossTemplateImportNoticeDialog : Form
    {
        public CrossTemplateImportNoticeDialog(TemplateTransferItem item, DocumentModule target, TemplateImportReport report)
        {
            Text = "ตรวจสอบการโหลด Template";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(760, 580);
            BackColor = Color.FromArgb(243, 246, 248);
            Font = new Font("Segoe UI", 10f);
            Controls.Add(new Label { Text = "กำลังจะเขียนทับข้อมูลร่วมทั้งหมด", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = Color.FromArgb(30, 41, 59), Location = new Point(20, 18), Size = new Size(600, 30) });
            Controls.Add(new Label { Text = "Template: " + item.Name, ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(22, 50), Size = new Size(700, 22) });
            var details = new TextBox { Location = new Point(20, 82), Size = new Size(720, 420), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, Text = report.ToDisplayText(item, target) };
            Controls.Add(details);
            var ok = new Button { Text = "โหลดและเขียนทับ", Location = new Point(540, 528), Size = new Size(130, 32), BackColor = target == DocumentModule.Payroll ? Color.FromArgb(31, 94, 140) : Color.FromArgb(15, 118, 110), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            ok.FlatAppearance.BorderSize = 0;
            ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok);
            var cancel = new Button { Text = "ยกเลิก", Location = new Point(680, 528), Size = new Size(60, 32), DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };
            cancel.FlatAppearance.BorderColor = Color.FromArgb(214, 224, 232);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }
    }
}
