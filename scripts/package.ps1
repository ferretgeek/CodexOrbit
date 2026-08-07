[CmdletBinding()]
param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not $NoBuild) {
    & (Join-Path $PSScriptRoot 'test.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw '测试失败，停止打包。'
    }
}

$exe = Join-Path $repoRoot 'src\CodexOrbit\bin\x64\Release\CodexOrbit.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw "未找到 Release 产物：$exe"
}

$fileVersion = [Version](Get-Item -LiteralPath $exe).VersionInfo.FileVersion
$version = '{0}.{1}.{2}' -f $fileVersion.Major, $fileVersion.Minor, $fileVersion.Build
$artifactRoot = Join-Path $repoRoot 'artifacts'
$staging = Join-Path $artifactRoot "CodexOrbit-$version-windows-x64"
$archive = "$staging.zip"
$releaseExe = Join-Path $artifactRoot "CodexOrbit-$version-windows-x64.exe"
$checksums = Join-Path $artifactRoot 'SHA256SUMS.txt'

function Assert-ArtifactPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $artifactFull = [System.IO.Path]::GetFullPath($artifactRoot)
    $artifactFull = $artifactFull.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar
    )
    $targetFull = [System.IO.Path]::GetFullPath($Path)
    $prefix = $artifactFull + [System.IO.Path]::DirectorySeparatorChar
    if (-not $targetFull.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝操作 artifacts 目录之外的路径：$targetFull"
    }
}

[System.IO.Directory]::CreateDirectory($artifactRoot) | Out-Null
Assert-ArtifactPath -Path $staging
Assert-ArtifactPath -Path $archive
Assert-ArtifactPath -Path $releaseExe
Assert-ArtifactPath -Path $checksums

if (Test-Path -LiteralPath $staging) {
    [System.IO.Directory]::Delete($staging, $true)
}
if (Test-Path -LiteralPath $archive) {
    [System.IO.File]::Delete($archive)
}
if (Test-Path -LiteralPath $releaseExe) {
    [System.IO.File]::Delete($releaseExe)
}
if (Test-Path -LiteralPath $checksums) {
    [System.IO.File]::Delete($checksums)
}

[System.IO.Directory]::CreateDirectory($staging) | Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $staging 'CodexOrbit.exe')
$releaseDocuments = @(
    'README.md',
    'README.en.md',
    'LICENSE',
    'LICENSE.zh-CN.md',
    'PRIVACY.md',
    'PRIVACY.en.md',
    'SECURITY.md',
    'SECURITY.en.md',
    'SUPPORT.md',
    'SUPPORT.en.md',
    'CONTRIBUTING.md',
    'CONTRIBUTING.en.md',
    'CODE_OF_CONDUCT.md',
    'CODE_OF_CONDUCT.en.md',
    'CHANGELOG.md',
    'CHANGELOG.en.md',
    'THIRD_PARTY_NOTICES.md',
    'THIRD_PARTY_NOTICES.en.md'
)
foreach ($document in $releaseDocuments) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $document) -Destination $staging
}

$releaseDirectories = @('assets', 'docs')
foreach ($directory in $releaseDirectories) {
    $sourceDirectory = Join-Path $repoRoot $directory
    $targetDirectory = Join-Path $staging $directory
    [System.IO.Directory]::CreateDirectory($targetDirectory) | Out-Null
    foreach ($item in Get-ChildItem -LiteralPath $sourceDirectory) {
        Copy-Item -LiteralPath $item.FullName -Destination $targetDirectory -Recurse
    }
}

$hash = Get-FileHash -LiteralPath (Join-Path $staging 'CodexOrbit.exe') -Algorithm SHA256
[System.IO.File]::WriteAllText(
    (Join-Path $staging 'SHA256.txt'),
    "$($hash.Hash)  CodexOrbit.exe`r`n",
    [System.Text.UTF8Encoding]::new($false)
)

Compress-Archive -LiteralPath $staging -DestinationPath $archive -CompressionLevel Optimal
Copy-Item -LiteralPath $exe -Destination $releaseExe

$releaseExeHash = Get-FileHash -LiteralPath $releaseExe -Algorithm SHA256
$archiveHash = Get-FileHash -LiteralPath $archive -Algorithm SHA256
$checksumText = @(
    "$($releaseExeHash.Hash)  $([System.IO.Path]::GetFileName($releaseExe))",
    "$($archiveHash.Hash)  $([System.IO.Path]::GetFileName($archive))"
) -join "`r`n"
[System.IO.File]::WriteAllText(
    $checksums,
    $checksumText + "`r`n",
    [System.Text.UTF8Encoding]::new($false)
)

Write-Host "独立程序：$releaseExe"
Write-Host "发布包：$archive"
Write-Host "校验文件：$checksums"
Write-Host "ZIP SHA-256：$($archiveHash.Hash)"
