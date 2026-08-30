[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$GamePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$solution = Join-Path $root 'ClientSideRenamer.sln'
$project = Join-Path $root 'src\ClientSideRenamer\ClientSideRenamer.csproj'
$tests = Join-Path $root 'tests\ClientSideRenamer.Tests\ClientSideRenamer.Tests.csproj'
$pluginSource = Join-Path $root 'src\ClientSideRenamer\Plugin.cs'
$artifacts = Join-Path $root 'artifacts'
$staging = Join-Path $artifacts 'release-staging'
$nommStage = Join-Path $staging 'nomm'
$pluginStage = Join-Path $staging 'plugin-only'
$safeRoot = $root.Replace('\', '/')

$gitStatus = @(& git -c "safe.directory=$safeRoot" status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to read the Git working-tree status.'
}
if ($gitStatus.Count -ne 0) {
    throw 'The release must be built from a clean Git working tree.'
}

$sourceCommit = (& git -c "safe.directory=$safeRoot" rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
    throw 'Unable to resolve the source commit.'
}

[xml]$projectXml = Get-Content -LiteralPath $project
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Invalid project version '$version'."
}

$pluginVersionMatch = [regex]::Match(
    [IO.File]::ReadAllText($pluginSource),
    'const\s+string\s+PluginVersion\s*=\s*"(?<version>[^"]+)"')
if (-not $pluginVersionMatch.Success) {
    throw 'Plugin.PluginVersion was not found.'
}
if ($pluginVersionMatch.Groups['version'].Value -ne $version) {
    throw "Plugin version does not match project version '$version'."
}

$resolvedGamePath = [IO.Path]::GetFullPath($GamePath)
if (-not (Test-Path -LiteralPath (Join-Path $resolvedGamePath 'NuclearOption_Data\Managed\Assembly-CSharp.dll') -PathType Leaf)) {
    throw "Nuclear Option was not found at '$resolvedGamePath'."
}

function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Copy-PackageFiles {
    param(
        [Parameter(Mandatory)][string]$Destination,
        [Parameter(Mandatory)][string]$PluginDll
    )

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Copy-Item -LiteralPath $PluginDll -Destination (Join-Path $Destination 'ClientSideRenamer.dll')
    $packageReadme = [IO.File]::ReadAllText((Join-Path $root 'packaging\README.txt'))
    if ($packageReadme -notlike '*@VERSION@*') {
        throw 'The package README does not contain its version token.'
    }
    [IO.File]::WriteAllText(
        (Join-Path $Destination 'README.txt'),
        $packageReadme.Replace('@VERSION@', $version),
        [Text.UTF8Encoding]::new($false))
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $Destination 'LICENSE.txt')
}

function New-DeterministicZip {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Force
    }

    $archive = [IO.Compression.ZipFile]::Open($Destination, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $sourceRoot = [IO.Path]::GetFullPath($Source).TrimEnd('\', '/') + '\'
        $timestamp = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
        foreach ($file in Get-ChildItem -LiteralPath $Source -Recurse -File | Sort-Object FullName) {
            $entryName = $file.FullName.Substring($sourceRoot.Length).Replace('\', '/')
            $entry = $archive.CreateEntry($entryName, [IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $timestamp
            $input = [IO.File]::OpenRead($file.FullName)
            try {
                $output = $entry.Open()
                try {
                    $input.CopyTo($output)
                }
                finally {
                    $output.Dispose()
                }
            }
            finally {
                $input.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-Package {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string[]]$ExpectedEntries
    )

    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $actualEntries = @($archive.Entries | Where-Object Name | ForEach-Object FullName | Sort-Object)
        $readmeEntry = $archive.Entries | Where-Object { $_.Name -eq 'README.txt' } | Select-Object -First 1
        if (-not $readmeEntry) {
            throw "Package '$Path' does not contain README.txt."
        }
        $reader = [IO.StreamReader]::new($readmeEntry.Open(), [Text.Encoding]::UTF8, $true)
        try {
            if ($reader.ReadToEnd() -match '@VERSION@') {
                throw "Package '$Path' contains an unresolved version token."
            }
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }

    $difference = Compare-Object ($ExpectedEntries | Sort-Object) $actualEntries
    if ($difference) {
        $difference | Format-Table | Out-String | Write-Error
        throw "Package '$Path' has unexpected contents."
    }
}

Invoke-DotNet @('clean', $solution, '-c', 'Release', '--nologo', '--verbosity', 'minimal', "/p:GamePath=$resolvedGamePath")
Invoke-DotNet @('restore', $solution, '--nologo', '--verbosity', 'minimal', "/p:GamePath=$resolvedGamePath")
Invoke-DotNet @('build', $solution, '-c', 'Release', '--no-restore', '--nologo', '--verbosity', 'minimal', "/p:GamePath=$resolvedGamePath")
Invoke-DotNet @('test', $tests, '-c', 'Release', '--no-build', '--no-restore', '--nologo', '--verbosity', 'minimal', "/p:GamePath=$resolvedGamePath")

$pluginDll = Join-Path $root 'src\ClientSideRenamer\bin\Release\netstandard2.1\ClientSideRenamer.dll'
if (-not (Test-Path -LiteralPath $pluginDll -PathType Leaf)) {
    throw 'The Release build did not produce ClientSideRenamer.dll.'
}

$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($pluginDll).Version.ToString(3)
if ($assemblyVersion -ne $version) {
    throw "Assembly version '$assemblyVersion' does not match project version '$version'."
}
$productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($pluginDll).ProductVersion
if ($productVersion -notlike "*$sourceCommit*") {
    throw "Assembly product version '$productVersion' does not identify source commit '$sourceCommit'."
}

if (Test-Path -LiteralPath $staging) {
    Remove-Item -LiteralPath $staging -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $nommStage, $pluginStage, $artifacts | Out-Null

Copy-PackageFiles -Destination $nommStage -PluginDll $pluginDll
$pluginDirectory = Join-Path $pluginStage 'BepInEx\plugins\ClientSideRenamer'
Copy-PackageFiles -Destination $pluginDirectory -PluginDll $pluginDll

$nommZip = Join-Path $artifacts "ClientSideRenamer-v$version-nomm.zip"
$pluginZip = Join-Path $artifacts "ClientSideRenamer-v$version-plugin-only.zip"
$obsoleteZip = Join-Path $artifacts "ClientSideRenamer-v$version.zip"
if (Test-Path -LiteralPath $obsoleteZip) {
    Remove-Item -LiteralPath $obsoleteZip -Force
}
New-DeterministicZip -Source $nommStage -Destination $nommZip
New-DeterministicZip -Source $pluginStage -Destination $pluginZip

Assert-Package -Path $nommZip -ExpectedEntries @(
    'ClientSideRenamer.dll',
    'LICENSE.txt',
    'README.txt'
)
Assert-Package -Path $pluginZip -ExpectedEntries @(
    'BepInEx/plugins/ClientSideRenamer/ClientSideRenamer.dll',
    'BepInEx/plugins/ClientSideRenamer/LICENSE.txt',
    'BepInEx/plugins/ClientSideRenamer/README.txt'
)

$checksumPath = Join-Path $artifacts 'SHA256SUMS.txt'
$checksumLines = @(
    $nommZip, $pluginZip |
        Sort-Object |
        ForEach-Object { "$((Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash)  $(Split-Path -Leaf $_)" }
)
[IO.File]::WriteAllLines($checksumPath, [string[]]$checksumLines, [Text.Encoding]::ASCII)

Remove-Item -LiteralPath $staging -Recurse -Force

Write-Host "Release v$version from commit $sourceCommit passed build, test, and package validation."
Get-Item -LiteralPath $nommZip, $pluginZip, $checksumPath | ForEach-Object {
    [pscustomobject]@{
        Name = $_.Name
        Bytes = $_.Length
        SHA256 = if ($_.Extension -eq '.zip') { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } else { '' }
    }
} | Format-Table -AutoSize
