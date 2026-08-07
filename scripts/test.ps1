[CmdletBinding()]
param(
    [switch]$Live,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $NoBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw '构建脚本执行失败。'
    }
}

$probe = Join-Path $repoRoot 'tests\UsageProbe\bin\UsageProbe.exe'
if (-not (Test-Path -LiteralPath $probe)) {
    throw "未找到测试程序：$probe"
}

& $probe --notification-test
if ($LASTEXITCODE -ne 0) {
    throw '实时通知确定性测试失败。'
}

if ($Live) {
    & $probe --once
    if ($LASTEXITCODE -ne 0) {
        throw '真实 Codex 账户测试失败。'
    }
}

Write-Host '全部请求的测试均已通过。'
