using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Sidecontract
{
    internal enum TemplateOperationResult
    {
        Saved,
        Declined,
        Cancelled,
        Failed
    }

    internal sealed class ProcurementModule : UserControl
    {
        private const string DefaultDistrict = "สำนักงานเขตพื้นที่การศึกษาประถมศึกษาแม่ฮ่องสอน เขต 2";
        private const string DistrictPrefix = "สำนักงานเขตพื้นที่การศึกษา";
        private const string EmptyValue = ".............";
        private const string ContractDatePlaceholder = ".................................";
        private const string UnspecifiedOption = "-- ไม่ระบุ --";
        private const string PositionPlaceholder = "-- เลือกตำแหน่ง --";
        private static readonly string[] DistrictOptions =
        {
            DefaultDistrict,
            "สำนักงานเขตพื้นที่การศึกษาประถมศึกษาประจวบคีรีขันธ์ เขต 2",
            "สำนักงานเขตพื้นที่การศึกษาประถมศึกษาพิษณุโลก เขต 2",
            "สำนักงานเขตพื้นที่การศึกษามัธยมศึกษากาญจนบุรี"
        };
        private static readonly Color ModuleAccent = Color.FromArgb(15, 118, 110);
        private static readonly Color ModuleAccentDark = Color.FromArgb(20, 83, 45);
        private static readonly Color ModuleAccentLight = Color.FromArgb(234, 245, 238);
        private static readonly Color ModuleBackground = Color.FromArgb(243, 246, 248);
        private static readonly Color ModuleBorder = Color.FromArgb(214, 224, 232);
        private static readonly Color TextColor = Color.FromArgb(30, 41, 59);
        private static readonly Color MutedText = Color.FromArgb(100, 116, 139);
        private static readonly Color Navy = Color.FromArgb(24, 56, 82);
        private static readonly Color PrimaryDark = Color.FromArgb(22, 72, 108);
        private static readonly string[] ThaiMonths = { "", "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน", "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม" };
        private static readonly string[] ContractYears = { "2569", "2570" };

        private readonly WorkingRecord record;
        private readonly Action sharedChanged;
        private readonly SidecontractStore store = new SidecontractStore(DocumentModule.Procurement);
        private readonly SidecontractPreferencesStore preferencesStore = new SidecontractPreferencesStore();
        private readonly ErrorProvider errorProvider = new ErrorProvider();
        private readonly Dictionary<string, Control> fields = new Dictionary<string, Control>();
        private readonly Dictionary<string, ComboBox> dateCombos = new Dictionary<string, ComboBox>();
        private readonly Dictionary<string, TextBox> dateCustomBoxes = new Dictionary<string, TextBox>();
        private readonly List<Panel> sectionPanels = new List<Panel>();
        private readonly List<CheckBox> documentChecks = new List<CheckBox>();
        private readonly List<ProcurementPositionConfig> positions = new List<ProcurementPositionConfig>();
        private readonly Dictionary<Control, int> employeeLowerControlBaseY = new Dictionary<Control, int>();
        private readonly ProcurementManifest manifest;
        private SidecontractStoreData storeData;
        private SidecontractPreferences districtPreferences;
        private ComboBox districtBox;
        private ComboBox positionBox;
        private CheckBox teachingBox;
        private CheckBox twoSchoolsBox;
        private Panel secondSchoolRow;
        private TextBox secondSchoolBox;
        private Label salaryLabel;
        private Label salaryTextLabel;
        private Label totalSalaryLabel;
        private Label totalSalaryCaption;
        private Panel documentPanel;
        private Panel employeeSectionPanel;
        private ListBox sectionList;
        private Panel contentHost;
        private bool suppressChanges;
        private bool isDirty;

        public bool IsDirty { get { return isDirty; } }
        public WorkingRecord Record { get { return record; } }

        public ProcurementModule(WorkingRecord sharedRecord, Action sharedChangedCallback)
        {
            record = sharedRecord ?? WorkingRecord.CreateEmpty();
            sharedChanged = sharedChangedCallback;
            Dock = DockStyle.Fill;
            BackColor = ModuleBackground;
            Font = new Font("Segoe UI", 10.0f);
            AutoScaleMode = AutoScaleMode.Dpi;
            manifest = LoadManifest();
            LoadCatalog();
            SidecontractPaths.EnsureUserTemplateRoot(DocumentModule.Procurement);
            try
            {
                storeData = store.Load();
            }
            catch (SidecontractStoreException ex)
            {
                storeData = new SidecontractStoreData();
                MessageBox.Show(ex.Message, "ข้อมูล Template", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            try
            {
                districtPreferences = preferencesStore.Load();
            }
            catch (SidecontractStoreException ex)
            {
                districtPreferences = new SidecontractPreferences();
                MessageBox.Show(ex.Message, "ค่าตั้งค่ารายชื่อเขต", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            BuildShell();
            SetDefaults();
            RefreshFromRecord();
        }

        public void RefreshFromSharedRecord()
        {
            RefreshFromRecord();
        }

        public void RefreshSharedFieldsFromRecord()
        {
            suppressChanges = true;
            try
            {
                Set("school.name", record.School.Name);
                Set("school.district", record.School.District);
                Set("school.subdistrict", record.School.Subdistrict);
                Set("school.amphoe", record.School.Amphoe);
                Set("school.province", record.School.Province);
                SetPerson("school.director", record.School.Director);
                SetPerson("school.supply", record.School.SupplyOfficer);
                SetPerson("school.headSupply", record.School.HeadSupplyOfficer);
                Set("employee.prefix", record.Employee.Prefix);
                Set("employee.given", record.Employee.GivenName);
                Set("employee.surname", record.Employee.Surname);
                Set("employee.nationalId", record.Employee.NationalId);
                SetDate("employee.birth.day", "employee.birth.dayCustom", record.Employee.BirthDay);
                SetDate("employee.birth.month", "employee.birth.monthCustom", record.Employee.BirthMonth);
                Set("employee.birth.year", record.Employee.BirthYear);
                Set("employee.age", record.Employee.Age);
                Set("employee.nationality", record.Employee.Nationality);
                Set("employee.race", record.Employee.Race);
                Set("employee.religion", record.Employee.Religion);
                Set("employee.houseNumber", record.Employee.HouseNumber);
                Set("employee.road", record.Employee.Road);
                Set("employee.subdistrict", record.Employee.Subdistrict);
                Set("employee.district", record.Employee.District);
                Set("employee.province", record.Employee.Province);
                Set("employee.idIssueDistrict", record.Employee.IdIssueDistrict);
                Set("employee.idIssueProvince", record.Employee.IdIssueProvince);
                SetDate("employee.idIssue.day", "employee.idIssue.dayCustom", record.Employee.IdIssueDay);
                SetDate("employee.idIssue.month", "employee.idIssue.monthCustom", record.Employee.IdIssueMonth);
                Set("employee.idIssue.year", record.Employee.IdIssueYear);
                SetDate("employee.idExpiry.day", "employee.idExpiry.dayCustom", record.Employee.IdExpiryDay);
                SetDate("employee.idExpiry.month", "employee.idExpiry.monthCustom", record.Employee.IdExpiryMonth);
                Set("employee.idExpiry.year", record.Employee.IdExpiryYear);
                Set("employee.educationLevel", record.Employee.EducationLevel);
                Set("employee.qualification", record.Employee.Qualification);
                Set("procurement.contractNumber", record.Procurement.ContractNumber);
                Set("procurement.contractDay", record.Procurement.ContractDay);
                Set("procurement.contractMonth", record.Procurement.ContractMonth);
                Set("procurement.contractYear", record.Procurement.ContractYear);
                Set("procurement.bank", record.Procurement.Bank);
                Set("procurement.bankBranch", record.Procurement.BankBranch);
                Set("procurement.bankAccountName", record.Procurement.BankAccountName);
                Set("procurement.bankAccountNumber", record.Procurement.BankAccountNumber);
                for (var i = 0; i < 3; i++) SetPerson("committee." + i, record.Committee[i]);
            }
            finally
            {
                suppressChanges = false;
            }
        }

        public void MarkSharedRecordClean()
        {
            isDirty = false;
        }

        public TemplateOperationResult SaveCurrentTemplate()
        {
            CaptureFromControls();
            using (var editor = new ProcurementTemplateDialog("บันทึก Template", ""))
            {
                if (editor.ShowDialog(this) != DialogResult.OK) return TemplateOperationResult.Cancelled;
                var name = editor.TemplateName;
                var copy = store.Clone(storeData);
                if (copy.Templates == null) copy.Templates = new List<SavedTemplateSnapshot>();
                var existing = copy.Templates.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                if (existing != null && MessageBox.Show("มีชื่อรายการนี้แล้ว ต้องการบันทึกทับหรือไม่?", "ชื่อซ้ำ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return TemplateOperationResult.Cancelled;
                }

                var now = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                var snapshot = existing ?? new SavedTemplateSnapshot();
                if (string.IsNullOrWhiteSpace(snapshot.CreatedAt)) snapshot.CreatedAt = now;
                snapshot.Name = name;
                snapshot.Note = editor.TemplateNote;
                snapshot.UpdatedAt = now;
                snapshot.Record = store.Clone(record);
                if (existing == null) copy.Templates.Add(snapshot);

                try
                {
                    store.Save(copy);
                    storeData = copy;
                    isDirty = false;
                    return TemplateOperationResult.Saved;
                }
                catch (SidecontractStoreException ex)
                {
                    MessageBox.Show(ex.Message, "บันทึก Template ไม่สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return TemplateOperationResult.Failed;
                }
            }
        }

        private ProcurementManifest LoadManifest()
        {
            var path = Path.Combine(SidecontractPaths.ShippedConfigRoot(DocumentModule.Procurement), "template_manifest.json");
            return SidecontractConfig.Load<ProcurementManifest>(path);
        }

        private Button CreateSecondaryButton(string text, Point location, Size size)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                BackColor = Color.White,
                ForeColor = PrimaryDark,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderColor = ModuleBorder;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(241, 247, 251);
            return button;
        }

        private Button CreatePrimaryButton(string text, Point location, Size size)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                BackColor = ModuleAccent,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = ModuleAccentDark;
            return button;
        }

        private void LoadCatalog()
        {
            var path = Path.Combine(SidecontractPaths.ShippedConfigRoot(DocumentModule.Procurement), "position_catalog.json");
            var catalog = SidecontractConfig.Load<ProcurementPositionCatalog>(path);
            if (catalog.positions == null || catalog.positions.Length == 0) throw new InvalidDataException("Procurement position catalog ต้องมีอย่างน้อย 1 ตำแหน่ง");
            positions.AddRange(catalog.positions);
        }

        private void BuildShell()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = ModuleAccent, Padding = new Padding(24, 12, 24, 10) };
            var accentBar = new Panel { Dock = DockStyle.Left, Width = 5, BackColor = Color.FromArgb(185, 230, 204) };
            var title = new Label { Text = "sidecontract  |  จัดซื้อจัดจ้างและสัญญา", Font = new Font("Segoe UI", 17f, FontStyle.Bold), ForeColor = Color.White, Dock = DockStyle.Left, Width = 520, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };
            var subtitle = new Label { Text = "สร้างเอกสารจากแม่แบบที่ตรวจสอบแล้ว — ทำงานแบบออฟไลน์และเก็บข้อมูลไว้ในเครื่อง", Font = new Font("Segoe UI", 9.5f), ForeColor = Color.FromArgb(240, 250, 244), TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            header.Controls.Add(subtitle);
            header.Controls.Add(title);
            header.Controls.Add(accentBar);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 68, BackColor = Color.White, Padding = new Padding(20, 12, 20, 12) };
            footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ModuleBorder });
            var clear = CreateSecondaryButton("ล้างข้อมูลหมวดนี้", new Point(20, 12), new Size(145, 42));
            clear.Dock = DockStyle.Left;
            clear.Click += delegate { ClearCurrentSection(); };
            footer.Controls.Add(clear);
            var generate = CreatePrimaryButton("ตรวจสอบและสร้างเอกสาร", new Point(0, 12), new Size(206, 42));
            generate.Dock = DockStyle.Right;
            generate.Click += delegate { GenerateDocuments(); };
            footer.Controls.Add(generate);
            var newEmployee = CreateSecondaryButton("เพิ่มลูกจ้างใหม่", new Point(0, 12), new Size(145, 42));
            newEmployee.Dock = DockStyle.Right;
            newEmployee.Click += delegate { AddNewEmployee(); };
            footer.Controls.Add(newEmployee);
            var load = CreateSecondaryButton("โหลด Template", new Point(0, 12), new Size(125, 42));
            load.Dock = DockStyle.Right;
            load.Click += delegate { LoadTemplateFlow(); };
            footer.Controls.Add(load);
            var save = CreateSecondaryButton("บันทึก Template", new Point(0, 12), new Size(135, 42));
            save.Dock = DockStyle.Right;
            save.Click += delegate { SaveCurrentTemplate(); };
            footer.Controls.Add(save);
            var deleteTemplate = CreateSecondaryButton("ลบ Template", new Point(0, 12), new Size(115, 42));
            deleteTemplate.Dock = DockStyle.Right;
            deleteTemplate.Click += delegate { DeleteTemplateFlow(); };
            footer.Controls.Add(deleteTemplate);

            var body = new Panel { Dock = DockStyle.Fill, BackColor = ModuleBackground };
            var navigation = new Panel { Dock = DockStyle.Left, Width = 350, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(20, 16, 16, 16) };
            var navigationTitle = new Label { Text = "ขั้นตอนการกรอกข้อมูล", Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Navy };
            var navigationHint = new Label { Text = "คลิกชื่อแต่ละขั้นตอนเพื่อกรอกข้อมูล", Dock = DockStyle.Top, Height = 30, ForeColor = MutedText, Font = new Font("Segoe UI", 9.5f) };
            sectionList = new ListBox { Dock = DockStyle.Top, BorderStyle = BorderStyle.None, BackColor = Color.White, Font = new Font("Segoe UI", 10.0f), IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 42, Cursor = Cursors.Hand, TabStop = true };
            sectionList.Items.AddRange(new object[] { "1  ข้อมูลโรงเรียน", "2  กรรมการ TOR", "3  ลูกจ้าง / ตำแหน่ง / เงินเดือน", "4  ข้อมูลสัญญา", "5  สรุปชุดเอกสารที่ต้องการสร้าง" });
            sectionList.Height = sectionList.ItemHeight * sectionList.Items.Count;
            sectionList.SelectedIndexChanged += delegate { ShowSection(sectionList.SelectedIndex); };
            sectionList.DrawItem += DrawSectionItem;
            var navigationCallout = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(255, 250, 224), BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(10, 7, 8, 7) };
            navigationCallout.Controls.Add(new Label { Text = "คลิกแถบแต่ละขั้นตอน\nเพื่อกรอกข้อมูลให้ครบทุกแถบ", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(132, 89, 0), TextAlign = ContentAlignment.MiddleCenter });
            var navigationGap = new Panel { Dock = DockStyle.Top, Height = 14, BackColor = Color.White };
            var openOutput = CreateSecondaryButton("เปิดโฟลเดอร์เอกสารที่สร้างแล้ว", new Point(0, 0), new Size(300, 42));
            openOutput.Dock = DockStyle.Bottom;
            openOutput.Click += delegate { OpenOutputFolder(); };
            navigation.Controls.Add(openOutput);
            navigation.Controls.Add(navigationCallout);
            navigation.Controls.Add(navigationGap);
            navigation.Controls.Add(sectionList);
            navigation.Controls.Add(navigationHint);
            navigation.Controls.Add(navigationTitle);

            contentHost = new Panel { Dock = DockStyle.Fill, AutoScroll = false, BackColor = ModuleBackground, Padding = new Padding(16, 16, 20, 14) };
            for (var i = 0; i < 5; i++)
            {
                var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Visible = false };
                contentHost.Controls.Add(panel);
                sectionPanels.Add(panel);
            }
            body.Controls.Add(contentHost);
            body.Controls.Add(navigation);

            // Dock order is intentional: the last control is laid out first in WinForms.
            // Put the fill body first so it cannot cover the fixed header or action bar.
            Controls.Add(body);
            Controls.Add(footer);
            Controls.Add(header);
            BuildSchoolSection(sectionPanels[0]);
            BuildCommitteeSection(sectionPanels[1]);
            BuildEmployeeSection(sectionPanels[2]);
            BuildContractSection(sectionPanels[3]);
            BuildSummarySection(sectionPanels[4]);
            sectionList.SelectedIndex = 0;
        }

        private void DrawSectionItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= sectionList.Items.Count) return;
            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var background = selected ? ModuleAccentLight : Color.White;
            var foreground = selected ? ModuleAccentDark : TextColor;
            var bounds = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 18, e.Bounds.Height);
            using (var backgroundBrush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
            }
            var hasStatus = e.Index < 4;
            var statusBounds = new Rectangle(e.Bounds.Right - 76, e.Bounds.Top, 68, e.Bounds.Height);
            using (var font = new Font(sectionList.Font, selected ? FontStyle.Bold : FontStyle.Regular))
            using (var brush = new SolidBrush(foreground))
            {
                var titleRight = hasStatus ? statusBounds.Left - 8 : e.Bounds.Right - 8;
                e.Graphics.DrawString(sectionList.Items[e.Index].ToString(), font, brush, new Rectangle(bounds.Left, bounds.Top, Math.Max(1, titleRight - bounds.Left), bounds.Height), new StringFormat { LineAlignment = StringAlignment.Center });
            }
            if (hasStatus)
            {
                var complete = IsSectionComplete(e.Index);
                var statusText = complete ? "ครบ" : "ไม่ครบ";
                var statusColor = complete ? Color.FromArgb(22, 163, 74) : Color.FromArgb(220, 38, 38);
                using (var statusBrush = new SolidBrush(statusColor))
                using (var statusFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                {
                    var dot = new Rectangle(statusBounds.Left + 2, statusBounds.Top + 16, 10, 10);
                    e.Graphics.FillEllipse(statusBrush, dot);
                    e.Graphics.DrawString(statusText, statusFont, statusBrush, new Rectangle(statusBounds.Left + 16, statusBounds.Top, statusBounds.Width - 16, statusBounds.Height), new StringFormat { LineAlignment = StringAlignment.Center });
                }
            }
            if (selected)
            {
                using (var accentBrush = new SolidBrush(ModuleAccent))
                {
                    e.Graphics.FillRectangle(accentBrush, new Rectangle(e.Bounds.Left, e.Bounds.Top, 4, e.Bounds.Height));
                }
            }
            e.DrawFocusRectangle();
        }

        private void BuildSchoolSection(Panel parent)
        {
            AddHeading(parent, "ข้อมูลโรงเรียนและผู้ลงนามของ record นี้", "ชื่อโรงเรียน เขตต้นสังกัด และที่อยู่โรงเรียนจะใช้กับเอกสารของลูกจ้างรายนี้");
            AddText(parent, "ชื่อโรงเรียน *", "school.name", 24, 82, 620);
            districtBox = AddEditableCombo(parent, "เขตพื้นที่การศึกษาต้นสังกัด *", "school.district", 24, 150, 390);
            RefreshDistrictOptions(record.School.District);
            var manageDistricts = CreateSecondaryButton("จัดการรายชื่อเขต", new Point(434, 170), new Size(280, 32));
            manageDistricts.Click += delegate { ManageDistricts(); };
            parent.Controls.Add(manageDistricts);

            AddZoneHeading(parent, "ที่อยู่โรงเรียน", 224);
            AddText(parent, "ตำบลโรงเรียน *", "school.subdistrict", 24, 258, 210);
            AddText(parent, "อำเภอโรงเรียน *", "school.amphoe", 244, 258, 210);
            AddText(parent, "จังหวัดโรงเรียน *", "school.province", 484, 258, 230);
            AddPersonFields(parent, "ผู้อำนวยการโรงเรียน", "school.director", 356);
            AddPersonFields(parent, "เจ้าหน้าที่พัสดุ", "school.supply", 476);
            AddPersonFields(parent, "หัวหน้าเจ้าหน้าที่พัสดุ", "school.headSupply", 596);
            parent.AutoScrollMinSize = new Size(0, 720);
        }
        private void BuildCommitteeSection(Panel parent)
        {
            AddHeading(parent, "คณะกรรมการกำหนด TOR", "กรอกกรรมการ 1–3 ให้ครบ โดยกรรมการ 1 เป็นประธาน และใช้ชุดเดียวกับเอกสารเบิกเงินเดือน");
            AddCommitteeFields(parent, 0, 92, "กรรมการ 1 (ประธาน)");
            AddCommitteeFields(parent, 1, 252, "กรรมการ 2");
            AddCommitteeFields(parent, 2, 412, "กรรมการ 3");
            AddText(parent, "เลขที่คำสั่งแต่งตั้ง TORและตรวจรับ", "procurement.specificationOrder", 24, 580, 220);
            parent.Controls.Add(new Label { Text = "(เช่น 1/2570)", Location = new Point(256, 604), Size = new Size(130, 26), ForeColor = Color.FromArgb(80, 95, 115), TextAlign = ContentAlignment.MiddleLeft });
        }

        private void BuildEmployeeSection(Panel parent)
        {
            AddHeading(parent, "ข้อมูลลูกจ้าง", "ไม่ครบ = ยังมีช่องว่าง | * = ต้องกรอกเพื่อสร้างเอกสาร | ไม่มี * = เว้นว่างหรือใส่ - ได้");
            AddZoneHeading(parent, "ข้อมูลส่วนตัว", 92);
            AddEditableCombo(parent, "คำนำหน้า", "employee.prefix", 24, 126, 150).Items.AddRange(new object[] { UnspecifiedOption, "นาย", "นาง", "นางสาว", "ว่าที่ร้อยตรี", "......(กรอกเอง)" });
            AddText(parent, "ชื่อ *", "employee.given", 194, 126, 250);
            AddText(parent, "นามสกุล *", "employee.surname", 464, 126, 250);
            AddText(parent, "เลขประจำตัวประชาชน *", "employee.nationalId", 24, 206, 360);
            AddDateField(parent, "วันเกิด", "employee.birth.day", "employee.birth.dayCustom", 24, 286);
            AddDateField(parent, "เดือนเกิด", "employee.birth.month", "employee.birth.monthCustom", 244, 286);
            AddText(parent, "ปีเกิด", "employee.birth.year", 464, 286, 120);
            AddText(parent, "อายุ", "employee.age", 604, 286, 70);
            parent.Controls.Add(new Label { Text = "ปี", Location = new Point(680, 310), Size = new Size(24, 20), ForeColor = Color.FromArgb(31, 41, 55) });
            AddText(parent, "สัญชาติ", "employee.nationality", 24, 366, 210);
            AddText(parent, "เชื้อชาติ", "employee.race", 254, 366, 210);
            AddText(parent, "ศาสนา", "employee.religion", 484, 366, 230);

            AddZoneHeading(parent, "ตำแหน่งและอัตราเงินเดือน", 446);
            positionBox = AddCombo(parent, "ตำแหน่ง *", "procurement.position", 24, 480, 330);
            positionBox.Items.Add(new PositionView(null));
            foreach (var position in positions) positionBox.Items.Add(new PositionView(position));
            positionBox.SelectedIndexChanged += delegate { UpdateRouteControls(); OnUiChanged(); };
            salaryLabel = AddReadOnlyValue(parent, "เงินเดือน *", 374, 480, 220);
            teachingBox = new CheckBox { Text = "มีงานสอน", Location = new Point(24, 538), Width = 130 };
            teachingBox.CheckedChanged += delegate { UpdateRouteControls(); OnUiChanged(); };
            parent.Controls.Add(teachingBox);
            twoSchoolsBox = new CheckBox { Text = "ทำงาน 2 โรงเรียน", Location = new Point(170, 538), Width = 170 };
            twoSchoolsBox.CheckedChanged += delegate { UpdateRouteControls(); OnUiChanged(); };
            parent.Controls.Add(twoSchoolsBox);
            secondSchoolRow = new Panel
            {
                Location = new Point(24, 568),
                Size = new Size(680, 84),
                BackColor = Color.FromArgb(250, 253, 251),
                BorderStyle = BorderStyle.None,
                Visible = false
            };
            secondSchoolRow.Controls.Add(new Label
            {
                Text = "ชื่อโรงเรียนที่สอง",
                Location = new Point(12, 8),
                Size = new Size(640, 20),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = TextColor
            });
            secondSchoolBox = new TextBox
            {
                Location = new Point(12, 34),
                Size = new Size(640, 26),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };
            secondSchoolBox.TextChanged += delegate { OnUiChanged(); };
            secondSchoolRow.Controls.Add(secondSchoolBox);
            parent.Controls.Add(secondSchoolRow);
            fields["procurement.secondSchool"] = secondSchoolBox;
            salaryTextLabel = AddReadOnlyValue(parent, "เงินเดือนTEXT", 24, 712, 220);
            totalSalaryLabel = AddReadOnlyValue(parent, "เงินรวม / อากรแสตมป์ที่ต้องติด", 264, 712, 440);

            AddZoneHeading(parent, "การศึกษา", 784);
            AddEditableCombo(parent, "สำเร็จการศึกษาระดับ", "employee.educationLevel", 24, 818, 330).Items.AddRange(new object[] { UnspecifiedOption, "ประถมศึกษาตอนต้น", "ประถมศึกษาตอนปลาย", "มัธยมศึกษาตอนต้น", "มัธยมศึกษาตอนปลาย", "ปวช", "ปวส", "ปริญญาตรี", "ปริญญาโท", "ปริญญาเอก", "......(กรอกเอง)" });
            AddEditableCombo(parent, "คุณวุฒิการศึกษา", "employee.qualification", 374, 818, 340).Items.AddRange(new object[] { UnspecifiedOption, "ปวช.", "ปวส.", "ครุศาสตรบัณฑิต (ค.บ.)", "ศึกษาศาสตรบัณฑิต (ศษ.บ.)", "ศิลปศาสตรบัณฑิต (ศศ.บ.)", "วิทยาศาสตรบัณฑิต (วท.บ.)", "วิศวกรรมศาสตรบัณฑิต (วศ.บ.)", "บริหารธุรกิจบัณฑิต (บธ.บ.)", "นิติศาสตรบัณฑิต (น.บ.)", "รัฐศาสตรบัณฑิต (ร.บ.)", "รัฐประศาสนศาสตรบัณฑิต (รป.บ.)", "สาธารณสุขศาสตรบัณฑิต (ส.บ.)", "พยาบาลศาสตรบัณฑิต (พย.บ.)", "ปริญญาโท", "ปริญญาเอก", "อื่น ๆ (กรอกเอง)" });

            AddZoneHeading(parent, "ที่อยู่ / บัตรประชาชน", 930);
            AddText(parent, "บ้านเลขที่", "employee.houseNumber", 24, 964, 150);
            AddText(parent, "ถนน", "employee.road", 194, 964, 220);
            AddText(parent, "ตำบล", "employee.subdistrict", 434, 964, 170);
            AddText(parent, "อำเภอ", "employee.district", 24, 1044, 200);
            AddText(parent, "จังหวัด", "employee.province", 244, 1044, 200);
            AddZoneHeading(parent, "สถานที่ออกบัตร", 1124);
            AddText(parent, "ออกอำเภอ", "employee.idIssueDistrict", 24, 1162, 250);
            AddText(parent, "ออกจังหวัด", "employee.idIssueProvince", 294, 1162, 250);

            AddZoneHeading(parent, "วันออกบัตร", 1244);
            AddDateField(parent, "วัน", "employee.idIssue.day", "employee.idIssue.dayCustom", 24, 1282);
            AddDateField(parent, "เดือน", "employee.idIssue.month", "employee.idIssue.monthCustom", 244, 1282);
            AddText(parent, "ปี", "employee.idIssue.year", 464, 1282, 150);

            AddZoneHeading(parent, "วันหมดอายุบัตร", 1364);
            AddDateField(parent, "วัน", "employee.idExpiry.day", "employee.idExpiry.dayCustom", 24, 1402);
            AddDateField(parent, "เดือน", "employee.idExpiry.month", "employee.idExpiry.monthCustom", 244, 1402);
            AddText(parent, "ปี", "employee.idExpiry.year", 464, 1402, 150);
            parent.AutoScrollMinSize = new Size(0, 1544);
            CaptureEmployeeLowerLayout(parent);
        }

        private void CaptureEmployeeLowerLayout(Panel parent)
        {
            employeeSectionPanel = parent;
            employeeLowerControlBaseY.Clear();
            foreach (Control child in parent.Controls)
            {
                if (child == secondSchoolRow) continue;
                if (child.Location.Y >= 712) employeeLowerControlBaseY[child] = child.Location.Y;
            }
            LayoutEmployeeOptionalBlock();
        }

        private void LayoutEmployeeOptionalBlock()
        {
            if (secondSchoolRow == null) return;
            var hasSecondSchool = twoSchoolsBox != null && twoSchoolsBox.Checked;
            var parent = employeeSectionPanel ?? (secondSchoolRow.Parent as Panel);
            if (parent == null) return;

            // A scrollable WinForms Panel keeps its display rectangle while a child
            // is moved. Reset the logical scroll position during the reflow so a
            // repeated check/uncheck cannot make the visible gap accumulate.
            var previousScrollY = Math.Max(0, -parent.AutoScrollPosition.Y);
            var offset = hasSecondSchool ? -24 : -144;
            parent.SuspendLayout();
            try
            {
                parent.AutoScrollPosition = Point.Empty;
                secondSchoolRow.SetBounds(24, 568, 680, 84);
                foreach (var entry in employeeLowerControlBaseY)
                {
                    entry.Key.Top = entry.Value + offset;
                }
                secondSchoolRow.Visible = hasSecondSchool;
                parent.AutoScrollMinSize = new Size(0, 1544 + offset);
            }
            finally
            {
                parent.ResumeLayout(true);
            }

            // Keep the user's place in the form, clamped to the new content height.
            var maxScrollY = Math.Max(0, parent.DisplayRectangle.Height - parent.ClientSize.Height);
            parent.AutoScrollPosition = new Point(0, Math.Min(previousScrollY, maxScrollY));
        }
        private void BuildContractSection(Panel parent)
        {
            AddHeading(parent, "ข้อมูลสัญญา", "ข้อมูลชุดนี้เป็นของสัญญาของลูกจ้างที่กำลังจัดทำ และจะถูกนำไปลงในแบบ 9. สัญญา");
            AddZoneHeading(parent, "เลขที่และวันที่ทำสัญญา", 92);
            AddText(parent, "เลขที่สัญญา *", "procurement.contractNumber", 24, 126, 220);
            parent.Controls.Add(new Label
            {
                Text = "เช่น 1/2570 ห้ามใช้เลขซ้ำกับผู้รับจ้างรายอื่นของโรงเรียน",
                Location = new Point(24, 178),
                Size = new Size(340, 36),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = MutedText,
                AutoSize = false
            });
            AddContractDateCombo(parent, "วันทำสัญญา", "procurement.contractDay", 264, 126, 100, false);
            AddContractDateCombo(parent, "เดือนทำสัญญา", "procurement.contractMonth", 384, 126, 180, true);
            AddContractYearCombo(parent, "ปีทำสัญญา", "procurement.contractYear", 584, 126, 130);
            parent.Controls.Add(new Label { Text = "*กรณียังไม่ทราบวันลงนามในสัญญา เพราะยังไม่ได้รับอนุมัติเงินงวด ให้เว้นว่าง", Location = new Point(264, 178), Size = new Size(450, 36), Font = new Font("Segoe UI", 8.5f, FontStyle.Italic), ForeColor = MutedText, AutoSize = false });

            AddZoneHeading(parent, "บัญชีรับเงิน", 230);
            AddText(parent, "ธนาคาร *", "procurement.bank", 24, 264, 200);
            AddText(parent, "สาขาธนาคาร *", "procurement.bankBranch", 234, 264, 220);
            AddText(parent, "ชื่อบัญชีธนาคาร *", "procurement.bankAccountName", 464, 264, 240);
            AddText(parent, "เลขบัญชี *", "procurement.bankAccountNumber", 24, 334, 440);
            parent.AutoScrollMinSize = new Size(0, 430);
        }
        private void BuildSummarySection(Panel parent)
        {
            AddHeading(parent, "สรุปชุดเอกสารที่ต้องการสร้าง", "ตรวจรายการเอกสารที่ต้องการก่อนกดตรวจสอบและสร้างเอกสาร");
            documentPanel = new Panel { Location = new Point(24, 96), Size = new Size(690, 560), AutoScroll = true, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            parent.Controls.Add(documentPanel);
            UpdateRouteControls();
        }

        private void OpenOutputFolder()
        {
            var outputRoot = SidecontractPaths.OutputRoot(DocumentModule.Procurement);
            Directory.CreateDirectory(outputRoot);
            Process.Start(new ProcessStartInfo
            {
                FileName = outputRoot,
                UseShellExecute = true
            });
        }

        private void AddZoneHeading(Control parent, string title, int y)
        {
            parent.Controls.Add(new Panel { Location = new Point(24, y), Size = new Size(680, 1), BackColor = ModuleBorder });
            parent.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Location = new Point(24, y + 8), Size = new Size(680, 22), ForeColor = ModuleAccentDark });
        }

        private void AddHeading(Panel parent, string title, string hint)
        {
            parent.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 15f, FontStyle.Bold), Location = new Point(24, 14), Size = new Size(680, 30), ForeColor = TextColor });
            var hintBox = new Panel { Location = new Point(24, 44), Size = new Size(680, 28), BackColor = ModuleAccentLight, BorderStyle = BorderStyle.FixedSingle };
            hintBox.Controls.Add(new Label { Text = hint, Font = new Font("Segoe UI", 8.5f), Location = new Point(9, 3), Size = new Size(658, 20), ForeColor = ModuleAccentDark, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft });
            parent.Controls.Add(hintBox);
            parent.Controls.Add(new Panel { Location = new Point(24, 76), Size = new Size(680, 2), BackColor = ModuleAccent });
        }

        private TextBox AddText(Control parent, string label, string key, int x, int y, int width)
        {
            parent.Controls.Add(new Label { Text = label, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Location = new Point(x, y), Size = new Size(width, 20), ForeColor = TextColor });
            var box = new TextBox { Location = new Point(x, y + 24), Size = new Size(width, 26), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
            box.TextChanged += delegate { OnUiChanged(); };
            parent.Controls.Add(box);
            fields[key] = box;
            return box;
        }

        private Label AddReadOnlyValue(Control parent, string label, int x, int y, int width)
        {
            var caption = new Label { Text = label, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Location = new Point(x, y), Size = new Size(width, 20), ForeColor = TextColor };
            if (label.StartsWith("เงินรวม", StringComparison.Ordinal)) totalSalaryCaption = caption;
            parent.Controls.Add(caption);
            var value = new Label { Location = new Point(x, y + 24), Size = new Size(width, 26), BorderStyle = BorderStyle.FixedSingle, BackColor = ModuleAccentLight, ForeColor = ModuleAccentDark, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(6, 0, 0, 0) };
            parent.Controls.Add(value);
            return value;
        }

        private ComboBox AddCombo(Control parent, string label, string key, int x, int y, int width)
        {
            parent.Controls.Add(new Label { Text = label, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), Location = new Point(x, y), Size = new Size(width, 20), ForeColor = TextColor });
            var box = new ComboBox { Location = new Point(x, y + 24), Size = new Size(width, 26), DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.White };
            box.SelectedIndexChanged += delegate { OnUiChanged(); };
            parent.Controls.Add(box);
            fields[key] = box;
            return box;
        }

        private ComboBox AddContractDateCombo(Control parent, string label, string key, int x, int y, int width, bool month)
        {
            var box = AddCombo(parent, label, key, x, y, width);
            box.Items.Add(UnspecifiedOption);
            if (month)
            {
                for (var i = 1; i < ThaiMonths.Length; i++) box.Items.Add(ThaiMonths[i]);
            }
            else
            {
                for (var i = 1; i <= 31; i++) box.Items.Add(i.ToString());
            }
            return box;
        }

        private ComboBox AddContractYearCombo(Control parent, string label, string key, int x, int y, int width)
        {
            var box = AddCombo(parent, label, key, x, y, width);
            box.Items.Add(UnspecifiedOption);
            box.Items.AddRange(ContractYears);
            return box;
        }

        private ComboBox AddEditableCombo(Control parent, string label, string key, int x, int y, int width)
        {
            var box = AddCombo(parent, label, key, x, y, width);
            box.DropDownStyle = ComboBoxStyle.DropDown;
            box.TextChanged += delegate { OnUiChanged(); };
            return box;
        }

        private void AddPersonFields(Control parent, string title, string prefix, int y)
        {
            parent.Controls.Add(new Panel { Location = new Point(24, y - 10), Size = new Size(680, 1), BackColor = ModuleBorder });
            parent.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(24, y), Size = new Size(680, 20), ForeColor = ModuleAccentDark });
            var prefixBox = AddEditableCombo(parent, "คำนำหน้า", prefix + ".prefix", 24, y + 24, 150);
            prefixBox.Items.AddRange(new object[] { UnspecifiedOption, "นาย", "นาง", "นางสาว", "ว่าที่ร้อยตรี", "......(กรอกเอง)" });
            AddText(parent, "ชื่อ", prefix + ".given", 194, y + 24, 250);
            AddText(parent, "นามสกุล", prefix + ".surname", 464, y + 24, 250);
        }

        private void AddCommitteeFields(Control parent, int index, int y, string title)
        {
            var prefix = "committee." + index;
            var group = new Panel { Location = new Point(16, y - 8), Size = new Size(700, 154), BackColor = Color.FromArgb(250, 253, 251), BorderStyle = BorderStyle.FixedSingle, TabStop = false };
            parent.Controls.Add(group);
            group.Controls.Add(new Panel { Location = new Point(0, 0), Size = new Size(5, 36), BackColor = ModuleAccent });
            group.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Location = new Point(5, 0), Size = new Size(689, 36), BackColor = Color.FromArgb(248, 250, 252), ForeColor = TextColor, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) });
            var prefixBox = AddEditableCombo(group, "คำนำหน้า", prefix + ".prefix", 12, 42, 150);
            prefixBox.Items.AddRange(new object[] { UnspecifiedOption, "นาย", "นาง", "นางสาว", "ว่าที่ร้อยตรี", "......(กรอกเอง)" });
            AddText(group, "ชื่อ", prefix + ".given", 182, 42, 220);
            AddText(group, "นามสกุล", prefix + ".surname", 424, 42, 180);
            var position = AddEditableCombo(group, "ตำแหน่ง", prefix + ".position", 12, 100, 580);
            position.Items.AddRange(new object[] { UnspecifiedOption, "ครูผู้ช่วย", "ครู", "ครูชำนาญการ", "ครูชำนาญการพิเศษ", "รองผู้อำนวยการโรงเรียน", "ผู้อำนวยการโรงเรียน", "......(กรอกเอง)" });
        }

        private void AddDateField(Control parent, string label, string key, string customKey, int x, int y)
        {
            parent.Controls.Add(new Label { Text = label, Location = new Point(x, y), Size = new Size(200, 20) });
            var box = new ComboBox { Location = new Point(x, y + 24), Size = new Size(108, 26), DropDownStyle = ComboBoxStyle.DropDown, BackColor = Color.White };
            box.Items.Add(UnspecifiedOption);
            if (key.EndsWith(".month", StringComparison.Ordinal))
            {
                for (var i = 1; i < ThaiMonths.Length; i++) box.Items.Add(ThaiMonths[i]);
            }
            else
            {
                for (var i = 1; i <= 31; i++) box.Items.Add(i.ToString());
            }
            box.Items.Add("......(กรอกเอง)");
            var custom = new TextBox { Location = new Point(x + 114, y + 24), Size = new Size(96, 26), Visible = false, BackColor = Color.White };
            box.SelectedIndexChanged += delegate { custom.Visible = box.Text.StartsWith("......", StringComparison.Ordinal); OnUiChanged(); };
            box.TextChanged += delegate { custom.Visible = box.Text.StartsWith("......", StringComparison.Ordinal); OnUiChanged(); };
            custom.TextChanged += delegate { OnUiChanged(); };
            parent.Controls.Add(box);
            parent.Controls.Add(custom);
            dateCombos[key] = box;
            dateCustomBoxes[customKey] = custom;
        }

        private void RefreshDistrictOptions(string currentValue)
        {
            if (districtBox == null) return;
            var value = (currentValue ?? "").Trim();

            var options = DistrictOptionService.BuildOptions(DistrictOptions, districtPreferences, value);
            var previousSuppressChanges = suppressChanges;
            suppressChanges = true;
            try
            {
                districtBox.Items.Clear();
                districtBox.Items.AddRange(options.ToArray());
                districtBox.Text = value;
            }
            finally
            {
                suppressChanges = previousSuppressChanges;
            }
        }

        private void ManageDistricts()
        {
            SidecontractPreferences preferences;
            try
            {
                preferences = preferencesStore.Load();
            }
            catch (SidecontractStoreException ex)
            {
                preferences = districtPreferences ?? new SidecontractPreferences();
                MessageBox.Show(ex.Message, "ค่าตั้งค่ารายชื่อเขต", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            using (var dialog = new DistrictManagerDialog(
                DistrictOptions,
                preferences.CustomDistricts,
                preferences.HiddenDefaultDistricts,
                NormalizeDistrictName))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || !dialog.IsDirty) return;
                preferences.CustomDistricts = dialog.CustomDistricts;
                preferences.HiddenDefaultDistricts = dialog.HiddenDefaultDistricts;
                try
                {
                    preferencesStore.Save(preferences);
                }
                catch (SidecontractStoreException ex)
                {
                    MessageBox.Show(ex.Message, "บันทึกรายชื่อเขตไม่สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                districtPreferences = preferences;
                var current = districtBox == null ? record.School.District : districtBox.Text;
                RefreshDistrictOptions(current);
            }
        }
        private void SetDefaults()
        {
            suppressChanges = true;
            try
            {
                var districtOptions = DistrictOptionService.BuildOptions(DistrictOptions, districtPreferences, "");
                var initialDistrict = districtOptions.Count == 0 ? "" : districtOptions[0];
                record.School.District = initialDistrict;
                if (districtBox != null) RefreshDistrictOptions(initialDistrict);
                if (positionBox != null) positionBox.SelectedIndex = 0;
                if (teachingBox != null) teachingBox.Checked = false;
                if (twoSchoolsBox != null) twoSchoolsBox.Checked = false;
                if (secondSchoolRow != null) secondSchoolRow.Visible = false;
            }
            finally
            {
                suppressChanges = false;
            }
        }

        private void ShowSection(int index)
        {
            for (var i = 0; i < sectionPanels.Count; i++) sectionPanels[i].Visible = i == index;
        }

        private void OnUiChanged()
        {
            if (suppressChanges) return;
            CaptureFromControls();
            isDirty = true;
            if (sectionList != null) sectionList.Invalidate();
            if (sharedChanged != null) sharedChanged();
        }

        private bool IsSectionComplete(int index)
        {
            CaptureFromControls();
            if (index == 0)
            {
                return AllFieldsComplete(fields.Keys.Where(x => x.StartsWith("school.", StringComparison.Ordinal)));
            }
            if (index == 1)
            {
                return AllFieldsComplete(fields.Keys.Where(x => x.StartsWith("committee.", StringComparison.Ordinal)).Concat(new[] { "procurement.specificationOrder" }));
            }
            if (index == 2)
            {
                var keys = fields.Keys
                    .Where(x => x.StartsWith("employee.", StringComparison.Ordinal))
                    .Concat(new[]
                    {
                        "employee.birth.day", "employee.birth.month",
                        "employee.idIssue.day", "employee.idIssue.month",
                        "employee.idExpiry.day", "employee.idExpiry.month",
                        "procurement.position"
                    });
                if (record.Procurement.WorksAtTwoSchools) keys = keys.Concat(new[] { "procurement.secondSchool" });
                return AllFieldsComplete(keys);
            }
            if (index == 3)
            {
                return AllFieldsComplete(new[]
                {
                    "procurement.contractNumber",
                    "procurement.bank", "procurement.bankBranch", "procurement.bankAccountName", "procurement.bankAccountNumber"
                });
            }
            return false;
        }

        private bool AllFieldsComplete(IEnumerable<string> keys)
        {
            return keys.All(HasValue);
        }

        private bool HasValue(string key)
        {
            if (dateCombos.ContainsKey(key)) return !string.IsNullOrWhiteSpace(ReadDate(key, key + "Custom"));
            return !string.IsNullOrWhiteSpace(Read(key));
        }

        private void CaptureFromControls()
        {
            record.School.Name = Read("school.name");
            record.School.District = Read("school.district");
            record.School.Subdistrict = Read("school.subdistrict");
            record.School.Amphoe = Read("school.amphoe");
            record.School.Province = Read("school.province");
            record.School.Director = ReadPerson("school.director");
            record.School.SupplyOfficer = ReadPerson("school.supply");
            record.School.HeadSupplyOfficer = ReadPerson("school.headSupply");
            record.Employee.Prefix = Read("employee.prefix");
            record.Employee.GivenName = Read("employee.given");
            record.Employee.Surname = Read("employee.surname");
            record.Employee.NationalId = Read("employee.nationalId");
            record.Employee.BirthDay = ReadDate("employee.birth.day", "employee.birth.dayCustom");
            record.Employee.BirthMonth = ReadDate("employee.birth.month", "employee.birth.monthCustom");
            record.Employee.BirthYear = Read("employee.birth.year");
            record.Employee.Age = Read("employee.age");
            record.Employee.Nationality = Read("employee.nationality");
            record.Employee.Race = Read("employee.race");
            record.Employee.Religion = Read("employee.religion");
            record.Employee.HouseNumber = Read("employee.houseNumber");
            record.Employee.Road = Read("employee.road");
            record.Employee.Subdistrict = Read("employee.subdistrict");
            record.Employee.District = Read("employee.district");
            record.Employee.Province = Read("employee.province");
            record.Employee.IdIssueDistrict = Read("employee.idIssueDistrict");
            record.Employee.IdIssueProvince = Read("employee.idIssueProvince");
            record.Employee.IdIssueDay = ReadDate("employee.idIssue.day", "employee.idIssue.dayCustom");
            record.Employee.IdIssueMonth = ReadDate("employee.idIssue.month", "employee.idIssue.monthCustom");
            record.Employee.IdIssueYear = Read("employee.idIssue.year");
            record.Employee.IdExpiryDay = ReadDate("employee.idExpiry.day", "employee.idExpiry.dayCustom");
            record.Employee.IdExpiryMonth = ReadDate("employee.idExpiry.month", "employee.idExpiry.monthCustom");
            record.Employee.IdExpiryYear = Read("employee.idExpiry.year");
            record.Employee.EducationLevel = Read("employee.educationLevel");
            record.Employee.Qualification = Read("employee.qualification");
            record.Procurement.ContractNumber = Read("procurement.contractNumber");
            record.Procurement.ContractDay = Read("procurement.contractDay");
            record.Procurement.ContractMonth = Read("procurement.contractMonth");
            record.Procurement.ContractYear = Read("procurement.contractYear");
            record.Procurement.Bank = Read("procurement.bank");
            record.Procurement.BankBranch = Read("procurement.bankBranch");
            record.Procurement.BankAccountName = Read("procurement.bankAccountName");
            record.Procurement.BankAccountNumber = Read("procurement.bankAccountNumber");
            for (var i = 0; i < 3; i++) record.Committee[i] = ReadCommitteePerson("committee." + i);
            var selectedPosition = positionBox == null ? null : positionBox.SelectedItem as PositionView;
            record.Procurement.PositionId = selectedPosition == null || selectedPosition.Config == null ? "" : selectedPosition.Config.id;
            record.Procurement.HasTeaching = teachingBox != null && teachingBox.Checked;
            record.Procurement.WorksAtTwoSchools = twoSchoolsBox != null && twoSchoolsBox.Checked;
            record.Procurement.SecondSchoolName = Read("procurement.secondSchool");
            record.Procurement.SpecificationOrder = Read("procurement.specificationOrder");
            record.Procurement.SelectedDocuments = documentChecks.Where(x => x.Checked).Select(x => (string)x.Tag).ToList();
        }

        private PersonRecord ReadPerson(string prefix)
        {
            return new PersonRecord { Prefix = Read(prefix + ".prefix"), GivenName = Read(prefix + ".given"), Surname = Read(prefix + ".surname"), Position = Read(prefix + ".position") };
        }

        private CommitteeMemberRecord ReadCommitteePerson(string prefix)
        {
            var person = ReadPerson(prefix);
            return new CommitteeMemberRecord { Prefix = person.Prefix, GivenName = person.GivenName, Surname = person.Surname, Position = person.Position };
        }

        private string Read(string key)
        {
            Control control;
            if (!fields.TryGetValue(key, out control)) return "";
            var combo = control as ComboBox;
            var value = combo == null ? control.Text : combo.Text;
            return CleanInput(value);
        }

        private string ReadDate(string key, string customKey)
        {
            ComboBox combo;
            if (!dateCombos.TryGetValue(key, out combo)) return "";
            var value = combo.Text.StartsWith("......", StringComparison.Ordinal) ? dateCustomBoxes[customKey].Text : combo.Text;
            return CleanInput(value);
        }

        private static string CleanInput(string value)
        {
            value = (value ?? "").Trim();
            if (value.StartsWith("......", StringComparison.Ordinal) || value == "--" || value == UnspecifiedOption || value == PositionPlaceholder) return "";
            return value;
        }

        private void RefreshFromRecord()
        {
            suppressChanges = true;
            try
            {
                Set("school.name", record.School.Name);
                Set("school.district", record.School.District);
                Set("school.subdistrict", record.School.Subdistrict);
                Set("school.amphoe", record.School.Amphoe);
                Set("school.province", record.School.Province);
                SetPerson("school.director", record.School.Director);
                SetPerson("school.supply", record.School.SupplyOfficer);
                SetPerson("school.headSupply", record.School.HeadSupplyOfficer);
                Set("employee.prefix", record.Employee.Prefix);
                Set("employee.given", record.Employee.GivenName);
                Set("employee.surname", record.Employee.Surname);
                Set("employee.nationalId", record.Employee.NationalId);
                SetDate("employee.birth.day", "employee.birth.dayCustom", record.Employee.BirthDay);
                SetDate("employee.birth.month", "employee.birth.monthCustom", record.Employee.BirthMonth);
                Set("employee.birth.year", record.Employee.BirthYear);
                Set("employee.age", record.Employee.Age);
                Set("employee.nationality", record.Employee.Nationality);
                Set("employee.race", record.Employee.Race);
                Set("employee.religion", record.Employee.Religion);
                Set("employee.houseNumber", record.Employee.HouseNumber);
                Set("employee.road", record.Employee.Road);
                Set("employee.subdistrict", record.Employee.Subdistrict);
                Set("employee.district", record.Employee.District);
                Set("employee.province", record.Employee.Province);
                Set("employee.idIssueDistrict", record.Employee.IdIssueDistrict);
                Set("employee.idIssueProvince", record.Employee.IdIssueProvince);
                SetDate("employee.idIssue.day", "employee.idIssue.dayCustom", record.Employee.IdIssueDay);
                SetDate("employee.idIssue.month", "employee.idIssue.monthCustom", record.Employee.IdIssueMonth);
                Set("employee.idIssue.year", record.Employee.IdIssueYear);
                SetDate("employee.idExpiry.day", "employee.idExpiry.dayCustom", record.Employee.IdExpiryDay);
                SetDate("employee.idExpiry.month", "employee.idExpiry.monthCustom", record.Employee.IdExpiryMonth);
                Set("employee.idExpiry.year", record.Employee.IdExpiryYear);
                Set("employee.educationLevel", record.Employee.EducationLevel);
                Set("employee.qualification", record.Employee.Qualification);
                Set("procurement.contractNumber", record.Procurement.ContractNumber);
                Set("procurement.contractDay", record.Procurement.ContractDay);
                Set("procurement.contractMonth", record.Procurement.ContractMonth);
                Set("procurement.contractYear", record.Procurement.ContractYear);
                Set("procurement.bank", record.Procurement.Bank);
                Set("procurement.bankBranch", record.Procurement.BankBranch);
                Set("procurement.bankAccountName", record.Procurement.BankAccountName);
                Set("procurement.bankAccountNumber", record.Procurement.BankAccountNumber);
                for (var i = 0; i < 3; i++) SetPerson("committee." + i, record.Committee[i]);
                SelectPosition(record.Procurement.PositionId);
                teachingBox.Checked = record.Procurement.HasTeaching;
                twoSchoolsBox.Checked = record.Procurement.WorksAtTwoSchools;
                Set("procurement.secondSchool", record.Procurement.SecondSchoolName);
                Set("procurement.specificationOrder", record.Procurement.SpecificationOrder);
                UpdateRouteControls();
                isDirty = false;
            }
            finally
            {
                suppressChanges = false;
            }
            if (sectionList != null) sectionList.Invalidate();
        }

        private void SetPerson(string prefix, PersonRecord person)
        {
            person = person ?? new PersonRecord();
            Set(prefix + ".prefix", person.Prefix);
            Set(prefix + ".given", person.GivenName);
            Set(prefix + ".surname", person.Surname);
            Set(prefix + ".position", person.Position);
        }

        private void Set(string key, string value)
        {
            Control control;
            if (!fields.TryGetValue(key, out control)) return;
            var combo = control as ComboBox;
            if (combo != null && (string.IsNullOrWhiteSpace(value) || value.Trim() == "--") && combo.Items.Contains(UnspecifiedOption))
            {
                combo.SelectedIndex = 0;
                return;
            }
            control.Text = value ?? "";
        }

        private void SetDate(string key, string customKey, string value)
        {
            ComboBox combo;
            if (!dateCombos.TryGetValue(key, out combo)) return;
            var text = value ?? "";
            if (text.Length == 0) combo.SelectedIndex = 0;
            else if (combo.Items.Contains(text)) combo.Text = text;
            else
            {
                combo.Text = "......(กรอกเอง)";
                dateCustomBoxes[customKey].Text = text;
            }
            dateCustomBoxes[customKey].Visible = combo.Text.StartsWith("......", StringComparison.Ordinal);
        }

        private void SelectPosition(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                positionBox.SelectedIndex = 0;
                return;
            }
            for (var i = 0; i < positionBox.Items.Count; i++)
            {
                var item = positionBox.Items[i] as PositionView;
                if (item != null && item.Config != null && item.Config.id == id) { positionBox.SelectedIndex = i; return; }
            }
            positionBox.SelectedIndex = -1;
        }

        private ProcurementPositionConfig CurrentPosition()
        {
            var item = positionBox == null ? null : positionBox.SelectedItem as PositionView;
            return item == null ? null : item.Config;
        }

        private ProcurementManifestRoute FindRoute(WorkingRecord source)
        {
            if (source == null || manifest.routes == null) return null;
            return manifest.routes.FirstOrDefault(x =>
                x.positionId == source.Procurement.PositionId
                && x.hasTeaching == source.Procurement.HasTeaching
                && x.worksAtTwoSchools == source.Procurement.WorksAtTwoSchools);
        }
        private ProcurementManifestRoute CurrentRoute()
        {
            var position = CurrentPosition();
            if (position == null || manifest.routes == null) return null;
            var route = manifest.routes.FirstOrDefault(x => x.positionId == position.id && x.hasTeaching == teachingBox.Checked && x.worksAtTwoSchools == twoSchoolsBox.Checked);
            return route;
        }

        private void UpdateRouteControls()
        {
            if (positionBox == null) return;
            var position = CurrentPosition();
            var teachingEnabled = position != null && position.teachingFlag;
            var twoSchoolsEnabled = position != null && position.twoSchoolsFlag;
            teachingBox.Enabled = teachingEnabled;
            twoSchoolsBox.Enabled = twoSchoolsEnabled;
            if (!teachingEnabled) teachingBox.Checked = false;
            if (!twoSchoolsEnabled) twoSchoolsBox.Checked = false;
            LayoutEmployeeOptionalBlock();
            if (!twoSchoolsBox.Checked) secondSchoolBox.Text = "";
            if (position == null)
            {
                salaryLabel.Text = "";
                salaryTextLabel.Text = "";
                if (totalSalaryCaption != null) totalSalaryCaption.Text = "เงินรวม / อากรแสตมป์ที่ต้องติด";
                totalSalaryLabel.Text = "";
            }
            else
            {
                salaryLabel.Text = position.salary + " บาท";
                salaryTextLabel.Text = position.salaryText;
                var contractMonths = position.contractMonths > 0 ? position.contractMonths : 12;
                if (totalSalaryCaption != null) totalSalaryCaption.Text = "เงินรวม " + contractMonths + " เดือน / อากรแสตมป์ที่ต้องติด";
                totalSalaryLabel.Text = position.totalSalary + " บาท / อากรแสตมป์ " + DutyTax(position.totalSalary) + " บาท";
            }
            BuildDocumentChecklist();
        }

        private void BuildDocumentChecklist()
        {
            if (documentPanel == null) return;
            documentPanel.Controls.Clear();
            documentChecks.Clear();
            documentPanel.Controls.Add(new Label { Text = "สรุปชุดเอกสารที่ต้องการสร้าง (เริ่มต้นเลือกครบ 10 รายการ)", Location = new Point(14, 12), Size = new Size(640, 24), Font = new Font("Tahoma", 10.5f, FontStyle.Bold), ForeColor = ModuleAccentDark });
            var route = CurrentRoute();
            var paths = new List<string>();
            if (route != null && route.documents != null && route.documents.Length > 0)
            {
                paths.AddRange(route.documents);
            }
            else
            {
                if (manifest.centralDocuments != null) paths.AddRange(manifest.centralDocuments.Select(x => x.relativePath));
                if (route != null) paths.Add(route.tor);
                if (route != null) paths.Add(route.quote);
            }
            paths = paths.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(GetDocumentSortKey)
                .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var y = 46;
            foreach (var path in paths)
            {
                var check = new CheckBox { Text = Path.GetFileName(path), Tag = path, Checked = true, Location = new Point(14, y), Size = new Size(620, 24) };
                check.CheckedChanged += delegate { OnUiChanged(); };
                documentPanel.Controls.Add(check);
                documentChecks.Add(check);
                y += 28;
            }
            if (paths.Count == 0) documentPanel.Controls.Add(new Label { Text = "เลือกตำแหน่งเพื่อแสดงชุดเอกสาร", Location = new Point(14, y), Size = new Size(400, 24) });
        }

        private static string GetDocumentSortKey(string path)
        {
            var fileName = Path.GetFileName(path ?? "");
            var match = Regex.Match(fileName, "^\\s*(\\d+(?:\\.\\d+)*)");
            if (!match.Success) return "99999999";
            var parts = match.Groups[1].Value.Split('.');
            return string.Join(".", parts.Select(x => int.Parse(x).ToString("D8")).ToArray());
        }

        private List<string> ValidateRecord(WorkingRecord candidate)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(candidate.School.Name)) errors.Add("ชื่อโรงเรียน");
            if (string.IsNullOrWhiteSpace(candidate.School.District)) errors.Add("เขตพื้นที่การศึกษา");
            if (string.IsNullOrWhiteSpace(candidate.School.Subdistrict)) errors.Add("ตำบลโรงเรียน");
            if (string.IsNullOrWhiteSpace(candidate.School.Amphoe)) errors.Add("อำเภอโรงเรียน");
            if (string.IsNullOrWhiteSpace(candidate.School.Province)) errors.Add("จังหวัดโรงเรียน");
            RequirePerson(errors, "ผู้อำนวยการโรงเรียน", candidate.School.Director);
            RequirePerson(errors, "เจ้าหน้าที่พัสดุ", candidate.School.SupplyOfficer);
            RequirePerson(errors, "หัวหน้าเจ้าหน้าที่พัสดุ", candidate.School.HeadSupplyOfficer);
            if (string.IsNullOrWhiteSpace(candidate.Employee.GivenName)) errors.Add("ชื่อลูกจ้าง");
            if (string.IsNullOrWhiteSpace(candidate.Employee.Surname)) errors.Add("นามสกุลลูกจ้าง");
            if (!Regex.IsMatch(candidate.Employee.NationalId ?? "", "^[0-9]{13}$")) errors.Add("เลขประจำตัวประชาชนต้องเป็นตัวเลข 13 หลัก");
            if (string.IsNullOrWhiteSpace(candidate.Procurement.ContractNumber)) errors.Add("เลขที่สัญญา");
            if (string.IsNullOrWhiteSpace(candidate.Procurement.Bank)) errors.Add("ธนาคาร");
            if (string.IsNullOrWhiteSpace(candidate.Procurement.BankBranch)) errors.Add("สาขาธนาคาร");
            if (string.IsNullOrWhiteSpace(candidate.Procurement.BankAccountName)) errors.Add("ชื่อบัญชีธนาคาร");
            if (string.IsNullOrWhiteSpace(candidate.Procurement.BankAccountNumber)) errors.Add("เลขบัญชี");
            var position = positions.FirstOrDefault(x => x.id == candidate.Procurement.PositionId);
            if (position == null) errors.Add("ตำแหน่ง");
            else
            {
                decimal monthlySalary;
                if (!decimal.TryParse((position.salary ?? "").Replace(",", ""), out monthlySalary) || monthlySalary <= 0) errors.Add("เงินเดือน");
                if (string.IsNullOrWhiteSpace(position.salaryText)) errors.Add("เงินเดือนภาษาไทย");
                if (string.IsNullOrWhiteSpace(position.totalSalary) || string.IsNullOrWhiteSpace(position.totalSalaryText)) errors.Add("เงินรวม 12 เดือน");
            }
            var pageRoute = FindRoute(candidate);
            if (pageRoute == null) errors.Add("ชุดเอกสารของตำแหน่ง");
            else
            {
                if (pageRoute.torPages <= 0) errors.Add("จำนวนหน้า TOR ของตำแหน่ง");
                if (pageRoute.quotePages <= 0) errors.Add("จำนวนหน้าใบเสนอราคาของตำแหน่ง");
            }
            if (candidate.Procurement.SelectedDocuments == null || candidate.Procurement.SelectedDocuments.Count == 0) errors.Add("เลือกเอกสารอย่างน้อย 1 รายการ");
            if (position != null && position.twoSchoolsFlag && candidate.Procurement.WorksAtTwoSchools && string.IsNullOrWhiteSpace(candidate.Procurement.SecondSchoolName)) errors.Add("ชื่อโรงเรียนที่สอง");
            for (var i = 0; i < candidate.Committee.Length; i++)
            {
                var member = candidate.Committee[i];
                if (member == null || string.IsNullOrWhiteSpace(member.Prefix) || string.IsNullOrWhiteSpace(member.GivenName) || string.IsNullOrWhiteSpace(member.Surname) || string.IsNullOrWhiteSpace(member.Position)) errors.Add("กรรมการ " + (i + 1));
            }
            return errors;
        }

        private static void RequirePerson(List<string> errors, string label, PersonRecord person)
        {
            if (person == null
                || string.IsNullOrWhiteSpace(person.Prefix)
                || string.IsNullOrWhiteSpace(person.GivenName)
                || string.IsNullOrWhiteSpace(person.Surname))
            {
                errors.Add(label);
            }
        }
        private void GenerateDocuments()
        {
            CaptureFromControls();
            var candidate = store.Clone(record);
            NormalizeRecord(candidate);
            var errors = ValidateRecord(candidate);
            errorProvider.Clear();
            if (errors.Count > 0)
            {
                MessageBox.Show("กรุณาแก้ไข:\n- " + string.Join("\n- ", errors), "ข้อมูลยังไม่ครบ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var route = FindRoute(candidate);
            if (route == null)
            {
                MessageBox.Show("ไม่พบ route ที่ตรงกับตำแหน่งและตัวเลือกที่เลือก", "Routing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var values = BuildValues(candidate);
            var templateRoot = SidecontractPaths.EnsureUserTemplateRoot(DocumentModule.Procurement);
            var outputRoot = SidecontractPaths.OutputRoot(DocumentModule.Procurement);
            var created = new List<string>();
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                foreach (var relative in candidate.Procurement.SelectedDocuments)
                {
                    var source = SidecontractPaths.ResolveUnder(templateRoot, relative);
                    var requested = Path.Combine(outputRoot, SafeFilePart(candidate.Employee.GivenName) + "_" + SafeFilePart(Path.GetFileNameWithoutExtension(relative)) + ".docx");
                    created.Add(SidecontractDocxRenderer.Render(source, requested, values));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("สร้างเอกสารไม่สำเร็จ: " + ex.Message, "สร้างเอกสาร", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
            var openOutput = MessageBox.Show(
                "สร้างเอกสารครบ " + created.Count + " ไฟล์แล้ว\n" + outputRoot + "\n\nกรุณาตรวจสอบข้อมูล และจัดหน้ากระดาษให้เรียบร้อยด้วยนะจ๊ะ\n\nต้องการเปิดโฟลเดอร์ output ตอนนี้หรือไม่?",
                "สำเร็จ",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);
            if (openOutput == DialogResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = outputRoot,
                    UseShellExecute = true
                });
            }
        }

        private Dictionary<string, string> BuildValues(WorkingRecord source)
        {
            var position = positions.First(x => x.id == source.Procurement.PositionId);
            var route = FindRoute(source);
            var values = new Dictionary<string, string>();
            Put(values, "{ชื่อโรงเรียน}", NormalizeSchoolName(source.School.Name));
            Put(values, "{ชื่อโรงเรียนสอง}", source.Procurement.WorksAtTwoSchools ? NormalizeSchoolName(source.Procurement.SecondSchoolName) : "");
            Put(values, "{ชื่อเขต}", NormalizeDistrictName(source.School.District));
            Put(values, "{ตำบลโรงเรียน}", StripPrefix(source.School.Subdistrict, "ตำบล"));
            Put(values, "{อำเภอโรงเรียน}", StripPrefix(source.School.Amphoe, "อำเภอ"));
            Put(values, "{จังหวัดโรงเรียน}", StripPrefix(source.School.Province, "จังหวัด"));
            var torPages = route == null ? 0 : route.torPages;
            var quotePages = route == null ? 0 : route.quotePages;
            Put(values, "{หน้าA}", torPages > 0 ? torPages.ToString() : "", false);
            Put(values, "{หน้าB}", torPages > 0 ? ThaiNumberText(torPages) : "", false);
            Put(values, "{หน้าC}", quotePages > 0 ? quotePages.ToString() : "", false);
            Put(values, "{หน้าD}", quotePages > 0 ? ThaiNumberText(quotePages) : "", false);
            Put(values, "{ใบสั่งจ้าง}", source.Procurement.ContractNumber, false);
            Put(values, "{วันทำสัญญา}", ContractDateValue(source.Procurement.ContractDay), false);
            Put(values, "{เดือนทำสัญญา}", ContractDateValue(source.Procurement.ContractMonth), false);
            Put(values, "{ปีทำสัญญา}", ContractDateValue(source.Procurement.ContractYear), false);
            Put(values, "{ธนาคาร}", source.Procurement.Bank, false);
            Put(values, "{สาขาธนาคาร}", source.Procurement.BankBranch, false);
            Put(values, "{ชื่อบัญชีธนาคาร}", source.Procurement.BankAccountName, false);
            Put(values, "{เลขบัญชี}", source.Procurement.BankAccountNumber, false);
            PutPerson(values, "ผอ", source.School.Director);
            PutPerson(values, "พัสดุ", source.School.SupplyOfficer);
            PutPerson(values, "หพัสดุ", source.School.HeadSupplyOfficer);
            Put(values, "{คำนำหน้าลูกจ้าง}", source.Employee.Prefix);
            Put(values, "{ชื่อลูกจ้าง}", source.Employee.GivenName);
            Put(values, "{นามสกุลลูกจ้าง}", source.Employee.Surname);
            Put(values, "{เลขประจำตัว}", source.Employee.NationalId, false);
            Put(values, "{ตำแหน่ง}", position.label);
            Put(values, "{เงินเดือน}", position.salary);
            Put(values, "{เงินเดือนTEXT}", position.salaryText);
            Put(values, "{เงินรวม}", position.totalSalary);
            Put(values, "{เงินรวมTEXT}", position.totalSalaryText);
            Put(values, "{เงินเดือนหารสามสิบ}", SalaryPerDay(position.salary), false);
            Put(values, "{เงินเดือนหารสามสิบTEXT}", SalaryPerDayText(position.salary), false);
            Put(values, "{บาทอากร}", DutyTax(position.totalSalary), false);
            Put(values, "{คำสั่งสเปค}", source.Procurement.SpecificationOrder);
            for (var i = 0; i < 3; i++)
            {
                var member = source.Committee[i] ?? new CommitteeMemberRecord();
                var suffix = ((char)('A' + i)).ToString();
                Put(values, "{คำนำหน้าสเปค" + suffix + "}", member.Prefix);
                Put(values, "{ชื่อสเปค" + suffix + "}", member.GivenName);
                Put(values, "{นามสกุลสเปค" + suffix + "}", member.Surname);
                Put(values, "{ตำแหน่งสเปค" + suffix + "}", member.Position);
            }
            Put(values, "{สัญชาติ}", source.Employee.Nationality);
            Put(values, "{เชื้อชาติ}", source.Employee.Race);
            Put(values, "{ศาสนา}", source.Employee.Religion);
            Put(values, "{เกิดวันที่}", source.Employee.BirthDay);
            Put(values, "{เดือนเกิด}", source.Employee.BirthMonth);
            Put(values, "{ปีเกิด}", source.Employee.BirthYear);
            Put(values, "{อายุลูกจ้าง}", source.Employee.Age);
            Put(values, "{ออกอำเภอ}", StripPrefix(source.Employee.IdIssueDistrict, "อำเภอ"));
            Put(values, "{ออกจังหวัด}", StripPrefix(source.Employee.IdIssueProvince, "จังหวัด"));
            Put(values, "{วันที่ออกบัตร}", source.Employee.IdIssueDay);
            Put(values, "{เดือนออกบัตร}", source.Employee.IdIssueMonth);
            Put(values, "{ปีออกบัตร}", source.Employee.IdIssueYear);
            Put(values, "{วันบัตรหมดอายุ}", source.Employee.IdExpiryDay);
            Put(values, "{เดือนบัตรหมดอายุ}", source.Employee.IdExpiryMonth);
            Put(values, "{ปีบัตรหมดอายุ}", source.Employee.IdExpiryYear);
            Put(values, "{สำเร็จการศึกษาระดับ}", source.Employee.EducationLevel);
            Put(values, "{คุณวุฒิการศึกษา}", source.Employee.Qualification);
            Put(values, "{บ้านเลขที่ลูกจ้าง}", source.Employee.HouseNumber);
            Put(values, "{ถนนลูกจ้าง}", source.Employee.Road);
            Put(values, "{ตำบลลูกจ้าง}", StripPrefix(source.Employee.Subdistrict, "ตำบล"));
            Put(values, "{อำเภอลูกจ้าง}", StripPrefix(source.Employee.District, "อำเภอ"));
            Put(values, "{จังหวัดลูกจ้าง}", StripPrefix(source.Employee.Province, "จังหวัด"));
            return values;
        }

        private void PutPerson(Dictionary<string, string> values, string suffix, PersonRecord person)
        {
            person = person ?? new PersonRecord();
            Put(values, "{คำนำหน้า" + suffix + "}", person.Prefix);
            Put(values, "{ชื่อ" + suffix + "}", person.GivenName);
            Put(values, "{นามสกุล" + suffix + "}", person.Surname);
        }

        private static string ContractDateValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? ContractDatePlaceholder : value.Trim();
        }

        private static void Put(Dictionary<string, string> values, string tag, string value, bool optional = true)
        {
            value = (value ?? "").Trim();
            values[tag] = optional && value.Length == 0 ? EmptyValue : value;
        }

        private static void NormalizeRecord(WorkingRecord source)
        {
            source.School.Name = NormalizeSchoolName(source.School.Name);
            source.School.District = NormalizeDistrictName(source.School.District);
            source.School.Subdistrict = (source.School.Subdistrict ?? "").Trim();
            source.School.Amphoe = (source.School.Amphoe ?? "").Trim();
            source.School.Province = (source.School.Province ?? "").Trim();
            source.Procurement.SecondSchoolName = NormalizeSchoolName(source.Procurement.SecondSchoolName);
            source.Employee.NationalId = (source.Employee.NationalId ?? "").Trim();
        }

        private static string NormalizeSchoolName(string input)
        {
            var value = (input ?? "").Trim();
            if (value.Length == 0) return "";
            while (value.StartsWith("โรงเรียนโรงเรียน", StringComparison.Ordinal)) value = value.Substring("โรงเรียน".Length).TrimStart();
            return value.StartsWith("โรงเรียน", StringComparison.Ordinal) ? value : "โรงเรียน" + value;
        }

        private static string NormalizeDistrictName(string input)
        {
            var value = Regex.Replace((input ?? "").Trim(), "\\s+", " ");
            if (value.Length == 0) return DefaultDistrict;
            if (value.StartsWith(DistrictPrefix, StringComparison.Ordinal)) return value;
            if (value.StartsWith("ประถมศึกษา", StringComparison.Ordinal) || value.StartsWith("มัธยมศึกษา", StringComparison.Ordinal)) return DistrictPrefix + value;
            return DistrictPrefix + "ประถมศึกษา" + value;
        }

        private static string StripPrefix(string input, string prefix)
        {
            var value = (input ?? "").Trim();
            while (value.StartsWith(prefix + prefix, StringComparison.Ordinal)) value = value.Substring(prefix.Length).TrimStart();
            return value.StartsWith(prefix, StringComparison.Ordinal) ? value.Substring(prefix.Length).TrimStart() : value;
        }

        private static string SalaryPerDay(string salary)
        {
            decimal amount;
            if (!decimal.TryParse((salary ?? "").Replace(",", ""), out amount)) return "";
            return decimal.Truncate(amount / 30m).ToString("0") + " บาท";
        }

        private static string SalaryPerDayText(string salary)
        {
            decimal amount;
            if (!decimal.TryParse((salary ?? "").Replace(",", ""), out amount)) return "";
            var daily = decimal.Truncate(amount / 30m);
            return ThaiNumberText((long)daily) + "บาทถ้วน";
        }

        private static string ThaiNumberText(long value)
        {
            if (value == 0) return "ศูนย์";
            if (value < 0) return "ลบ" + ThaiNumberText(-value);
            if (value >= 1000000)
            {
                var millions = value / 1000000;
                var remainder = value % 1000000;
                return ThaiNumberText(millions) + "ล้าน" + (remainder == 0 ? "" : ThaiNumberText(remainder));
            }

            var digits = new[] { "ศูนย์", "หนึ่ง", "สอง", "สาม", "สี่", "ห้า", "หก", "เจ็ด", "แปด", "เก้า" };
            var units = new[] { "", "สิบ", "ร้อย", "พัน", "หมื่น", "แสน" };
            var text = "";
            var divisor = 100000;
            for (var position = 5; position >= 0; position--)
            {
                var digit = (int)(value / divisor);
                value %= divisor;
                if (digit == 0)
                {
                    divisor /= 10;
                    continue;
                }

                if (position == 1)
                {
                    if (digit == 1) text += "สิบ";
                    else if (digit == 2) text += "ยี่สิบ";
                    else text += digits[digit] + "สิบ";
                }
                else if (position == 0 && digit == 1 && text.Length > 0)
                {
                    text += "เอ็ด";
                }
                else
                {
                    text += digits[digit] + units[position];
                }
                divisor /= 10;
            }
            return text;
        }
        private static string DutyTax(string totalSalary)
        {
            decimal amount;
            if (!decimal.TryParse((totalSalary ?? "").Replace(",", ""), out amount)) return "";
            return decimal.Truncate(amount / 1000m).ToString("0");
        }

        private static string SafeFilePart(string value)
        {
            var safe = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars()) safe = safe.Replace(invalid, '_');
            return safe.Length > 80 ? safe.Substring(0, 80) : safe;
        }

        private void ClearCurrentSection()
        {
            var index = sectionList == null ? 0 : sectionList.SelectedIndex;
            suppressChanges = true;
            try
            {
                var keys = fields.Keys.Where(x => (index == 0 && x.StartsWith("school.", StringComparison.Ordinal))
                    || (index == 1 && (x.StartsWith("committee.", StringComparison.Ordinal) || x == "procurement.specificationOrder"))
                    || (index == 2 && (x.StartsWith("employee.", StringComparison.Ordinal) || x == "procurement.position" || x == "procurement.secondSchool"))
                    || (index == 3 && (x == "procurement.contractNumber" || x == "procurement.contractDay" || x == "procurement.contractMonth" || x == "procurement.contractYear" || x == "procurement.bank" || x == "procurement.bankBranch" || x == "procurement.bankAccountName" || x == "procurement.bankAccountNumber"))).ToArray();
                foreach (var key in keys) Set(key, "");
                if (index == 2)
                {
                    foreach (var combo in dateCombos.Values) combo.SelectedIndex = 0;
                    foreach (var custom in dateCustomBoxes.Values) custom.Text = "";
                }
                if (index == 2)
                {
                    positionBox.SelectedIndex = 0;
                    teachingBox.Checked = false;
                    twoSchoolsBox.Checked = false;
                    UpdateRouteControls();
                }
                if (index == 4)
                {
                    foreach (var check in documentChecks) check.Checked = true;
                }
                CaptureFromControls();
                isDirty = true;
            }
            finally
            {
                suppressChanges = false;
            }
            if (sharedChanged != null) sharedChanged();
        }

        private void AddNewEmployee()
        {
            CaptureFromControls();
            var answer = MessageBox.Show("ต้องการบันทึกข้อมูลปัจจุบันเป็น Template ของ " + (record.Employee.GivenName + " " + record.Employee.Surname).Trim() + " ก่อนเริ่มลูกจ้างใหม่หรือไม่?", "เพิ่มลูกจ้างใหม่", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel) return;
            if (answer == DialogResult.Yes)
            {
                var result = SaveCurrentTemplate();
                if (result != TemplateOperationResult.Saved) return;
            }
            var empty = WorkingRecord.CreateEmpty();
            record.RecordId = empty.RecordId;
            record.School = empty.School;
            record.Employee = empty.Employee;
            record.Committee = empty.Committee;
            record.Payroll = empty.Payroll;
            record.Procurement = empty.Procurement;
            SetDefaults();
            RefreshFromRecord();
            isDirty = false;
            if (sharedChanged != null) sharedChanged();
        }

        private void LoadTemplateFlow()
        {
            var procurementTemplates = (storeData.Templates ?? new List<SavedTemplateSnapshot>())
                .Select(TemplateTransferService.FromProcurement)
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .ToList();
            var payrollTemplates = TemplateTransferService.LoadPayrollTemplates();
            if (procurementTemplates.Count == 0 && payrollTemplates.Count == 0)
            {
                MessageBox.Show("ยังไม่มี Template ให้เลือกโหลด", "โหลด Template", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var picker = new CrossTemplatePickerDialog(DocumentModule.Procurement, procurementTemplates, payrollTemplates))
            {
                if (picker.ShowDialog(this) != DialogResult.OK || picker.Selected == null) return;
                var selected = picker.Selected;
                if (selected.SourceModule == DocumentModule.Procurement)
                {
                    var local = (storeData.Templates ?? new List<SavedTemplateSnapshot>()).FirstOrDefault(x => x.Id == selected.Id);
                    if (local == null) return;
                    ApplyLoadedRecord(store.Clone(local.Record));
                    MessageBox.Show("โหลด Template จัดซื้อจัดจ้างฯ แล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var report = TemplateTransferService.BuildReport(selected, DocumentModule.Procurement);
                using (var notice = new CrossTemplateImportNoticeDialog(selected, DocumentModule.Procurement, report))
                {
                    if (notice.ShowDialog(this) != DialogResult.OK) return;
                }
                ApplyImportedProcurementValues(selected);
                MessageBox.Show("โหลดข้อมูลร่วมจาก Template เบิกเงินเดือนแล้ว และเขียนทับข้อมูลร่วมทั้งหมดตามรายการ", "โหลด Template สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void DeleteTemplateFlow()
        {
            if (storeData.Templates == null || !storeData.Templates.Any(x => x != null && !string.IsNullOrWhiteSpace(x.Name)))
            {
                MessageBox.Show("ยังไม่มี Template ให้ลบ", "ลบ Template", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var draft = store.Clone(storeData);
            using (var dialog = new ProcurementTemplateDeleteDialog(draft.Templates))
            {
                dialog.ShowDialog(this);
                if (!dialog.IsDirty) return;
                try
                {
                    store.Save(draft);
                    storeData = draft;
                    MessageBox.Show("ลบ Template แล้ว", "สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (SidecontractStoreException ex)
                {
                    MessageBox.Show(ex.Message, "ลบ Template ไม่สำเร็จ", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ApplyLoadedRecord(WorkingRecord loaded)
        {
            if (loaded == null) return;
            record.RecordId = loaded.RecordId;
            record.School = loaded.School;
            record.Employee = loaded.Employee;
            record.Committee = loaded.Committee;
            record.Payroll = loaded.Payroll;
            record.Procurement = loaded.Procurement;
            RefreshFromRecord();
            isDirty = false;
            if (sharedChanged != null) sharedChanged();
        }

        private void ApplyImportedProcurementValues(TemplateTransferItem item)
        {
            var values = TemplateTransferService.GetImportableValues(item);
            if (values.Count == 0)
            {
                MessageBox.Show("Template ต้นทางไม่มีข้อมูลร่วมที่โหลดได้", "โหลด Template", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string value;
            if (values.TryGetValue("school.name", out value)) record.School.Name = value;
            if (values.TryGetValue("school.district", out value)) record.School.District = value;
            if (record.School.Director == null) record.School.Director = new PersonRecord();
            if (record.School.SupplyOfficer == null) record.School.SupplyOfficer = new PersonRecord();
            if (record.School.HeadSupplyOfficer == null) record.School.HeadSupplyOfficer = new PersonRecord();
            ApplyImportedPerson(values, "school.director", record.School.Director);
            ApplyImportedPerson(values, "school.supply", record.School.SupplyOfficer);
            ApplyImportedPerson(values, "school.headSupply", record.School.HeadSupplyOfficer);

            if (values.TryGetValue("employee.prefix", out value)) record.Employee.Prefix = value;
            if (values.TryGetValue("employee.given", out value)) record.Employee.GivenName = value;
            if (values.TryGetValue("employee.surname", out value)) record.Employee.Surname = value;
            if (values.TryGetValue("employee.position", out value))
            {
                var positionId = TemplateTransferService.MapPayrollPositionToProcurement(value);
                if (!string.IsNullOrWhiteSpace(positionId)) SelectPosition(positionId);
            }
            if (values.TryGetValue("employee.nationalId", out value)) record.Employee.NationalId = value;
            if (values.TryGetValue("employee.birth.day", out value)) record.Employee.BirthDay = value;
            if (values.TryGetValue("employee.birth.month", out value)) record.Employee.BirthMonth = value;
            if (values.TryGetValue("employee.birth.year", out value)) record.Employee.BirthYear = value;
            if (values.TryGetValue("employee.age", out value)) record.Employee.Age = value;
            if (values.TryGetValue("employee.nationality", out value)) record.Employee.Nationality = value;
            if (values.TryGetValue("employee.race", out value)) record.Employee.Race = value;
            if (values.TryGetValue("employee.religion", out value)) record.Employee.Religion = value;
            if (values.TryGetValue("employee.idIssueDistrict", out value)) record.Employee.IdIssueDistrict = value;
            if (values.TryGetValue("employee.idIssueProvince", out value)) record.Employee.IdIssueProvince = value;
            if (values.TryGetValue("employee.idIssue.day", out value)) record.Employee.IdIssueDay = value;
            if (values.TryGetValue("employee.idIssue.month", out value)) record.Employee.IdIssueMonth = value;
            if (values.TryGetValue("employee.idIssue.year", out value)) record.Employee.IdIssueYear = value;
            if (values.TryGetValue("employee.idExpiry.day", out value)) record.Employee.IdExpiryDay = value;
            if (values.TryGetValue("employee.idExpiry.month", out value)) record.Employee.IdExpiryMonth = value;
            if (values.TryGetValue("employee.idExpiry.year", out value)) record.Employee.IdExpiryYear = value;
            if (values.TryGetValue("employee.educationLevel", out value)) record.Employee.EducationLevel = value;
            if (values.TryGetValue("employee.qualification", out value)) record.Employee.Qualification = value;
            if (values.TryGetValue("employee.houseNumber", out value)) record.Employee.HouseNumber = value;
            if (values.TryGetValue("employee.road", out value)) record.Employee.Road = value;
            if (values.TryGetValue("employee.subdistrict", out value)) record.Employee.Subdistrict = value;
            if (values.TryGetValue("employee.district", out value)) record.Employee.District = value;
            if (values.TryGetValue("employee.province", out value)) record.Employee.Province = value;
            for (var i = 0; i < 3; i++)
            {
                if (record.Committee[i] == null) record.Committee[i] = new CommitteeMemberRecord();
                ApplyImportedPerson(values, "committee." + i, record.Committee[i]);
            }

            RefreshFromRecord();
            isDirty = true;
            if (sharedChanged != null) sharedChanged();
        }

        private void ApplyImportedPerson(Dictionary<string, string> values, string key, PersonRecord target)
        {
            if (target == null) return;
            string value;
            if (values.TryGetValue(key + ".prefix", out value)) target.Prefix = value;
            if (values.TryGetValue(key + ".given", out value)) target.GivenName = value;
            if (values.TryGetValue(key + ".surname", out value)) target.Surname = value;
        }

        private sealed class PositionView
        {
            public readonly ProcurementPositionConfig Config;
            public PositionView(ProcurementPositionConfig config) { Config = config; }
            public override string ToString() { return Config == null ? PositionPlaceholder : Config.label; }
        }
    }

    internal sealed class ProcurementTemplateDialog : Form
    {
        private readonly TextBox nameBox = new TextBox();
        private readonly TextBox noteBox = new TextBox();
        public string TemplateName { get { return nameBox.Text.Trim(); } }
        public string TemplateNote { get { return noteBox.Text.Trim(); } }

        public ProcurementTemplateDialog(string title, string initialName)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(460, 235);
            Controls.Add(new Label { Text = "ชื่อรายการ Template *", Location = new Point(20, 20), Size = new Size(190, 22) });
            nameBox.Location = new Point(20, 48); nameBox.Size = new Size(420, 26); nameBox.Text = initialName ?? ""; Controls.Add(nameBox);
            Controls.Add(new Label { Text = "หมายเหตุ (ถ้ามี)", Location = new Point(20, 88), Size = new Size(190, 22) });
            noteBox.Location = new Point(20, 116); noteBox.Size = new Size(420, 54); noteBox.Multiline = true; Controls.Add(noteBox);
            var ok = new Button { Text = "บันทึก", Location = new Point(274, 188), Size = new Size(80, 30) };
            var cancel = new Button { Text = "ยกเลิก", Location = new Point(360, 188), Size = new Size(80, 30), DialogResult = DialogResult.Cancel };
            ok.Click += delegate { if (TemplateName.Length == 0) { MessageBox.Show("กรุณากรอกชื่อรายการ", "ข้อมูลไม่ครบ", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; } DialogResult = DialogResult.OK; Close(); };
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
        }
    }

    internal sealed class ProcurementTemplateDeleteDialog : Form
    {
        private readonly IList<SavedTemplateSnapshot> templates;
        private readonly ListBox list = new ListBox();
        public bool IsDirty { get; private set; }

        public ProcurementTemplateDeleteDialog(IList<SavedTemplateSnapshot> templates)
        {
            this.templates = templates ?? new List<SavedTemplateSnapshot>();
            Text = "ลบ Template";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false; MaximizeBox = false; ClientSize = new Size(480, 330);
            Controls.Add(new Label { Text = "เลือก Template ที่ต้องการลบ", Location = new Point(20, 16), Size = new Size(240, 20) });
            list.Location = new Point(20, 44);
            list.Size = new Size(440, 220);
            list.DisplayMember = "Name";
            Controls.Add(list);

            var delete = new Button { Text = "ลบรายการที่เลือก", Location = new Point(260, 280), Size = new Size(110, 30) };
            var cancel = new Button { Text = "ปิด", Location = new Point(380, 280), Size = new Size(80, 30), DialogResult = DialogResult.Cancel };
            delete.Click += delegate
            {
                var selected = list.SelectedItem as SavedTemplateSnapshot;
                if (selected == null)
                {
                    MessageBox.Show("กรุณาเลือกรายการ", "ยังไม่ได้เลือก", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (MessageBox.Show("ยืนยันการลบ Template \"" + selected.Name + "\" ?", "ยืนยันการลบ", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                templates.Remove(selected);
                IsDirty = true;
                RebindList();
            };
            Controls.Add(delete);
            Controls.Add(cancel);
            CancelButton = cancel;
            RebindList();
        }

        private void RebindList()
        {
            list.DataSource = null;
            list.DataSource = templates.Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name)).ToList();
            list.DisplayMember = "Name";
        }
    }
}
