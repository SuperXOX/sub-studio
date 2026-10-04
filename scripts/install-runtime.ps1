[CmdletBinding()]
param(
    [ValidateSet('traditional','cpu','vulkan','online','model')]
    [string[]]$Components = @('traditional','cpu','vulkan','online','model'),
    [string]$OutputDirectory,
    [string]$CacheDirectory
)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$repoRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'dist\SUB Studio' }
if (!$CacheDirectory) { $CacheDirectory = Join-Path $repoRoot '.downloads' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$CacheDirectory = [IO.Path]::GetFullPath($CacheDirectory)
New-Item -ItemType Directory -Path $OutputDirectory,$CacheDirectory -Force | Out-Null
$lock = Get-Content -LiteralPath (Join-Path $repoRoot 'runtime-lock.json') -Raw | ConvertFrom-Json
function Test-PinnedFile($Path,$Sha256) {
    return (Test-Path -LiteralPath $Path -PathType Leaf) -and ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Sha256)
}
function Copy-LocalCppRuntime($Destination) {
    foreach ($name in @('msvcp140.dll','vcruntime140.dll','vcruntime140_1.dll')) {
        $source = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::System)) $name
        if (!(Test-Path -LiteralPath $source)) { throw 'Build computer needs Microsoft Visual C++ v14 x64 runtime: https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist' }
        $signature = Get-AuthenticodeSignature -LiteralPath $source
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') { throw ('Cannot verify Microsoft runtime: ' + $name) }
        Copy-Item -LiteralPath $source -Destination (Join-Path $Destination $name) -Force
    }
}
foreach ($asset in $lock.assets | Where-Object { $Components -contains $_.id }) {
    $destination = Join-Path $OutputDirectory $asset.destination
    if ($asset.kind -eq 'file') {
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        $modelPath = Join-Path $destination $asset.file
        if (Test-PinnedFile $modelPath $asset.sha256) { Write-Output ($asset.id + ': pinned file verified'); continue }
    }
    $archive = Join-Path $CacheDirectory $asset.file
    if (!(Test-PinnedFile $archive $asset.sha256)) {
        $partial = $archive + '.partial'
        Write-Output ('Downloading ' + $asset.id + ' ' + $asset.version)
        try {
            $ProgressPreference = 'SilentlyContinue'
            Invoke-WebRequest -Uri $asset.url -OutFile $partial -UseBasicParsing
            if (!(Test-PinnedFile $partial $asset.sha256)) { throw ('SHA256 mismatch: ' + $asset.id) }
            Move-Item -LiteralPath $partial -Destination $archive -Force
        } finally { if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial } }
    }
    if ($asset.kind -eq 'file') { Copy-Item -LiteralPath $archive -Destination $modelPath -Force; continue }
    $tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $unpack = Join-Path $tempBase ('SUB-runtime-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $unpack | Out-Null
    try {
        if ($asset.kind -eq 'zip') {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $zip = [IO.Compression.ZipFile]::OpenRead($archive)
            try {
                foreach ($entry in $zip.Entries) {
                    $entryPath = [IO.Path]::GetFullPath((Join-Path $unpack $entry.FullName))
                    if (!$entryPath.StartsWith($unpack + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe archive path' }
                }
            } finally { $zip.Dispose() }
            Expand-Archive -LiteralPath $archive -DestinationPath $unpack
        } else {
            $entries = @(& tar -tf $archive)
            if ($LASTEXITCODE -ne 0) { throw 'Cannot list runtime archive' }
            foreach ($entry in $entries) {
                if ($entry -match '(^[/\\]|(^|[/\\])\.\.([/\\]|$)|:)') { throw 'Unsafe archive path' }
            }
            & tar -xzf $archive -C $unpack
            if ($LASTEXITCODE -ne 0) { throw 'Cannot extract runtime archive' }
        }
        $markerName = [IO.Path]::GetFileName($asset.marker)
        $matches = @(Get-ChildItem -LiteralPath $unpack -Recurse -File | Where-Object Name -eq $markerName)
        if ($matches.Count -ne 1) { throw ('Runtime marker missing or ambiguous: ' + $markerName) }
        $payloadRoot = Split-Path -Parent $matches[0].FullName
        if ($asset.id -in @('traditional','online')) { $payloadRoot = Split-Path -Parent $payloadRoot }
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        foreach ($item in Get-ChildItem -LiteralPath $payloadRoot) { Copy-Item -LiteralPath $item.FullName -Destination $destination -Recurse -Force }
        if (!(Test-Path -LiteralPath (Join-Path $destination $asset.marker))) { throw ('Incomplete installed component: ' + $asset.id) }
        if ($asset.id -in @('cpu','vulkan')) { Copy-LocalCppRuntime $destination }
        Write-Output ($asset.id + ': archive SHA256 verified and component installed')
    } finally {
        $resolved = [IO.Path]::GetFullPath($unpack)
        if (!$resolved.StartsWith($tempBase,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^SUB-runtime-[a-f0-9]{32}$') { throw 'Invalid temporary cleanup path' }
        if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -ErrorAction Stop }
    }
}
