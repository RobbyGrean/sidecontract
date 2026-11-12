$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root ('qa\updater-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$exe = Join-Path $output 'UpdaterRegression.exe'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /codepage:65001 /target:exe "/out:$exe" /reference:System.Web.Extensions.dll (Join-Path $root 'Shared\sidecontractUpdate.cs') (Join-Path $PSScriptRoot 'UpdaterRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Updater regression compile failed' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Updater regression failed' }
