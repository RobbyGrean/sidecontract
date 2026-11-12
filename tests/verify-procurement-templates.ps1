$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root ('qa\procurement-templates-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$exe = Join-Path $output 'ProcurementTemplateRegression.exe'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /codepage:65001 /target:exe "/out:$exe" /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll (Join-Path $root 'Shared\DocxRenderer.cs') (Join-Path $PSScriptRoot 'ProcurementTemplateRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Procurement template regression compile failed' }
& $exe (Join-Path $root 'Template\Procurement') (Join-Path $output 'generated')
if ($LASTEXITCODE -ne 0) { throw 'Procurement template regression failed' }
Write-Host "Generated test documents: $output\generated"
