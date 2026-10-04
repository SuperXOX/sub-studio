param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'dist\SUB Studio' }
$root = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path (Join-Path $root 'assets') -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repoRoot 'assets') -File | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $root 'assets') -Force }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$buildRoot = Join-Path $tempRoot ('SUB-build-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildRoot | Out-Null
$shared = @("$PSScriptRoot\Srt.cs", "$PSScriptRoot\Languages.cs", "$PSScriptRoot\Translator.cs", "$PSScriptRoot\Jobs.cs", "$PSScriptRoot\OnlineTranslator.cs", "$PSScriptRoot\TraditionalJob.cs")
function Invoke-CheckedCompile([string[]]$Arguments) {
    & $compiler /nologo /warnaserror /platform:x64 @Arguments
    if ($LASTEXITCODE -ne 0) { throw '编译失败，程序未更新。' }
}
try {
    foreach ($suite in @('Tests', 'CoreTests', 'LanguageTests', 'WorkflowTests', 'TraditionalTests')) {
        $testExe = Join-Path $buildRoot ($suite + '.exe')
        $sources = if ($suite -in @('CoreTests', 'LanguageTests')) { @("$PSScriptRoot\Srt.cs", "$PSScriptRoot\Languages.cs") } else { $shared }
        Invoke-CheckedCompile (@('/r:System.Core.dll', '/r:System.Web.Extensions.dll', "/out:$testExe") + $sources + "$PSScriptRoot\$suite.cs")
        if ($suite -eq 'TraditionalTests') { & $testExe $root } else { & $testExe }
        if ($LASTEXITCODE -ne 0) { throw "验证失败：$suite，程序未更新。" }
    }
    if (!(Test-Path -LiteralPath "$root\assets\sub.ico") -or !(Test-Path -LiteralPath "$root\assets\sub.png")) {
        $iconExe = Join-Path $buildRoot 'icon-builder.exe'
        Invoke-CheckedCompile @('/r:System.Drawing.dll', "/out:$iconExe", "$PSScriptRoot\制作图标.cs")
        & $iconExe "$root\assets"
        if ($LASTEXITCODE -ne 0) { throw '图标生成失败。' }
    }
    $wpf = Join-Path (Split-Path -Parent $compiler) 'WPF'
    $newExe = Join-Path $buildRoot '字幕翻译 Studio.exe'
    Invoke-CheckedCompile (@('/target:winexe', '/optimize+', '/r:System.Core.dll', '/r:System.Web.Extensions.dll', '/r:System.Xaml.dll', "/r:$wpf\WindowsBase.dll", "/r:$wpf\PresentationCore.dll", "/r:$wpf\PresentationFramework.dll", "/win32icon:$root\assets\sub.ico", "/resource:$PSScriptRoot\Ui.xaml,SubtitleStudio.Ui.xaml", "/resource:$root\assets\sub.png,SubtitleStudio.Sub.png", "/out:$newExe") + $shared + @("$PSScriptRoot\Ui.cs", "$PSScriptRoot\Program.cs"))
    Copy-Item -LiteralPath $newExe -Destination "$root\字幕翻译 Studio.exe" -Force
    Write-Output '测试全部通过，SUB Studio 已更新。发布前请刷新 manifest-sha256.json。'
}
finally {
    $resolved = [IO.Path]::GetFullPath($buildRoot)
    if (!$resolved.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^SUB-build-[a-f0-9]{32}$') { throw '临时目录范围校验失败。' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -ErrorAction Stop }
}

