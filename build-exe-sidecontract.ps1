$ErrorActionPreference = 'Stop'

$root = (Resolve-Path $PSScriptRoot).Path
$dist = Join-Path $root 'dist\sidecontract'
$rootFull = [IO.Path]::GetFullPath($root).TrimEnd('\')
$distFull = [IO.Path]::GetFullPath($dist).TrimEnd('\')
if (-not $distFull.StartsWith($rootFull + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean a path outside the project: $distFull"
}
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) { $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $csc)) { throw 'ไม่พบ C# compiler (csc.exe)' }

if (Test-Path -LiteralPath $dist) { Remove-Item -LiteralPath $dist -Recurse -Force }
New-Item -ItemType Directory -Path $dist -Force | Out-Null

$sourceFiles = @(
    (Join-Path $root 'SidecontractApp.cs'),
    (Join-Path $root 'Shared\SidecontractCore.cs'),
    (Join-Path $root 'Shared\SidecontractConfig.cs'),
    (Join-Path $root 'Shared\TemplateTransfer.cs'),
    (Join-Path $root 'Shared\DocxRenderer.cs'),
    (Join-Path $root 'Shared\SidecontractUpdate.cs'),
    (Join-Path $root 'Modules\Procurement\ProcurementModule.cs'),
    (Join-Path $root 'SidecontractShell.cs')
)
foreach ($sourceFile in $sourceFiles) {
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) { throw "Source file not found: $sourceFile" }
}

& $csc /codepage:65001 /target:winexe /platform:anycpu /main:Sidecontract.SidecontractShellProgram /win32icon:"$root\app-icon.ico" /out:"$dist\sidecontract.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.Extensions.dll $sourceFiles
if ($LASTEXITCODE -ne 0) { throw "C# compiler failed with exit code $LASTEXITCODE" }

$defaultTemplate = Join-Path $dist 'Defaults\Template'
New-Item -ItemType Directory -Path (Join-Path $dist 'Config'),(Join-Path $dist 'Template'),$defaultTemplate -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'template_tags.json') -Destination (Join-Path $dist 'template_tags.json') -Force
Copy-Item -LiteralPath (Join-Path $root 'Config\Payroll\app_database.json') -Destination (Join-Path $dist 'app_database.json') -Force
Copy-Item -Path (Join-Path $root 'Config\*') -Destination (Join-Path $dist 'Config') -Recurse -Force
Copy-Item -Path (Join-Path $root 'Template\*') -Destination (Join-Path $dist 'Template') -Recurse -Force
Copy-Item -Path (Join-Path $root 'Template\*') -Destination $defaultTemplate -Recurse -Force

Write-Host "Built sidecontract package: $dist\sidecontract.exe"
