$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$qaRoot = Join-Path $root ('qa\installer-upgrade-' + [Guid]::NewGuid().ToString('N'))
$target = Join-Path $qaRoot 'installed'
New-Item -ItemType Directory -Path $target -Force | Out-Null
$dist = Join-Path $root 'dist\sidecontract'

# Preserve the shipped installer file/backup rules. Isolate registration and
# shortcuts so this test cannot change the user's actual installed application.
$iss = [IO.File]::ReadAllText((Join-Path $root 'sidecontract-Setup.iss'), [Text.Encoding]::UTF8)
$iss = $iss.Replace('#define DistRoot "dist\sidecontract"', ('#define DistRoot "' + $dist + '"'))
$iss = $iss.Replace('SetupIconFile=app-icon.ico', ('SetupIconFile=' + (Join-Path $root 'app-icon.ico')))
$iss = $iss.Replace('AppId=sidecontract', 'AppId=sidecontract-PayrollUpgrade-QA')
$iss = $iss.Replace('Uninstallable=yes', "Uninstallable=no`r`nCreateUninstallRegKey=no`r`nUsePreviousAppDir=no")
$iss = [regex]::Replace($iss, '(?ms)^\[Icons\].*?(?=^\[Run\])', '')
$iss = [regex]::Replace($iss, '(?ms)^\[Run\].*?(?=^\[Code\])', '')
$qaIss = Join-Path $qaRoot 'upgrade-test.iss'
[IO.File]::WriteAllText($qaIss, $iss, (New-Object Text.UTF8Encoding($true)))
$compiler = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
& $compiler '/Q' "/O$qaRoot" $qaIss
if ($LASTEXITCODE -ne 0) { throw 'Isolated Inno Setup compile failed' }

$oldDatabase = [IO.File]::ReadAllText((Join-Path $root 'Config\Payroll\app_database.json'), [Text.Encoding]::UTF8)
$oldDatabase = $oldDatabase.Replace('"2570":  "2 พฤศจิกายน 2569"', '"2569":  "2 พฤศจิกายน 2569"').Replace('"2570":  "1 ธันวาคม 2569"', '"2569":  "1 ธันวาคม 2569"').Replace('"2570":  "4 มกราคม 2570"', '"2569":  "4 มกราคม 2570"')
New-Item -ItemType Directory -Path (Join-Path $target 'Config\Payroll') -Force | Out-Null
foreach ($relative in @('app_database.json', 'Config\Payroll\app_database.json')) {
    [IO.File]::WriteAllText((Join-Path $target $relative), $oldDatabase, (New-Object Text.UTF8Encoding($false)))
    # Even a newer local file must be replaced by the installer's ignoreversion rule.
    (Get-Item -LiteralPath (Join-Path $target $relative)).LastWriteTime = [DateTime]::Now.AddDays(1)
}
foreach ($relative in @('Data\keep.txt', 'Output\keep.txt', 'Template\keep.txt')) {
    $path = Join-Path $target $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    [IO.File]::WriteAllText($path, 'user-content-must-survive')
}
$log = Join-Path $qaRoot 'install.log'
$setup = Join-Path $qaRoot 'sidecontract-Setup.exe'
$process = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/SP-', '/NORESTART', '/NOCLOSEAPPLICATIONS', ('/DIR="' + $target + '"'), ('/LOG="' + $log + '"')) -PassThru -Wait -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "Installer failed: $($process.ExitCode). See $log" }
foreach ($relative in @('app_database.json', 'Config\Payroll\app_database.json', 'sidecontract.exe')) {
    $expectedHash = (Get-FileHash -LiteralPath (Join-Path $dist $relative) -Algorithm SHA256).Hash
    $installedHash = (Get-FileHash -LiteralPath (Join-Path $target $relative) -Algorithm SHA256).Hash
    if ($expectedHash -ne $installedHash) { throw "Installed file mismatch: $relative" }
    Write-Host "PASS installed file replaced: $relative"
}
foreach ($relative in @('Data\keep.txt', 'Output\keep.txt', 'Template\keep.txt')) {
    if ([IO.File]::ReadAllText((Join-Path $target $relative)) -ne 'user-content-must-survive') { throw "User content changed: $relative" }
}
$backups = @(Get-ChildItem -LiteralPath (Join-Path $target 'Data\TemplateBackups') -Filter keep.txt -Recurse -File)
if ($backups.Count -ne 1) { throw 'Template backup missing' }
Write-Host 'PASS user Data/Output/Template preserved and Template backup created.'
& (Join-Path $PSScriptRoot 'verify-payroll-dates.ps1') -DatabasePath (Join-Path $target 'app_database.json') -ExecutablePath (Join-Path $target 'sidecontract.exe')
Write-Host "Installer upgrade test artifacts: $qaRoot"
