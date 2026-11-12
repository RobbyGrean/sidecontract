$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root ('qa\display-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item -Path (Join-Path $root 'dist\sidecontract\*') -Destination $output -Recurse -Force
# Test the real forms while isolating all preferences, templates and user data.
$core = [IO.File]::ReadAllText((Join-Path $root 'Shared\sidecontractCore.cs'), [Text.Encoding]::UTF8)
$core = $core.Replace('Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppId', 'AppDomain.CurrentDomain.BaseDirectory, "isolated-user"')
$testCore = Join-Path $output 'TestCore.cs'
[IO.File]::WriteAllText($testCore, $core, (New-Object Text.UTF8Encoding($true)))
$testApp = Join-Path $output 'TestPayrollApp.cs'
$app = [IO.File]::ReadAllText((Join-Path $root 'sidecontractApp.cs'), [Text.Encoding]::UTF8).Replace('DateTime.Today', 'DisplayRegression.Today').Replace('Process.Start(new ProcessStartInfo', 'DisplayRegression.StartProcess(new ProcessStartInfo')
[IO.File]::WriteAllText($testApp, $app, (New-Object Text.UTF8Encoding($true)))
$testShell = Join-Path $output 'TestShell.cs'
$shell = [IO.File]::ReadAllText((Join-Path $root 'sidecontractShell.cs'), [Text.Encoding]::UTF8).Replace('await sidecontractUpdate.CheckAsync()', 'await DisplayRegression.CheckAsync()')
[IO.File]::WriteAllText($testShell, $shell, (New-Object Text.UTF8Encoding($true)))
$sources = @('Shared\sidecontractConfig.cs', 'Shared\TemplateTransfer.cs', 'Shared\DocxRenderer.cs', 'Shared\sidecontractUpdate.cs', 'Modules\Procurement\ProcurementModule.cs', 'tests\DisplayRegression.cs') | ForEach-Object { Join-Path $root $_ }
$sources += $testShell
$sources += $testApp
$sources += $testCore
$exe = Join-Path $output 'DisplayRegression.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /codepage:65001 /target:exe /main:sidecontract.DisplayRegression "/out:$exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.Extensions.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Display regression compile failed' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Display regression failed' }
Write-Host "Display test screenshots: $output"
