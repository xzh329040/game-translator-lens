param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDirectory
Set-Location $repoRoot

[xml]$project = Get-Content -LiteralPath "Game-Translator-Lens.csproj" -Encoding UTF8
$version = $project.Project.PropertyGroup.Version | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Unable to read Version from Game-Translator-Lens.csproj."
}

$dotnet = Join-Path (Split-Path -Parent $repoRoot) ".dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet)) {
    $dotnet = "dotnet"
}

$distDirectory = Join-Path $repoRoot "dist"
$packageRoot = Join-Path $distDirectory "GameTranslatorLens"
$zipPath = Join-Path $distDirectory "GameTranslatorLens-v$version-portable-win-x64.zip"
$sha256Path = "$zipPath.sha256.txt"

if (Test-Path -LiteralPath $packageRoot) {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null

& $dotnet publish "Game-Translator-Lens.csproj" -c $Configuration -o (Join-Path $packageRoot "app")
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$cscCandidates = @(
    (Join-Path -Path $env:WINDIR -ChildPath "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path -Path $env:WINDIR -ChildPath "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)
$csc = $cscCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($csc)) {
    throw "Unable to find .NET Framework csc.exe for the outer launcher."
}

$launcherPath = Join-Path $packageRoot "GameTranslatorLens.exe"
$iconPath = Join-Path $repoRoot "Resources\UI\game-translator-lens-icon.ico"
$launcherSource = Join-Path $repoRoot "Launcher\Program.cs"
$launcherArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/win32icon:$iconPath",
    "/reference:System.Windows.Forms.dll",
    "/out:$launcherPath",
    $launcherSource
)
& $csc $launcherArgs
if ($LASTEXITCODE -ne 0) {
    throw "Launcher build failed."
}

$updaterPath = Join-Path $packageRoot "GameTranslatorLensUpdater.exe"
$updaterSource = Join-Path $repoRoot "Updater\Program.cs"
$updaterArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/win32icon:$iconPath",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    "/out:$updaterPath",
    $updaterSource
)
& $csc $updaterArgs
if ($LASTEXITCODE -ne 0) {
    throw "Updater build failed."
}

$uninstallerPath = Join-Path $packageRoot "GameTranslatorLensUninstall.exe"
$uninstallerSource = Join-Path $repoRoot "Uninstaller\Program.cs"
$uninstallerArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:x64",
    "/optimize+",
    "/win32icon:$iconPath",
    "/reference:System.Windows.Forms.dll",
    "/out:$uninstallerPath",
    $uninstallerSource
)
& $csc $uninstallerArgs
if ($LASTEXITCODE -ne 0) {
    throw "Uninstaller build failed."
}

# ============================================================
# Post-build: Replace icons with full multi-resolution .ico
# Both csc.exe and dotnet publish only embed 1 icon size.
# ReplaceIcon.exe injects all 6 sizes (16–256 px) from the
# source .ico into each EXE via Win32 UpdateResource API.
# ============================================================
$replaceIconProject = Join-Path $scriptDirectory "ReplaceIcon"
$replaceIconExe = Join-Path $replaceIconProject "bin\Release\net9.0\win-x64\ReplaceIcon.exe"
Write-Host "Building ReplaceIcon tool..."
& $dotnet build $replaceIconProject -c Release
if ($LASTEXITCODE -ne 0) {
    throw "ReplaceIcon build failed."
}

$iconPath = Join-Path $repoRoot "Resources\UI\game-translator-lens-icon.ico"
$exeList = @(
    (Join-Path $packageRoot "GameTranslatorLens.exe"),
    (Join-Path $packageRoot "GameTranslatorLensUpdater.exe"),
    (Join-Path $packageRoot "GameTranslatorLensUninstall.exe"),
    (Join-Path $packageRoot "app\GameTranslatorLens.exe")
)

Write-Host "Injecting full-resolution icons..."
foreach ($targetExe in $exeList) {
    if (-not (Test-Path -LiteralPath $targetExe)) {
        Write-Host "  SKIP (not found): $targetExe"
        continue
    }
    Write-Host "  Processing: $(Split-Path -Leaf $targetExe)"
    & $replaceIconExe $targetExe $iconPath
    if ($LASTEXITCODE -ne 0) {
        throw "ReplaceIcon failed for: $targetExe"
    }
    # Remove backup files created by ReplaceIcon
    $backupFile = "$targetExe.backup"
    if (Test-Path -LiteralPath $backupFile) {
        Remove-Item -LiteralPath $backupFile -Force
    }
}
Write-Host "Icon injection complete."

$readmeSource = Join-Path $repoRoot "Docs\Release-v$version.md"
if (-not (Test-Path -LiteralPath $readmeSource)) {
    $readmeSource = Join-Path $repoRoot "README.md"
}
Copy-Item -LiteralPath $readmeSource -Destination (Join-Path $packageRoot "README.md") -Force

$releaseNotesDirectory = Join-Path $packageRoot "app\Resources\ReleaseNotes"
New-Item -ItemType Directory -Force -Path $releaseNotesDirectory | Out-Null
Copy-Item -LiteralPath $readmeSource -Destination (Join-Path $releaseNotesDirectory "current.md") -Force

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
if (Test-Path -LiteralPath $sha256Path) {
    Remove-Item -LiteralPath $sha256Path -Force
}
Compress-Archive -Path $packageRoot -DestinationPath $zipPath -Force

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
$shaLine = "$($hash.Hash.ToLowerInvariant())  $(Split-Path -Leaf $zipPath)"
[System.IO.File]::WriteAllText($sha256Path, $shaLine + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))

Write-Host "Package folder: $packageRoot"
Write-Host "Package zip:    $zipPath"
Write-Host "SHA256 file:    $sha256Path"
