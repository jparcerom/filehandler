param(
    [switch]$PortableOnly
)

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src\FileHandler\FileHandler.csproj"
$projectXml = [xml](Get-Content -LiteralPath $projectPath -Raw)
$version = [string]($projectXml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "No se encontró la versión de la aplicación en $projectPath."
}

$dotnetCandidates = @(
    (Join-Path $env:LOCALAPPDATA "dotnet\dotnet.exe"),
    (Get-Command dotnet.exe -ErrorAction SilentlyContinue).Source
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique
$dotnet = $null
foreach ($candidate in $dotnetCandidates) {
    if (-not (Test-Path -LiteralPath $candidate)) {
        continue
    }

    $installedSdks = & $candidate --list-sdks 2>$null
    if ($LASTEXITCODE -eq 0 -and $installedSdks -match '(^|\r?\n)\s*8\.') {
        $dotnet = $candidate
        break
    }
}
if ([string]::IsNullOrWhiteSpace($dotnet)) {
    throw "No se encontró el SDK de .NET 8. Instala el SDK antes de publicar."
}

$runtime = "win-x64"
$artifactDirectory = Join-Path $repositoryRoot "artifacts"
$publishDirectory = Join-Path $artifactDirectory "publish\$runtime"
$portableArchive = Join-Path $artifactDirectory "FileHandler-$version-$runtime-portable.zip"

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

& $dotnet publish $projectPath `
    --configuration Release `
    --runtime $runtime `
    --self-contained true `
    --output $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Falló la publicación autocontenida para $runtime."
}

$applicationPath = Join-Path $publishDirectory "FileHandler.exe"
if (-not (Test-Path -LiteralPath $applicationPath)) {
    throw "La publicación no generó el ejecutable esperado: $applicationPath"
}

if (Test-Path -LiteralPath $portableArchive) {
    Remove-Item -LiteralPath $portableArchive -Force
}
Compress-Archive -Path (Join-Path $publishDirectory "*") `
    -DestinationPath $portableArchive `
    -CompressionLevel Optimal
Write-Output "Portable autocontenido: $portableArchive"

if ($PortableOnly) {
    return
}

$compiler = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
if ([string]::IsNullOrWhiteSpace($compiler)) {
    $compilerCandidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    )
    $compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($compiler)) {
    throw "No se encontró ISCC.exe. Instala Inno Setup 6 y vuelve a ejecutar este script."
}

$installerScript = Join-Path $PSScriptRoot "FileHandler.iss"
& $compiler "/DAppVersion=$version" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Falló la compilación del instalador de Inno Setup."
}
