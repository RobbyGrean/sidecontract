using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows.Forms;

namespace Sidecontract
{
    internal static class DisplayRegression
    {
        public static DateTime Today = DateTime.Today;
        public static int StartupChecks;
        public static bool FailStartupCheck;
        public static string LastOpenedFolder;
        public static Process StartProcess(ProcessStartInfo info)
        {
            if (!info.UseShellExecute) throw new Exception("Folder must open with the system shell");
            LastOpenedFolder = info.FileName;
            return null;
        }
        public static Task<UpdateManifest> CheckAsync()
        {
            StartupChecks++;
            if (FailStartupCheck) throw new WebException("offline test");
            return Task.FromResult(new UpdateManifest { schemaVersion = 1, version = "99.0.0", installerUrl = SidecontractUpdate.InstallerUrl, installerSha256 = new string('A', 64), databaseSha256 = new string('A', 64) });
        }
        private static Button FindGenerate(Control root)
        {
            foreach (Control child in root.Controls)
            {
                var button = child as Button;
                if (button != null && button.Text.Contains("ตรวจสอบและสร้าง")) return button;
                var found = FindGenerate(child);
                if (found != null) return found;
            }
            return null;
        }

        [STAThread]
        private static int Main()
        {
            try
            {
                Application.EnableVisualStyles();
                var cases = new[] {
                    new { Today = new DateTime(2026, 11, 3), Month = "ตุลาคม", Year = 2569 },
                    new { Today = new DateTime(2027, 1, 3), Month = "ธันวาคม", Year = 2569 },
                    new { Today = new DateTime(2026, 10, 1), Month = "กันยายน", Year = 2569 },
                    new { Today = new DateTime(2028, 3, 31), Month = "กุมภาพันธ์", Year = 2571 }
                };
                foreach (var test in cases)
                {
                    Today = test.Today;
                    using (var shell = new SidecontractShellForm())
                    {
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        var payroll = typeof(SidecontractShellForm).GetField("payrollForm", flags).GetValue(shell);
                        var month = (ComboBox)payroll.GetType().GetField("fiscalMonthBox", flags).GetValue(payroll);
                        var year = (NumericUpDown)payroll.GetType().GetField("fiscalYearBox", flags).GetValue(payroll);
                        var fields = (Dictionary<string, Control>)payroll.GetType().GetField("fieldBoxes", flags).GetValue(payroll);
                        var districtTag = (string)payroll.GetType().DeclaringType.GetField("TagDistrictName", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                        var district = (ComboBox)fields[districtTag];
                        var schoolTag = (string)payroll.GetType().DeclaringType.GetField("TagSchoolName", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                        var school = fields[schoolTag];
                        if (district.Top <= school.Bottom || district.Width < 500) throw new Exception("Payroll district field is not on a full-width separate row");
                        foreach (var option in district.Items)
                        {
                            var fullName = option.ToString();
                            var measured = TextRenderer.MeasureText(fullName, district.Font).Width;
                            if (district.DropDownWidth < measured + SystemInformation.VerticalScrollBarWidth + 8) throw new Exception("District dropdown clips a full name");
                            district.SelectedItem = option;
                            var value = (string)payroll.GetType().GetMethod("GetControlValue", flags).Invoke(payroll, new object[] { district });
                            if (value != fullName) throw new Exception("District selection changed its document value");
                        }
                        if ((string)month.SelectedItem != test.Month || year.Value != test.Year)
                            throw new Exception("Incorrect previous delivery month/year for " + Today.ToString("yyyy-MM-dd"));
                        Console.WriteLine("PASS startup " + Today.ToString("yyyy-MM-dd") + " -> " + test.Month + " " + test.Year);
                    }
                }
                Today = DateTime.Today;
                using (var form = new SidecontractShellForm())
                {
                    form.Opacity = 0;
                    form.ShowInTaskbar = false;
                    form.Show();
                    Application.DoEvents();
                    var update = (Button)typeof(SidecontractShellForm).GetField("updateButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    if (StartupChecks != 1 || !update.Text.Contains("มีอัปเดตใหม่!")) throw new Exception("Startup update notification missing");
                    Console.WriteLine("PASS startup check highlights a newer version");
                    var tabs = form.Controls.OfType<TabControl>().Single();
                    tabs.SelectedIndex = 1;
                    Application.DoEvents();
                    var payrollForm = (Form)typeof(SidecontractShellForm).GetField("payrollForm", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    var folderButton = (Button)payrollForm.Controls.Find("openPayrollOutputButton", true).Single();
                    var outputBox = (TextBox)payrollForm.GetType().GetField("outputBox", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(payrollForm);
                    var customOutput = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ผลลัพธ์ ทดสอบ", "เงินเดือน");
                    outputBox.Text = customOutput;
                    folderButton.PerformClick();
                    if (LastOpenedFolder != customOutput || !Directory.Exists(customOutput)) throw new Exception("Folder button ignored the configured payroll output path");
                    Console.WriteLine("PASS payroll folder button opens the configured custom path and creates a missing directory");
                    foreach (var size in new[] { new Size(1280, 680), new Size(1024, 680), new Size(960, 600), new Size(800, 500) })
                    {
                        form.Size = size;
                        Application.DoEvents();
                        if (form.Width > size.Width || form.Height > size.Height) throw new Exception("Window minimum exceeds available area: " + size + " actual=" + form.Size);
                        for (var i = 0; i < tabs.TabCount; i++)
                        {
                            tabs.SelectedIndex = i;
                            Application.DoEvents();
                            var page = tabs.TabPages[i];
                            var generate = FindGenerate(page);
                            if (generate == null) throw new Exception("Generate button missing: " + page.Text);
                            page.ScrollControlIntoView(generate);
                            Application.DoEvents();
                            var bounds = page.RectangleToClient(generate.RectangleToScreen(generate.ClientRectangle));
                            if (!page.ClientRectangle.Contains(bounds) || !generate.Visible || !generate.Enabled)
                                throw new Exception("Generate button cannot be reached: " + size + " tab=" + page.Text + " button=" + bounds + " viewport=" + page.ClientRectangle);
                            Console.WriteLine("PASS " + size + " " + page.Text + ": generate button reachable");
                            if (i == 1)
                            {
                                page.ScrollControlIntoView(folderButton);
                                Application.DoEvents();
                                var folderBounds = page.RectangleToClient(folderButton.RectangleToScreen(folderButton.ClientRectangle));
                                if (!page.ClientRectangle.Contains(folderBounds) || !folderButton.Visible) throw new Exception("Payroll output button cannot be reached on small screen");
                                var checks = (Dictionary<string, CheckBox>)payrollForm.GetType().GetField("templateChecks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(payrollForm);
                                var last = checks.Values.OrderBy(c => c.Top).Last();
                                if (last.Parent.Bottom > folderButton.Top) throw new Exception("Template list overlaps the output button");
                                ((ScrollableControl)last.Parent).ScrollControlIntoView(last);
                                if (!last.Parent.ClientRectangle.Contains(last.Bounds)) throw new Exception("Final template checkbox is clipped");
                            }
                            if (size.Width == 1024)
                            {
                                using (var bitmap = new Bitmap(form.Width, form.Height))
                                {
                                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "small-screen-tab-" + i + ".png"));
                                }
                            }
                        }
                    }
                    if (StartupChecks != 1) throw new Exception("Startup check repeated on tab switch/resize");
                    Console.WriteLine("PASS startup check runs once across tab switches and resizing");
                }
                FailStartupCheck = true;
                using (var offline = new SidecontractShellForm())
                {
                    offline.Opacity = 0;
                    offline.ShowInTaskbar = false;
                    offline.Show();
                    Application.DoEvents();
                    var update = (Button)typeof(SidecontractShellForm).GetField("updateButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(offline);
                    if (!offline.Visible || !update.Enabled || update.Text != "อัปเดตโปรแกรม") throw new Exception("Offline startup blocked normal work");
                    Console.WriteLine("PASS offline startup keeps the application usable without notification dialogs");
                }
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
