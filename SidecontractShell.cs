using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace Sidecontract
{
    internal static class SidecontractShellProgram
    {
        [STAThread]
        private static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += HandleThreadException;
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SidecontractShellForm());
            }
            catch (Exception ex)
            {
                ReportException(ex, true);
            }
        }

        private static void HandleThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ReportException(e.Exception, true);
        }

        private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            ReportException(exception ?? new Exception(Convert.ToString(e.ExceptionObject)), !e.IsTerminating);
        }

        private static void ReportException(Exception ex, bool showDialog)
        {
            var details = ex == null ? "ไม่ทราบรายละเอียดข้อผิดพลาด" : ex.ToString();
            try
            {
                Directory.CreateDirectory(SidecontractPaths.DataRoot);
                File.WriteAllText(Path.Combine(SidecontractPaths.DataRoot, "startup-error.log"), DateTime.Now.ToString("O") + Environment.NewLine + details);
            }
            catch
            {
            }
            if (!showDialog) return;
            try
            {
                MessageBox.Show("เปิด sidecontract ไม่สำเร็จ\n\n" + details, "sidecontract เกิดข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }
    internal sealed class SidecontractShellForm : Form
    {
        private readonly TabControl tabs = new TabControl();
        private Form payrollForm;
        private ProcurementModule procurementModule;
        private bool closingConfirmed;
        private readonly Button updateButton = new Button();
        private bool updateInProgress;
        private string pendingInstaller;
        private UpdateManifest pendingManifest;
        private string availableUpdateVersion;
        private bool startupUpdateChecked;

        public SidecontractShellForm()
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = "sidecontract " + SidecontractUpdate.VersionText;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new System.Drawing.Size(1220, 850);
            MinimumSize = new System.Drawing.Size(800, 500);
            BackColor = Color.FromArgb(243, 246, 248);
            Font = new Font("Segoe UI", 10f);
            tabs.Dock = DockStyle.Fill;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.ItemSize = new Size(210, 30);
            tabs.Padding = new Point(16, 2);
            tabs.DrawItem += DrawTab;
            Controls.Add(tabs);
            BuildUpdateBar();

            BuildProcurementTab();
            BuildPayrollTab();
            tabs.SelectedIndex = 0;
            FormClosing += HandleFormClosing;
            FormClosed += delegate
            {
                if (pendingInstaller == null) return;
                try { SidecontractUpdate.StartInstaller(pendingInstaller, pendingManifest); }
                catch (Exception ex) { MessageBox.Show("เปิดตัวติดตั้งไม่สำเร็จ กรุณาเปิดโปรแกรมแล้วลองอัปเดตใหม่\n\n" + ex.Message, "อัปเดต sidecontract", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            Shown += async delegate
            {
                try
                {
                    var result = SidecontractUpdate.ReadUpdateResult();
                    if (result != null) MessageBox.Show(this, result, "ผลการอัปเดต sidecontract");
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "ตรวจสอบการอัปเดต", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                await CheckStartupUpdateAsync();
            };
        }

        private async System.Threading.Tasks.Task CheckStartupUpdateAsync()
        {
            if (startupUpdateChecked) return;
            startupUpdateChecked = true;
            try
            {
                var manifest = await SidecontractUpdate.CheckAsync();
                if (IsDisposed || Disposing) return;
                availableUpdateVersion = SidecontractUpdate.NeedsUpdate(manifest) ? manifest.version : null;
                if (!updateInProgress) RefreshUpdateButton();
            }
            catch
            {
                // Startup checks must not interrupt offline document work.
                // The explicit update action reports download/network errors.
            }
        }

        private void RefreshUpdateButton()
        {
            updateButton.Text = availableUpdateVersion == null ? "อัปเดตโปรแกรม" : "มีอัปเดตใหม่! " + availableUpdateVersion;
            updateButton.UseVisualStyleBackColor = availableUpdateVersion == null;
            updateButton.BackColor = availableUpdateVersion == null ? SystemColors.Control : Color.FromArgb(255, 243, 205);
            updateButton.ForeColor = availableUpdateVersion == null ? SystemColors.ControlText : Color.FromArgb(120, 68, 8);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(800, area.Width), Math.Min(500, area.Height));
            Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
            Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2), area.Top + Math.Max(0, (area.Height - Height) / 2));
        }

        private TabPage CreateModulePage(string title, Control content)
        {
            // Absolute-positioned legacy forms need a scrollable canvas, not a
            // docked minimum-sized child that extends outside the visible tab.
            var page = new TabPage(title) { BackColor = Color.FromArgb(243, 246, 248), AutoScroll = true };
            var scale = 1f;
            using (var graphics = content.CreateGraphics()) scale = graphics.DpiX / 96f;
            var minimum = new Size((int)Math.Ceiling(1080 * scale), (int)Math.Ceiling(560 * scale));
            content.MinimumSize = Size.Empty;
            content.Dock = DockStyle.None;
            content.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            content.Location = Point.Empty;
            page.AutoScrollMinSize = minimum;
            page.Controls.Add(content);
            page.ClientSizeChanged += delegate
            {
                var target = new Size(Math.Max(minimum.Width, page.ClientSize.Width), Math.Max(minimum.Height, page.ClientSize.Height));
                if (content.Size != target) content.Size = target;
            };
            content.Size = minimum;
            return page;
        }

        private void BuildUpdateBar()
        {
            var bar = new Panel { Dock = DockStyle.Top, Width = ClientSize.Width, Height = 46, BackColor = Color.White };
            var version = new Label { Text = "sidecontract  |  รุ่น " + SidecontractUpdate.VersionText, AutoSize = true, Location = new Point(14, 13) };
            updateButton.Text = "อัปเดตโปรแกรม";
            updateButton.Size = new Size(180, 32);
            updateButton.Location = new Point(ClientSize.Width - 194, 7);
            updateButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            updateButton.Click += async delegate { await UpdateProgramAsync(); };
            var details = new Button { Text = "ข้อมูลโปรแกรม", Size = new Size(130, 32), Location = new Point(ClientSize.Width - 334, 7), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            details.Click += delegate
            {
                var hash = File.Exists(SidecontractUpdate.DatabasePath) ? SidecontractUpdate.HashFile(SidecontractUpdate.DatabasePath) : "ไม่พบไฟล์";
                MessageBox.Show(this, "รุ่นโปรแกรม: " + SidecontractUpdate.VersionText + "\n\nโปรแกรมที่เปิดอยู่:\n" + Application.ExecutablePath + "\n\nตารางวันที่ส่งเบิก:\n" + SidecontractUpdate.DatabasePath + "\n\nSHA256 ตาราง:\n" + hash, "ข้อมูล sidecontract");
            };
            bar.Controls.Add(version);
            bar.Controls.Add(details);
            bar.Controls.Add(updateButton);
            Controls.Add(bar);
        }

        private async System.Threading.Tasks.Task UpdateProgramAsync()
        {
            updateInProgress = true;
            updateButton.Enabled = false;
            updateButton.Text = "กำลังตรวจสอบ...";
            try
            {
                var manifest = await SidecontractUpdate.CheckAsync();
                var needsUpdate = SidecontractUpdate.NeedsUpdate(manifest);
                availableUpdateVersion = needsUpdate ? manifest.version : null;
                if (!needsUpdate)
                {
                    MessageBox.Show(this, "รุ่นปัจจุบัน " + SidecontractUpdate.VersionText + " ไม่ต้องอัปเดต", "อัปเดต sidecontract");
                    return;
                }
                if (MessageBox.Show(this, "พร้อมติดตั้งรุ่น " + manifest.version + "\nโปรแกรมจะดาวน์โหลดตัวติดตั้ง แล้วให้บันทึกงานก่อนปิด\nหลังติดตั้งเสร็จ เลือกเปิด sidecontract อีกครั้ง\n\nต้องการดำเนินการหรือไม่?", "อัปเดต sidecontract", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                pendingInstaller = await SidecontractUpdate.DownloadAsync(manifest, delegate(int percent) { updateButton.Text = "ดาวน์โหลด " + percent + "%"; });
                pendingManifest = manifest;
                updateInProgress = false;
                Close(); // Existing save/cancel flow runs before FormClosed launches the installer.
                if (!IsDisposed) { pendingInstaller = null; pendingManifest = null; }
            }
            catch (Exception ex)
            {
                pendingInstaller = null;
                pendingManifest = null;
                MessageBox.Show(this, "อัปเดตไม่สำเร็จ โปรแกรมและข้อมูลเดิมยังใช้งานได้\nกรุณาตรวจสอบอินเทอร์เน็ต หรือลองใหม่ภายหลัง\n\n" + ex.Message, "อัปเดต sidecontract", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                updateInProgress = false;
                if (!IsDisposed) { updateButton.Enabled = true; RefreshUpdateButton(); }
            }
        }

        private void DrawTab(object sender, DrawItemEventArgs e)
        {
            var selected = e.Index == tabs.SelectedIndex;
            var accent = e.Index == 0 ? Color.FromArgb(15, 118, 110) : Color.FromArgb(31, 94, 140);
            var background = selected ? accent : Color.White;
            var foreground = selected ? Color.White : Color.FromArgb(30, 41, 59);
            using (var backgroundBrush = new SolidBrush(background))
            using (var foregroundBrush = new SolidBrush(foreground))
                using (var font = new Font(tabs.Font, selected ? FontStyle.Bold : FontStyle.Regular))
            {
                e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
                var textBounds = new Rectangle(e.Bounds.Left + 8, Math.Max(0, e.Bounds.Top + 3), e.Bounds.Width - 16, Math.Max(1, e.Bounds.Height - 6));
                e.Graphics.DrawString(tabs.TabPages[e.Index].Text, font, foregroundBrush, textBounds, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }
            e.DrawFocusRectangle();
        }

        private void BuildPayrollTab()
        {
            payrollForm = CreateLegacyPayrollForm();
            payrollForm.TopLevel = false;
            payrollForm.FormBorderStyle = FormBorderStyle.None;
            var page = CreateModulePage("เบิกเงินเดือน", payrollForm);
            tabs.TabPages.Add(page);
            payrollForm.Show();
        }

        private void BuildProcurementTab()
        {
            procurementModule = new ProcurementModule(WorkingRecord.CreateEmpty(), null);
            var page = CreateModulePage("จัดซื้อจัดจ้างและสัญญา", procurementModule);
            tabs.TabPages.Add(page);
        }

        private Form CreateLegacyPayrollForm()
        {
            var programType = typeof(Program);
            var loadTemplates = programType.GetMethod("LoadTemplates", BindingFlags.NonPublic | BindingFlags.Static);
            var templates = loadTemplates.Invoke(null, null);
            var mainFormType = programType.GetNestedType("MainForm", BindingFlags.NonPublic);
            var form = (Form)Activator.CreateInstance(mainFormType, new[] { templates });
            var templateBox = GetPrivateField(form, "templateBox") as TextBox;
            var outputBox = GetPrivateField(form, "outputBox") as TextBox;
            var templateRoot = SidecontractPaths.EnsureUserTemplateRoot(DocumentModule.Payroll);
            if (templateBox != null) templateBox.Text = templateRoot;
            if (outputBox != null) outputBox.Text = SidecontractPaths.OutputRoot(DocumentModule.Payroll);
            return form;
        }

        private void HandleFormClosing(object sender, FormClosingEventArgs e)
        {
            if (updateInProgress)
            {
                e.Cancel = true;
                MessageBox.Show(this, "กำลังตรวจสอบหรือดาวน์โหลดอัปเดต กรุณารอให้เสร็จก่อนปิดโปรแกรม", "อัปเดต sidecontract");
                return;
            }
            if (closingConfirmed || procurementModule == null) return;
            var procurementHasChanges = procurementModule.IsDirty;
            var payrollHasChanges = IsPayrollDirty();
            var choice = MessageBox.Show("ก่อนออกจากโปรแกรม ต้องการบันทึก Template ที่จัดทำค้างไว้หรือไม่?\nใช่ = บันทึกข้อมูลที่แก้ไข, ไม่ใช่ = ออกโดยไม่บันทึก, ยกเลิกหรือกากบาท = กลับไปทำงาน", "ปิด sidecontract", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (choice == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (choice == DialogResult.Yes)
            {
                if (payrollHasChanges && !SavePayrollDraftForExit())
                {
                    e.Cancel = true;
                    return;
                }
                if (procurementHasChanges)
                {
                    var result = procurementModule.SaveCurrentTemplate();
                    if (result != TemplateOperationResult.Saved)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
            closingConfirmed = true;
        }

        private bool IsPayrollDirty()
        {
            if (payrollForm == null) return false;
            var property = payrollForm.GetType().GetProperty("IsDirty", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null) return false;
            var value = property.GetValue(payrollForm, null);
            return value is bool && (bool)value;
        }

        private bool SavePayrollDraftForExit()
        {
            if (payrollForm == null) return true;
            var method = payrollForm.GetType().GetMethod("SaveDraftForExit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) return true;
            try
            {
                var result = method.Invoke(payrollForm, null);
                return !(result is bool) || (bool)result;
            }
            catch (TargetInvocationException ex)
            {
                var message = ex.InnerException == null ? ex.Message : ex.InnerException.Message;
                MessageBox.Show("บันทึกข้อมูลเบิกเงินเดือนก่อนปิดไม่สำเร็จ: " + message, "บันทึกข้อมูล", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("บันทึกข้อมูลเบิกเงินเดือนก่อนปิดไม่สำเร็จ: " + ex.Message, "บันทึกข้อมูล", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private static object GetPrivateField(object instance, string name)
        {
            if (instance == null) return null;
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(instance);
        }

    }
}
