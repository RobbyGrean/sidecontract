param(
    [string]$DatabasePath,
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $DatabasePath) { $DatabasePath = Join-Path $root 'Config\Payroll\app_database.json' }
$testOutput = Join-Path $root 'qa\payroll-dates'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$exe = $ExecutablePath
if (-not $exe) {
$exe = Join-Path $testOutput 'sidecontract.exe'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(
    'sidecontractApp.cs', 'Shared\sidecontractCore.cs', 'Shared\sidecontractConfig.cs',
    'Shared\TemplateTransfer.cs', 'Shared\DocxRenderer.cs', 'Shared\sidecontractUpdate.cs',
    'Modules\Procurement\ProcurementModule.cs', 'sidecontractShell.cs'
) | ForEach-Object { Join-Path $root $_ }
& $csc /nologo /codepage:65001 /target:winexe /main:sidecontract.sidecontractShellProgram "/out:$exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.Extensions.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed' }
}

Add-Type -AssemblyName System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom($exe)
$programType = $assembly.GetType('sidecontract.Program', $true)
$formType = $programType.GetNestedType('MainForm', [Reflection.BindingFlags]::NonPublic)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
$staticFlags = [Reflection.BindingFlags]'Static,NonPublic'
# Avoid the form constructor so the test never reads or writes user preferences/templates.
$form = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($formType)
$json = [IO.File]::ReadAllText($DatabasePath, [Text.Encoding]::UTF8)
$months = $formType.GetMethod('LoadMonthArray', $flags).Invoke($form, @($json, 'months'))
$formType.GetField('monthOptions', $flags).SetValue($form, $months)
$monthNames = $programType.GetField('ThaiMonths', $staticFlags).GetValue($null)
$monthBox = New-Object System.Windows.Forms.ComboBox
for ($i = 1; $i -le 12; $i++) { [void]$monthBox.Items.Add($monthNames[$i]) }
$yearBox = New-Object System.Windows.Forms.NumericUpDown
$yearBox.Minimum = 2500
$yearBox.Maximum = 2600
$fields = New-Object 'System.Collections.Generic.Dictionary[string,System.Windows.Forms.Control]'
foreach ($tagName in @('TagDeliveryMonth', 'TagDeliveryMonthShort', 'TagPayDate', 'TagPurchaseOrderYear')) {
    $tag = $programType.GetField($tagName, $staticFlags).GetValue($null)
    $fields.Add($tag, (New-Object System.Windows.Forms.TextBox))
}
$formType.GetField('fiscalMonthBox', $flags).SetValue($form, $monthBox)
$formType.GetField('fiscalYearBox', $flags).SetValue($form, $yearBox)
$formType.GetField('fieldBoxes', $flags).SetValue($form, $fields)
$formType.GetField('fiscalPreview', $flags).SetValue($form, (New-Object System.Windows.Forms.Label))
$formType.GetField('statusLabel', $flags).SetValue($form, (New-Object System.Windows.Forms.Label))
$payTag = $programType.GetField('TagPayDate', $staticFlags).GetValue($null)
$periodTag = $programType.GetField('TagDeliveryMonthShort', $staticFlags).GetValue($null)
$orderYearTag = $programType.GetField('TagPurchaseOrderYear', $staticFlags).GetValue($null)
$days = @(2, 1, 4, 1, 1, 1, 3, 1, 1, 2, 1, 1)
$failures = @()
try {
    # September FY2569, followed by every delivery period in FY2570.
    for ($period = 0; $period -le 12; $period++) {
        $month = (($period + 8) % 12) + 1
        $year = if ($period -le 3) { 2569 } else { 2570 }
        $payMonth = ($month % 12) + 1
        $payYear = if ($month -eq 12) { $year + 1 } else { $year }
        $day = if ($period -eq 0) { 1 } else { $days[$period - 1] }
        $expected = '{0} {1} {2}' -f $day, $monthNames[$payMonth], $payYear
        $monthBox.SelectedIndex = $month - 1
        $yearBox.Value = $year
        $formType.GetMethod('ApplyFiscalValues', $flags).Invoke($form, @())
        $actual = $fields[$payTag].Text
        $expectedPeriod = if ($period -eq 0) { 12 } else { $period }
        $expectedFiscalYear = if ($period -eq 0) { 2569 } else { 2570 }
        if ($actual -ne $expected -or $fields[$periodTag].Text -ne "$expectedPeriod" -or $fields[$orderYearTag].Text -ne "$expectedFiscalYear") {
            $failures += "Delivery $month/$year expected '$expected' (period $expectedPeriod FY$expectedFiscalYear), got '$actual' (period $($fields[$periodTag].Text) FY$($fields[$orderYearTag].Text))"
        }
        else { Write-Host "PASS delivery $month/$year -> $actual (period $expectedPeriod FY$expectedFiscalYear)" }
    }
    if ($failures.Count) { throw ($failures -join [Environment]::NewLine) }
    Write-Host 'PASS: all 13 selections populate the required pay-date field.'
}
finally {
    $monthBox.Dispose()
    $yearBox.Dispose()
    foreach ($control in $fields.Values) { $control.Dispose() }
    foreach ($name in @('fiscalPreview', 'statusLabel')) { $formType.GetField($name, $flags).GetValue($form).Dispose() }
    # The uninitialized form has no native window; suppress its normal finalizer.
    [GC]::SuppressFinalize($form)
}
