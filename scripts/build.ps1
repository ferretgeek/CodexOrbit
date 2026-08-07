[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Find-MSBuild {
    $command = Get-Command 'MSBuild.exe' -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $programFilesX86 = [Environment]::GetFolderPath(
        [Environment+SpecialFolder]::ProgramFilesX86
    )
    $vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $found = & $vswhere -latest -products * `
            -requires Microsoft.Component.MSBuild `
            -find 'MSBuild\**\Bin\MSBuild.exe' |
            Select-Object -First 1
        if ($found -and (Test-Path -LiteralPath $found)) {
            return $found
        }
    }

    $windows = [Environment]::GetFolderPath([Environment+SpecialFolder]::Windows)
    $framework = Join-Path $windows 'Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
    if (Test-Path -LiteralPath $framework) {
        return $framework
    }

    throw '未找到 MSBuild。请安装 Visual Studio 2022 或 .NET Framework 4.8 Developer Pack。'
}

function Invoke-ProjectBuild {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Project
    )

    & $script:msbuild $Project `
        /nologo `
        /m `
        /t:Rebuild `
        "/p:Configuration=$Configuration" `
        /p:Platform=x64 `
        /warnaserror:true

    if ($LASTEXITCODE -ne 0) {
        throw "构建失败：$Project"
    }
}

$msbuild = Find-MSBuild
$appProject = Join-Path $repoRoot 'src\CodexOrbit\CodexOrbit.csproj'
$testProject = Join-Path $repoRoot 'tests\UsageProbe\UsageProbe.csproj'

Write-Host "MSBuild: $msbuild"
Invoke-ProjectBuild -Project $appProject

if (-not $SkipTests) {
    Invoke-ProjectBuild -Project $testProject
}

$output = Join-Path $repoRoot "src\CodexOrbit\bin\x64\$Configuration\CodexOrbit.exe"
if (-not (Test-Path -LiteralPath $output)) {
    throw "构建完成但未找到产物：$output"
}

$info = Get-Item -LiteralPath $output
$hash = Get-FileHash -LiteralPath $output -Algorithm SHA256
Write-Host "完成：$($info.FullName)"
Write-Host "版本：$($info.VersionInfo.FileVersion)"
Write-Host "SHA-256：$($hash.Hash)"
