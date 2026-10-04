[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
& git -C $repoRoot rev-parse --is-inside-work-tree | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'A valid Git repository is required.' }
$paths = @(& git -C $repoRoot -c core.quotepath=false ls-files)
if ($LASTEXITCODE -ne 0 -or !$paths.Count) { throw 'No tracked source files.' }
$issues = [Collections.Generic.List[string]]::new()
foreach ($relative in $paths) {
    if ($relative -match '(^|/)(dist|data|\.downloads|\.local|\.codex)(/|$)|\.(srt|gguf|exe|dll|zip|tar\.gz|pem|key)$|(^|/)(auth|credentials)\.json$|(^|/)\.env($|\.)') {
        $issues.Add('Forbidden public file: ' + $relative); continue
    }
    $path = Join-Path $repoRoot $relative
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { $issues.Add('Missing file: ' + $relative); continue }
    if ((Get-Item -LiteralPath $path).Length -gt 5MB) { $issues.Add('Unexpected large file: ' + $relative); continue }
    $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($path))
    if ($text -match 'C:[/\\]Users[/\\][^/\\\r\n]+' -or $text -match '(ghp_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{40,}|sk-[A-Za-z0-9_-]{32,}|-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----)') {
        $issues.Add('Potential private path or credential: ' + $relative)
    }
}
if ($issues.Count) { $issues | Write-Output; throw 'Public source inventory verification failed.' }
Write-Output ('Public source inventory verified: ' + $paths.Count + ' tracked files; runtime, models, subtitles and credentials excluded.')
