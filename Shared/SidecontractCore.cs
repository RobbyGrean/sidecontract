using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace Sidecontract
{
    internal enum DocumentModule
    {
        Payroll,
        Procurement
    }

    internal static class SidecontractPaths
    {
        public const string AppId = "sidecontract";
        public const int CurrentSchemaVersion = 1;

        public static string UserRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppId); }
        }

        public static string DataRoot
        {
            get { return Path.Combine(UserRoot, "Data"); }
        }

        public static string ModuleDataRoot(DocumentModule module)
        {
            return Path.Combine(DataRoot, module == DocumentModule.Payroll ? "Payroll" : "Procurement");
        }

        public static string TemplateRoot(DocumentModule module)
        {
            return Path.Combine(UserRoot, "Template", module == DocumentModule.Payroll ? "Payroll" : "Procurement");
        }

        public static string OutputRoot(DocumentModule module)
        {
            return Path.Combine(UserRoot, "Output", module == DocumentModule.Payroll ? "เงินเดือน" : "เอกสารจัดจ้าง");
        }

        public static string ShippedRoot
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string ShippedTemplateRoot(DocumentModule module)
        {
            return Path.Combine(ShippedRoot, "Template", module == DocumentModule.Payroll ? "Payroll" : "Procurement");
        }

        public static string ShippedConfigRoot(DocumentModule module)
        {
            return Path.Combine(ShippedRoot, "Config", module == DocumentModule.Payroll ? "Payroll" : "Procurement");
        }

        public static string EnsureUserTemplateRoot(DocumentModule module)
        {
            var target = TemplateRoot(module);
            Directory.CreateDirectory(target);
            var shipped = ShippedTemplateRoot(module);
            if (!Directory.Exists(shipped)) return target;

            foreach (var sourceFile in Directory.GetFiles(shipped, "*", SearchOption.AllDirectories))
            {
                var relative = sourceFile.Substring(shipped.TrimEnd(Path.DirectorySeparatorChar).Length + 1);
                var targetFile = ResolveUnder(target, relative);
                var targetDirectory = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrWhiteSpace(targetDirectory)) Directory.CreateDirectory(targetDirectory);
                if (!File.Exists(targetFile)
                    || File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(targetFile))
                {
                    // Ship updated official templates without overwriting a newer user-edited copy.
                    File.Copy(sourceFile, targetFile, true);
                }
            }
            return target;
        }

        public static string SavedTemplatesPath
        {
            get { return Path.Combine(DataRoot, "saved_templates.json"); }
        }

        public static string SavedTemplatesPathFor(DocumentModule module)
        {
            return Path.Combine(ModuleDataRoot(module), "saved_templates.json");
        }

        public static string PreferencesPath
        {
            get { return Path.Combine(DataRoot, "preferences.json"); }
        }

        public static string ResolveUnder(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            {
                throw new InvalidDataException("Path must be relative: " + relativePath);
            }

            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Path escapes its module root: " + relativePath);
            }
            return fullPath;
        }
    }

    internal class PersonRecord
    {
        public string Prefix { get; set; }
        public string GivenName { get; set; }
        public string Surname { get; set; }
        public string Position { get; set; }

        public PersonRecord()
        {
            Prefix = "";
            GivenName = "";
            Surname = "";
            Position = "";
        }
    }

    internal sealed class CommitteeMemberRecord : PersonRecord
    {
    }

    internal sealed class SchoolRecord
    {
        public string Name { get; set; }
        public string District { get; set; }
        public string Subdistrict { get; set; }
        public string Amphoe { get; set; }
        public string Province { get; set; }
        public PersonRecord Director { get; set; }
        public PersonRecord SupplyOfficer { get; set; }
        public PersonRecord HeadSupplyOfficer { get; set; }

        public SchoolRecord()
        {
            Name = "";
            District = "";
            Subdistrict = "";
            Amphoe = "";
            Province = "";
            Director = new PersonRecord();
            SupplyOfficer = new PersonRecord();
            HeadSupplyOfficer = new PersonRecord();
        }
    }

    internal sealed class EmployeeRecord
    {
        public string Prefix { get; set; }
        public string GivenName { get; set; }
        public string Surname { get; set; }
        public string NationalId { get; set; }
        public string BirthDay { get; set; }
        public string BirthMonth { get; set; }
        public string BirthYear { get; set; }
        public string Age { get; set; }
        public string Nationality { get; set; }
        public string Race { get; set; }
        public string Religion { get; set; }
        public string IdIssueDistrict { get; set; }
        public string IdIssueProvince { get; set; }
        public string IdIssueDay { get; set; }
        public string IdIssueMonth { get; set; }
        public string IdIssueYear { get; set; }
        public string IdExpiryDay { get; set; }
        public string IdExpiryMonth { get; set; }
        public string IdExpiryYear { get; set; }
        public string EducationLevel { get; set; }
        public string Qualification { get; set; }
        public string HouseNumber { get; set; }
        public string Road { get; set; }
        public string Subdistrict { get; set; }
        public string District { get; set; }
        public string Province { get; set; }

        public EmployeeRecord()
        {
            Prefix = "";
            GivenName = "";
            Surname = "";
            NationalId = "";
            BirthDay = "";
            BirthMonth = "";
            BirthYear = "";
            Age = "";
            Nationality = "";
            Race = "";
            Religion = "";
            IdIssueDistrict = "";
            IdIssueProvince = "";
            IdIssueDay = "";
            IdIssueMonth = "";
            IdIssueYear = "";
            IdExpiryDay = "";
            IdExpiryMonth = "";
            IdExpiryYear = "";
            EducationLevel = "";
            Qualification = "";
            HouseNumber = "";
            Road = "";
            Subdistrict = "";
            District = "";
            Province = "";
        }
    }

    internal sealed class PayrollState
    {
        public string PositionId { get; set; }
        public string FiscalMonth { get; set; }
        public string FiscalYear { get; set; }
        public string OrderNumber { get; set; }
        public string OrderDate { get; set; }
        public bool HasHeadFinance { get; set; }
        public PersonRecord HeadFinance { get; set; }
        public List<string> SelectedDocuments { get; set; }
        public string QuickLoadedTemplateId { get; set; }
        public string LastGeneratedFiscalMonth { get; set; }
        public string LastGeneratedFiscalYear { get; set; }
        public string LastGeneratedAt { get; set; }

        public PayrollState()
        {
            PositionId = "";
            FiscalMonth = "";
            FiscalYear = "";
            OrderNumber = "";
            OrderDate = "";
            HasHeadFinance = false;
            HeadFinance = new PersonRecord();
            SelectedDocuments = new List<string>();
            QuickLoadedTemplateId = "";
            LastGeneratedFiscalMonth = "";
            LastGeneratedFiscalYear = "";
            LastGeneratedAt = "";
        }
    }

    internal sealed class ProcurementState
    {
        public string PositionId { get; set; }
        public bool HasTeaching { get; set; }
        public bool WorksAtTwoSchools { get; set; }
        public string SecondSchoolName { get; set; }
        public string SpecificationOrder { get; set; }
        public string ContractNumber { get; set; }
        public string ContractDay { get; set; }
        public string ContractMonth { get; set; }
        public string ContractYear { get; set; }
        public string Bank { get; set; }
        public string BankBranch { get; set; }
        public string BankAccountName { get; set; }
        public string BankAccountNumber { get; set; }
        public List<string> SelectedDocuments { get; set; }

        public ProcurementState()
        {
            PositionId = "";
            HasTeaching = false;
            WorksAtTwoSchools = false;
            SecondSchoolName = "";
            SpecificationOrder = "";
            ContractNumber = "";
            ContractDay = "";
            ContractMonth = "";
            ContractYear = "";
            Bank = "";
            BankBranch = "";
            BankAccountName = "";
            BankAccountNumber = "";
            SelectedDocuments = new List<string>();
        }
    }

    internal sealed class WorkingRecord
    {
        public int SchemaVersion { get; set; }
        public string RecordId { get; set; }
        public SchoolRecord School { get; set; }
        public EmployeeRecord Employee { get; set; }
        public CommitteeMemberRecord[] Committee { get; set; }
        public PayrollState Payroll { get; set; }
        public ProcurementState Procurement { get; set; }

        public WorkingRecord()
        {
            SchemaVersion = SidecontractPaths.CurrentSchemaVersion;
            RecordId = Guid.NewGuid().ToString("N");
            School = new SchoolRecord();
            Employee = new EmployeeRecord();
            Committee = new[] { new CommitteeMemberRecord(), new CommitteeMemberRecord(), new CommitteeMemberRecord() };
            Payroll = new PayrollState();
            Procurement = new ProcurementState();
        }

        public static WorkingRecord CreateEmpty()
        {
            return new WorkingRecord();
        }
    }

    internal sealed class SavedTemplateSnapshot
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Note { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string LastGeneratedFiscalMonth { get; set; }
        public string LastGeneratedFiscalYear { get; set; }
        public string LastGeneratedAt { get; set; }
        public WorkingRecord Record { get; set; }

        public SavedTemplateSnapshot()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "";
            Note = "";
            CreatedAt = "";
            UpdatedAt = "";
            LastGeneratedFiscalMonth = "";
            LastGeneratedFiscalYear = "";
            LastGeneratedAt = "";
            Record = WorkingRecord.CreateEmpty();
        }
    }

    internal sealed class SidecontractStoreData
    {
        public int SchemaVersion { get; set; }
        public List<SavedTemplateSnapshot> Templates { get; set; }
        public List<string> CustomDistricts { get; set; }
        public List<string> CustomPrefixes { get; set; }
        public List<string> CustomQualifications { get; set; }

        public SidecontractStoreData()
        {
            SchemaVersion = SidecontractPaths.CurrentSchemaVersion;
            Templates = new List<SavedTemplateSnapshot>();
            CustomDistricts = new List<string>();
            CustomPrefixes = new List<string>();
            CustomQualifications = new List<string>();
        }
    }

    internal sealed class SidecontractStoreException : Exception
    {
        public SidecontractStoreException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    internal sealed class SidecontractStore
    {
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        private readonly string dataRoot;
        private readonly string legacyDataRoot;

        public SidecontractStore()
            : this(SidecontractPaths.DataRoot, "")
        {
        }

        internal SidecontractStore(string testDataRoot)
            : this(testDataRoot, "")
        {
        }

        internal SidecontractStore(DocumentModule module)
            : this(SidecontractPaths.ModuleDataRoot(module), SidecontractPaths.DataRoot)
        {
        }

        private SidecontractStore(string root, string legacyRoot)
        {
            dataRoot = string.IsNullOrWhiteSpace(root) ? SidecontractPaths.DataRoot : root;
            legacyDataRoot = legacyRoot ?? "";
        }

        public SidecontractStoreData Load()
        {
            var path = Path.Combine(dataRoot, "saved_templates.json");
            if (!File.Exists(path) && !string.IsNullOrWhiteSpace(legacyDataRoot))
            {
                var legacyPath = Path.Combine(legacyDataRoot, "saved_templates.json");
                if (File.Exists(legacyPath)) path = legacyPath;
            }
            if (!File.Exists(path)) return new SidecontractStoreData();

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var data = serializer.Deserialize<SidecontractStoreData>(json);
                if (data == null) throw new InvalidDataException("The saved template file is empty.");
                if (data.SchemaVersion > SidecontractPaths.CurrentSchemaVersion)
                {
                    throw new InvalidDataException("The saved template file uses a newer schema.");
                }
                Normalize(data);
                return data;
            }
            catch (Exception ex)
            {
                throw new SidecontractStoreException("ไม่สามารถอ่านข้อมูล Template ของเอกสาร 346 ได้ ไฟล์เดิมยังคงอยู่", ex);
            }
        }

        public void Save(SidecontractStoreData data)
        {
            if (data == null) throw new ArgumentNullException("data");
            Normalize(data);
            var directory = dataRoot;
            Directory.CreateDirectory(directory);
            var path = Path.Combine(dataRoot, "saved_templates.json");
            var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var backupPath = path + ".bak";

            try
            {
                var json = serializer.Serialize(data);
                File.WriteAllText(tempPath, json, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, backupPath, true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch
                {
                }
                throw new SidecontractStoreException("ไม่สามารถบันทึกข้อมูล Template ของเอกสาร 346 ได้ ข้อมูลในฟอร์มยังคงอยู่", ex);
            }
        }

        public T Clone<T>(T value)
        {
            if (value == null) return default(T);
            return serializer.Deserialize<T>(serializer.Serialize(value));
        }

        private void Normalize(SidecontractStoreData data)
        {
            if (data.SchemaVersion <= 0) data.SchemaVersion = SidecontractPaths.CurrentSchemaVersion;
            if (data.Templates == null) data.Templates = new List<SavedTemplateSnapshot>();
            if (data.CustomDistricts == null) data.CustomDistricts = new List<string>();
            if (data.CustomPrefixes == null) data.CustomPrefixes = new List<string>();
            if (data.CustomQualifications == null) data.CustomQualifications = new List<string>();
            foreach (var template in data.Templates)
            {
                if (template == null) continue;
                if (string.IsNullOrWhiteSpace(template.Id)) template.Id = Guid.NewGuid().ToString("N");
                if (template.Record == null) template.Record = WorkingRecord.CreateEmpty();
                Normalize(template.Record);
            }
        }

        private void Normalize(WorkingRecord record)
        {
            if (record.School == null) record.School = new SchoolRecord();
            if (record.Employee == null) record.Employee = new EmployeeRecord();
            if (record.Committee == null || record.Committee.Length != 3)
            {
                record.Committee = new[] { new CommitteeMemberRecord(), new CommitteeMemberRecord(), new CommitteeMemberRecord() };
            }
            for (var i = 0; i < record.Committee.Length; i++)
            {
                if (record.Committee[i] == null) record.Committee[i] = new CommitteeMemberRecord();
            }
            if (record.Payroll == null) record.Payroll = new PayrollState();
            if (record.Procurement == null) record.Procurement = new ProcurementState();
            if (record.Payroll.HeadFinance == null) record.Payroll.HeadFinance = new PersonRecord();
            if (record.Payroll.SelectedDocuments == null) record.Payroll.SelectedDocuments = new List<string>();
            if (record.Procurement.SelectedDocuments == null) record.Procurement.SelectedDocuments = new List<string>();
            for (var i = 0; i < record.Procurement.SelectedDocuments.Count; i++)
            {
                if (string.Equals(record.Procurement.SelectedDocuments[i], "9. ใบสั่งจ้าง.docx", StringComparison.OrdinalIgnoreCase))
                {
                    record.Procurement.SelectedDocuments[i] = "9. สัญญา.docx";
                }
            }
            if (string.IsNullOrWhiteSpace(record.RecordId)) record.RecordId = Guid.NewGuid().ToString("N");
        }
    }

    internal sealed class SidecontractPreferences
    {
        public int SchemaVersion { get; set; }
        public List<string> CustomDistricts { get; set; }
        public List<string> HiddenDefaultDistricts { get; set; }

        public SidecontractPreferences()
        {
            SchemaVersion = SidecontractPaths.CurrentSchemaVersion;
            CustomDistricts = new List<string>();
            HiddenDefaultDistricts = new List<string>();
        }
    }

    internal sealed class SidecontractPreferencesStore
    {
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public SidecontractPreferences Load()
        {
            var path = SidecontractPaths.PreferencesPath;
            if (!File.Exists(path)) return new SidecontractPreferences();

            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var data = serializer.Deserialize<SidecontractPreferences>(json);
                if (data == null) throw new InvalidDataException("The preferences file is empty.");
                if (data.SchemaVersion > SidecontractPaths.CurrentSchemaVersion)
                {
                    throw new InvalidDataException("The preferences file uses a newer schema.");
                }
                Normalize(data);
                return data;
            }
            catch (Exception ex)
            {
                throw new SidecontractStoreException("ไม่สามารถอ่านค่าตั้งค่ารายชื่อเขตได้ ไฟล์เดิมยังคงอยู่", ex);
            }
        }

        public void Save(SidecontractPreferences data)
        {
            if (data == null) throw new ArgumentNullException("data");
            Normalize(data);
            var directory = SidecontractPaths.DataRoot;
            Directory.CreateDirectory(directory);
            var path = SidecontractPaths.PreferencesPath;
            var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var backupPath = path + ".bak";

            try
            {
                var json = serializer.Serialize(data);
                File.WriteAllText(tempPath, json, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, backupPath, true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch
                {
                }
                throw new SidecontractStoreException("ไม่สามารถบันทึกค่าตั้งค่ารายชื่อเขตได้ ข้อมูลเดิมยังคงอยู่", ex);
            }
        }

        private static void Normalize(SidecontractPreferences data)
        {
            if (data.SchemaVersion <= 0) data.SchemaVersion = SidecontractPaths.CurrentSchemaVersion;
            if (data.CustomDistricts == null) data.CustomDistricts = new List<string>();
            if (data.HiddenDefaultDistricts == null) data.HiddenDefaultDistricts = new List<string>();
            NormalizeList(data.CustomDistricts);
            NormalizeList(data.HiddenDefaultDistricts);
        }

        private static void NormalizeList(List<string> values)
        {
            var unique = new List<string>();
            foreach (var item in values)
            {
                var value = (item ?? "").Trim();
                if (value.Length == 0 || DistrictOptionService.ContainsIgnoreCase(unique, value)) continue;
                unique.Add(value);
            }
            values.Clear();
            values.AddRange(unique);
        }
    }

    internal static class DistrictOptionService
    {
        public static List<string> BuildOptions(IEnumerable<string> defaults, SidecontractPreferences preferences, string currentValue)
        {
            var result = new List<string>();
            var hidden = preferences == null ? null : preferences.HiddenDefaultDistricts;
            var custom = preferences == null ? null : preferences.CustomDistricts;

            if (defaults != null)
            {
                foreach (var item in defaults)
                {
                    var value = (item ?? "").Trim();
                    if (value.Length == 0) continue;
                    if (hidden != null && ContainsIgnoreCase(hidden, value)) continue;
                    AddUnique(result, value);
                }
            }
            if (custom != null)
            {
                foreach (var item in custom)
                {
                    AddUnique(result, item);
                }
            }

            var current = (currentValue ?? "").Trim();
            if (current.Length > 0 && !ContainsIgnoreCase(result, current))
            {
                result.Insert(0, current);
            }
            return result;
        }

        public static bool ContainsIgnoreCase(IEnumerable<string> values, string value)
        {
            if (values == null) return false;
            foreach (var item in values)
            {
                if (string.Equals((item ?? "").Trim(), (value ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static void AddUnique(List<string> values, string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0 || ContainsIgnoreCase(values, value)) return;
            values.Add(value);
        }
    }

    internal sealed class DistrictManagerDialog : Form
    {
        private sealed class DistrictListItem
        {
            public string Name { get; private set; }
            public bool IsDefault { get; private set; }
            public bool IsHidden { get; private set; }

            public DistrictListItem(string name, bool isDefault, bool isHidden)
            {
                Name = name;
                IsDefault = isDefault;
                IsHidden = isHidden;
            }

            public override string ToString()
            {
                if (IsDefault)
                {
                    return Name + (IsHidden ? "  [มาตรฐาน • ซ่อนอยู่]" : "  [มาตรฐาน]");
                }
                return Name + "  [เพิ่มเอง]";
            }
        }

        private readonly List<string> defaults = new List<string>();
        private readonly ListBox districtList = new ListBox();
        private readonly Func<string, string> normalizer;

        public List<string> CustomDistricts { get; private set; }
        public List<string> HiddenDefaultDistricts { get; private set; }
        public bool IsDirty { get; private set; }

        public DistrictManagerDialog(
            IEnumerable<string> defaultValues,
            IEnumerable<string> customValues,
            IEnumerable<string> hiddenValues,
            Func<string, string> normalize)
        {
            normalizer = normalize;
            CopyUnique(defaults, defaultValues);
            CustomDistricts = new List<string>();
            HiddenDefaultDistricts = new List<string>();
            CopyUnique(CustomDistricts, customValues);
            CopyUnique(HiddenDefaultDistricts, hiddenValues);

            Text = "จัดการรายชื่อเขตพื้นที่การศึกษา";
            ClientSize = new Size(650, 475);
            MinimumSize = new Size(650, 475);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 10.0f);
            BackColor = Color.White;

            var title = new Label
            {
                Text = "รายชื่อที่ใช้ในช่องเขตพื้นที่การศึกษา",
                Location = new Point(16, 14),
                Size = new Size(600, 28),
                Font = new Font("Segoe UI", 11.0f, FontStyle.Bold),
                ForeColor = Color.FromArgb(24, 56, 82)
            };
            Controls.Add(title);

            var help = new Label
            {
                Text = "รายการมาตรฐานซ่อนได้ รายการที่เพิ่มเองลบได้ และข้อมูลใน Template เดิมจะไม่ถูกลบ",
                Location = new Point(16, 42),
                Size = new Size(610, 24),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            Controls.Add(help);

            districtList.Location = new Point(16, 75);
            districtList.Size = new Size(618, 300);
            districtList.IntegralHeight = false;
            districtList.HorizontalScrollbar = true;
            Controls.Add(districtList);

            var addButton = CreateButton("เพิ่มรายชื่อ", 16, 388, 126);
            addButton.Click += delegate { AddDistrict(); };
            Controls.Add(addButton);

            var toggleButton = CreateButton("ซ่อน/แสดงที่เลือก", 150, 388, 154);
            toggleButton.Click += delegate { ToggleSelectedDefault(); };
            Controls.Add(toggleButton);

            var deleteButton = CreateButton("ลบรายการที่เพิ่มเอง", 312, 388, 154);
            deleteButton.Click += delegate { DeleteSelectedCustom(); };
            Controls.Add(deleteButton);

            var restoreButton = CreateButton("คืนค่ามาตรฐาน", 474, 388, 144);
            restoreButton.Click += delegate { RestoreDefaults(); };
            Controls.Add(restoreButton);

            var saveButton = CreateButton("บันทึก", 422, 430, 100);
            saveButton.DialogResult = DialogResult.OK;
            saveButton.Click += delegate { Close(); };
            Controls.Add(saveButton);

            var cancelButton = CreateButton("ยกเลิก", 534, 430, 100);
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Click += delegate { Close(); };
            Controls.Add(cancelButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
            RefreshList("");
        }

        private static Button CreateButton(string text, int x, int y, int width)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(247, 250, 252),
                ForeColor = Color.FromArgb(24, 56, 82)
            };
        }

        private void AddDistrict()
        {
            string entered;
            using (var prompt = new Form())
            {
                prompt.Text = "เพิ่มรายชื่อเขต";
                prompt.ClientSize = new Size(490, 145);
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.ShowInTaskbar = false;
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.Font = new Font("Segoe UI", 10.0f);

                var label = new Label
                {
                    Text = "ชื่อเขตพื้นที่การศึกษา",
                    Location = new Point(16, 16),
                    Size = new Size(450, 24)
                };
                prompt.Controls.Add(label);

                var input = new TextBox
                {
                    Location = new Point(16, 45),
                    Size = new Size(458, 27)
                };
                prompt.Controls.Add(input);

                var ok = CreateButton("เพิ่ม", 262, 93, 100);
                var cancel = CreateButton("ยกเลิก", 374, 93, 100);
                ok.DialogResult = DialogResult.OK;
                cancel.DialogResult = DialogResult.Cancel;
                prompt.Controls.Add(ok);
                prompt.Controls.Add(cancel);
                prompt.AcceptButton = ok;
                prompt.CancelButton = cancel;
                prompt.Shown += delegate { input.Focus(); };

                if (prompt.ShowDialog(this) != DialogResult.OK) return;
                entered = input.Text;
            }

            if (string.IsNullOrWhiteSpace(entered))
            {
                MessageBox.Show(this, "กรุณาระบุชื่อเขต", "เพิ่มรายชื่อเขต", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var value = NormalizeEntry(entered);
            if (value.Length == 0)
            {
                MessageBox.Show(this, "ชื่อเขตไม่ถูกต้อง", "เพิ่มรายชื่อเขต", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (DistrictOptionService.ContainsIgnoreCase(defaults, value)
                || DistrictOptionService.ContainsIgnoreCase(CustomDistricts, value))
            {
                MessageBox.Show(this, "มีชื่อเขตนี้อยู่แล้ว", "เพิ่มรายชื่อเขต", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            CustomDistricts.Add(value);
            IsDirty = true;
            RefreshList(value);
        }

        private void ToggleSelectedDefault()
        {
            var item = districtList.SelectedItem as DistrictListItem;
            if (item == null)
            {
                MessageBox.Show(this, "กรุณาเลือกรายการก่อน", "จัดการรายชื่อเขต", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!item.IsDefault)
            {
                MessageBox.Show(this, "รายการที่เพิ่มเองใช้ปุ่มลบรายการที่เพิ่มเอง", "จัดการรายชื่อเขต", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (item.IsHidden)
            {
                RemoveIgnoreCase(HiddenDefaultDistricts, item.Name);
            }
            else
            {
                DistrictOptionService.AddUnique(HiddenDefaultDistricts, item.Name);
            }
            IsDirty = true;
            RefreshList(item.Name);
        }

        private void DeleteSelectedCustom()
        {
            var item = districtList.SelectedItem as DistrictListItem;
            if (item == null)
            {
                MessageBox.Show(this, "กรุณาเลือกรายการก่อน", "จัดการรายชื่อเขต", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (item.IsDefault)
            {
                MessageBox.Show(this, "รายการมาตรฐานให้ใช้ปุ่มซ่อน/แสดงที่เลือก", "จัดการรายชื่อเขต", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(this, "ลบรายการที่เพิ่มเองนี้หรือไม่?", "ยืนยันการลบ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            RemoveIgnoreCase(CustomDistricts, item.Name);
            IsDirty = true;
            RefreshList("");
        }

        private void RestoreDefaults()
        {
            if (HiddenDefaultDistricts.Count == 0)
            {
                MessageBox.Show(this, "รายการมาตรฐานครบอยู่แล้ว", "คืนค่ามาตรฐาน", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "คืนค่ารายการมาตรฐานทั้งหมดหรือไม่?", "คืนค่ามาตรฐาน", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            HiddenDefaultDistricts.Clear();
            IsDirty = true;
            RefreshList("");
        }

        private void RefreshList(string selectedName)
        {
            districtList.BeginUpdate();
            districtList.Items.Clear();
            var selectedIndex = -1;
            foreach (var name in defaults)
            {
                var hidden = DistrictOptionService.ContainsIgnoreCase(HiddenDefaultDistricts, name);
                var item = new DistrictListItem(name, true, hidden);
                districtList.Items.Add(item);
                if (selectedIndex < 0 && string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase)) selectedIndex = districtList.Items.Count - 1;
            }
            foreach (var name in CustomDistricts)
            {
                if (DistrictOptionService.ContainsIgnoreCase(defaults, name)) continue;
                var item = new DistrictListItem(name, false, false);
                districtList.Items.Add(item);
                if (selectedIndex < 0 && string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase)) selectedIndex = districtList.Items.Count - 1;
            }
            if (selectedIndex >= 0) districtList.SelectedIndex = selectedIndex;
            districtList.EndUpdate();
        }

        private string NormalizeEntry(string value)
        {
            var result = normalizer == null ? value : normalizer(value);
            return (result ?? "").Trim();
        }

        private static void CopyUnique(List<string> target, IEnumerable<string> values)
        {
            if (values == null) return;
            foreach (var value in values) DistrictOptionService.AddUnique(target, value);
        }

        private static void RemoveIgnoreCase(List<string> values, string value)
        {
            for (var i = values.Count - 1; i >= 0; i--)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase)) values.RemoveAt(i);
            }
        }
    }
}
