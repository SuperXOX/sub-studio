[CmdletBinding()]
param([switch]$TraditionalOnly,[string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'dist\SUB Studio' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$components = if ($TraditionalOnly) { @('traditional') } else { @('traditional','cpu','vulkan','online','model') }
& (Join-Path $PSScriptRoot 'install-runtime.ps1') -Components $components -OutputDirectory $OutputDirectory
& (Join-Path $repoRoot '源码\编译.ps1') -OutputDirectory $OutputDirectory
foreach ($folder in @('assets','licenses')) {
    New-Item -ItemType Directory -Path (Join-Path $OutputDirectory $folder) -Force | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repoRoot $folder) -File) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $OutputDirectory $folder) -Force }
}
foreach ($file in @('LICENSE','components.json')) { Copy-Item -LiteralPath (Join-Path $repoRoot $file) -Destination (Join-Path $OutputDirectory $file) -Force }
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\使用说明.txt') -Destination (Join-Path $OutputDirectory '使用说明.txt') -Force
$map = [ordered]@{}
Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File | Where-Object { $_.Name -ne 'manifest-sha256.json' -and !$_.FullName.StartsWith((Join-Path $OutputDirectory 'data') + '\',[StringComparison]::OrdinalIgnoreCase) } | Sort-Object FullName | ForEach-Object {
    $map[$_.FullName.Substring($OutputDirectory.Length + 1).Replace('\','/')] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
}
$map | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'manifest-sha256.json') -Encoding UTF8
Write-Output ('Portable output: ' + $OutputDirectory)
