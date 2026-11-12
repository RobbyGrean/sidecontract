$ErrorActionPreference = 'Stop'

$root = (Resolve-Path $PSScriptRoot).Path
$identity = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root 'Config\app_identity.json') | ConvertFrom-Json
$payrollManifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root 'Config\Payroll\template_manifest.json') | ConvertFrom-Json
$procurementManifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root 'Config\Procurement\template_manifest.json') | ConvertFrom-Json

function Resolve-ContainedPath([string]$relativePath) {
    if ([string]::IsNullOrWhiteSpace($relativePath) -or [IO.Path]::IsPathRooted($relativePath)) {
        throw "Manifest path must be relative: $relativePath"
    }
    $full = [IO.Path]::GetFullPath((Join-Path $root ($relativePath.Replace('/', '\'))))
    $rootWithSlash = $root.TrimEnd('\') + '\'
    if (-not $full.StartsWith($rootWithSlash, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Manifest path escapes workspace: $relativePath"
    }
    return $full
}

function Assert-ManifestPath([string]$relativePath, [string]$moduleRoot) {
    if ([string]::IsNullOrWhiteSpace($relativePath) -or $relativePath.Contains('..')) {
        throw "Invalid $moduleRoot manifest path: $relativePath"
    }
    $path = Resolve-ContainedPath ((Join-Path $moduleRoot $relativePath).Replace('\', '/'))
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing manifest document: $path"
    }
    return $relativePath
}

function Assert-Document([object]$document, [string]$moduleRoot) {
    $relative = [string]$document.relativePath
    $path = Resolve-ContainedPath ((Join-Path $moduleRoot $relative).Replace('\', '/'))
    if ([string]::IsNullOrWhiteSpace($relative) -or $relative.Contains('..')) {
        throw "Invalid $moduleRoot manifest path: $relative"
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing manifest document: $path"
    }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    if ($actual -ne ([string]$document.sha256).ToLowerInvariant()) {
        throw "Hash mismatch: $path actual=$actual expected=$($document.sha256)"
    }
    return $relative
}

$payrollRoot = [string]$identity.moduleRoots.Payroll.template
$payrollPaths = @($payrollManifest.documents | ForEach-Object { Assert-Document $_ $payrollRoot })
if ($payrollPaths.Count -ne 5 -or @($payrollPaths | Sort-Object -Unique).Count -ne 5) {
    throw 'Payroll manifest must contain five unique documents.'
}

$procurementRoot = [string]$identity.moduleRoots.Procurement.template
$procurementPaths = @()
$procurementPaths += @($procurementManifest.centralDocuments | ForEach-Object { Assert-Document $_ $procurementRoot })
foreach ($route in $procurementManifest.routes) {
    if ([string]::IsNullOrWhiteSpace([string]$route.tor) -or [string]::IsNullOrWhiteSpace([string]$route.quote)) {
        throw "Route is missing a TOR/quote pair: $($route.id)"
    }
    $tor = [pscustomobject]@{ relativePath = [string]$route.tor; sha256 = [string]$route.torSha256 }
    $quote = [pscustomobject]@{ relativePath = [string]$route.quote; sha256 = [string]$route.quoteSha256 }
    $torPath = Assert-Document $tor $procurementRoot
    $quotePath = Assert-Document $quote $procurementRoot
    if ($null -ne $route.documents -and @($route.documents).Count -gt 0) {
        $routePaths = @($route.documents | ForEach-Object { Assert-ManifestPath ([string]$_) $procurementRoot })
        if ($routePaths.Count -ne @($routePaths | Sort-Object -Unique).Count) {
            throw "Route contains duplicate documents: $($route.id)"
        }
        if ((@($routePaths | Where-Object { $_ -eq $torPath }).Count -ne 1) -or (@($routePaths | Where-Object { $_ -eq $quotePath }).Count -ne 1)) {
            throw "Route documents must include its TOR and quote: $($route.id)"
        }
        $procurementPaths += $routePaths
    }
    else {
        $procurementPaths += $torPath
        $procurementPaths += $quotePath
    }
}
if (@($procurementManifest.routes).Count -ne 15) { throw 'Procurement manifest must contain fifteen routes.' }
$uniqueProcurementPaths = @($procurementPaths | Sort-Object -Unique)
if ($uniqueProcurementPaths.Count -ne 46) {
    throw 'Procurement manifest must resolve to forty-six unique documents.'
}

$allDocx = @(Get-ChildItem -LiteralPath (Resolve-ContainedPath $procurementRoot) -Recurse -File -Filter '*.docx')
if ($allDocx.Count -ne 46) { throw "Procurement template root contains $($allDocx.Count) DOCX files; expected 46." }

$routeIds = @($procurementManifest.routes | ForEach-Object { [string]$_.id })
if (@($routeIds | Sort-Object -Unique).Count -ne 15) { throw 'Procurement route IDs must be unique.' }

Write-Output "Manifest OK: Payroll=$($payrollPaths.Count), Procurement=$($uniqueProcurementPaths.Count), Routes=$(@($procurementManifest.routes).Count), AppId=$($identity.appId)"