$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$catalog = Join-Path $root "catalog.json"
if (-not (Test-Path $catalog)) {
    throw "catalog.json not found at $catalog. It is the tweak list the exe embeds."
}

Get-Content $catalog -Raw | ConvertFrom-Json | Out-Null

$publishDir = Join-Path $root "publish"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish (Join-Path $root "src\Tweaker\Tweaker.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

Write-Host ""
Write-Host "Build complete: $(Join-Path $publishDir 'tweaker.exe')" -ForegroundColor Green
