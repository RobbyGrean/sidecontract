$ErrorActionPreference = 'Stop'

$root = (Resolve-Path $PSScriptRoot).Path
$dist = Join-Path $root 'dist\sidecontract'
$release = Join-Path $root 'release\sidecontract-Installer'
$legacy = Join-Path $root 'release\legacy'
$setup = Join-Path $release 'sidecontract-Setup.exe'
$standardZip = Join-Path $root 'release\sidecontract-Installer-standard.zip'
$sourceZip = Join-Path $root 'release\sidecontract-Source-standard.zip'
$sourceStage = Join-Path $root 'release\sidecontract-Source-standard-stage'
$iss = Join-Path $root 'sidecontract-Setup.iss'

$iscc = (Get-Command 'iscc.exe' -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($iscc)) {
    foreach ($candidate in @(
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe',
        'C:\Users\MSI\AppData\Local\Programs\Inno Setup 6\ISCC.exe'
    )) {
        if (Test-Path -LiteralPath $candidate) {
            $iscc = $candidate
            break
        }
    }
}
if ([string]::IsNullOrWhiteSpace($iscc)) { throw 'ไม่พบ Inno Setup Compiler (ISCC.exe)' }


& (Join-Path $root 'verify-manifest.ps1')
& (Join-Path $root 'build-exe-sidecontract.ps1')
if (-not (Test-Path -LiteralPath (Join-Path $dist 'sidecontract.exe') -PathType Leaf)) {
    throw 'Built executable is missing'
}

$procurementCount = @(Get-ChildItem -LiteralPath (Join-Path $dist 'Template\Procurement') -Filter '*.docx' -Recurse -File).Count
$payrollCount = @(Get-ChildItem -LiteralPath (Join-Path $dist 'Template\Payroll') -Filter '*.docx' -Recurse -File).Count
$totalCount = $procurementCount + $payrollCount
if ($procurementCount -ne 46 -or $payrollCount -ne 5 -or $totalCount -ne 51) {
    throw "Template count mismatch: Payroll=$payrollCount Procurement=$procurementCount Total=$totalCount"
}
$obsolete = Join-Path $dist 'Template\Procurement\9. ใบสั่งจ้าง.docx'
if (Test-Path -LiteralPath $obsolete) { throw "Obsolete template is still packaged: $obsolete" }

New-Item -ItemType Directory -Path $release -Force | Out-Null
if (Test-Path -LiteralPath $setup) { Remove-Item -LiteralPath $setup -Force }
if (Test-Path -LiteralPath $standardZip) { Remove-Item -LiteralPath $standardZip -Force }

& $iscc "/O$release" $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed with exit code $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw 'Standard installer output is missing' }

Compress-Archive -LiteralPath $setup -DestinationPath $standardZip -Force

if (Test-Path -LiteralPath $sourceStage) { Remove-Item -LiteralPath $sourceStage -Recurse -Force }
New-Item -ItemType Directory -Path $sourceStage -Force | Out-Null
$excludedSourceDirectories = @('.git', 'dist', 'release', 'qa', '.qa-installer', '.qa-tor-311', '__pycache__')
Get-ChildItem -LiteralPath $root -Force |
    Where-Object { $excludedSourceDirectories -notcontains $_.Name -and $_.Name -notlike '_qa_*' -and $_.Name -notlike 'Template*new*' } |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $sourceStage -Recurse -Force }
$sourceDownloads = Join-Path $sourceStage 'assets\\downloads'
if (Test-Path -LiteralPath $sourceDownloads) { Remove-Item -LiteralPath $sourceDownloads -Recurse -Force }
if (Test-Path -LiteralPath $sourceZip) { Remove-Item -LiteralPath $sourceZip -Force }
Compress-Archive -Path $sourceStage -DestinationPath $sourceZip -Force
Remove-Item -LiteralPath $sourceStage -Recurse -Force

$checksums = Join-Path $root 'release\sidecontract-Checksums.txt'
$setupHash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
$installerZipHash = (Get-FileHash -LiteralPath $standardZip -Algorithm SHA256).Hash
$sourceZipHash = (Get-FileHash -LiteralPath $sourceZip -Algorithm SHA256).Hash
@(
    "sidecontract-Setup.exe  $setupHash",
    "sidecontract-Installer-standard.zip  $installerZipHash",
    "sidecontract-Source-standard.zip  $sourceZipHash"
) | Set-Content -LiteralPath $checksums -Encoding ASCII

$downloadRoot = Join-Path $root 'assets\downloads'
New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
Copy-Item -LiteralPath $standardZip -Destination (Join-Path $downloadRoot 'sidecontract-Installer-standard.zip') -Force
Copy-Item -LiteralPath $sourceZip -Destination (Join-Path $downloadRoot 'sidecontract-Source-standard.zip') -Force
# Keep existing website links pointed at the current standard artifacts.
Copy-Item -LiteralPath $standardZip -Destination (Join-Path $downloadRoot 'sidecontract-Installer.zip') -Force
Copy-Item -LiteralPath $sourceZip -Destination (Join-Path $downloadRoot 'sidecontract-Source.zip') -Force
Copy-Item -LiteralPath $setup -Destination (Join-Path $downloadRoot 'sidecontract-Setup.exe') -Force
Copy-Item -LiteralPath $checksums -Destination (Join-Path $downloadRoot 'sidecontract-Checksums.txt') -Force
$builtVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $dist 'sidecontract.exe')).FileVersion
$installerVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($setup).FileVersion
if ([Version]$builtVersion -ne [Version]$installerVersion) { throw "App/installer version mismatch: $builtVersion / $installerVersion" }
$updateManifest = [ordered]@{
    schemaVersion = 1
    version = ([Version]$builtVersion).ToString(3)
    installerUrl = 'https://raw.githubusercontent.com/RobbyGrean/sidecontract/main/assets/downloads/sidecontract-Setup.exe'
    installerSha256 = $setupHash
    databaseSha256 = (Get-FileHash -LiteralPath (Join-Path $dist 'app_database.json') -Algorithm SHA256).Hash
}
[IO.File]::WriteAllText((Join-Path $downloadRoot 'update.json'), ($updateManifest | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))

Write-Host "Standard installer built: $setup"
Write-Host "Standard installer archive: $standardZip"
Write-Host "Standard source archive: $sourceZip"
Write-Host "Checksums: $checksums"
Write-Host "Templates: Payroll=$payrollCount Procurement=$procurementCount Total=$totalCount"




