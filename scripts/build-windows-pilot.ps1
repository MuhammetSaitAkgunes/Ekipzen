#Requires -Version 7.0
<#
.SYNOPSIS
Yeni bir artifacts klasörüne Windows/IIS pilot paketi üretir; yayın, migration veya servis değişikliği yapmaz.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]{0,79}$')][string]$ReleaseName,
    [switch]$AllowDirty,
    [string]$SourceRef
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = Join-Path $repoRoot 'artifacts'
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $artifactRoot $ReleaseName))
if (-not $releaseRoot.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Yayın hedefi artifacts dışına çıkamaz.'
}
if (Test-Path -LiteralPath $releaseRoot) { throw 'Sürüm klasörü zaten var. Yeni ReleaseName kullanın; mevcut paket ezilmez.' }
foreach ($command in @('git', 'node', 'npm', 'dotnet')) { Get-Command $command -ErrorAction Stop | Out-Null }
$nodeVersion = (& node --version).Trim()
if ($LASTEXITCODE -ne 0 -or $nodeVersion -notmatch '^v24\.') { throw 'Paket üretimi Node.js 24 LTS gerektirir.' }
Push-Location $repoRoot
try {
    if ($SourceRef -and $AllowDirty) { throw 'SourceRef ile AllowDirty birlikte kullanılamaz: kaynak yalnız Git commitinden alınır.' }
    $revision = if ($SourceRef) { (& git rev-parse --verify --end-of-options "$SourceRef^{commit}").Trim() } else { (& git rev-parse HEAD).Trim() }
    if ($LASTEXITCODE -ne 0) { throw 'Git commit kimliği okunamadı.' }
    $workspaceDirty = [bool](& git status --porcelain)
    $dirty = $workspaceDirty -and -not $SourceRef
    if ($dirty -and -not $AllowDirty) { throw 'Çalışma ağacı temiz olmalı. Yerel prova için açıkça -AllowDirty kullanın.' }
    $null = New-Item -ItemType Directory -Path $releaseRoot
    $buildRoot = $repoRoot
    if ($SourceRef) {
        # Arşiv yalnız bu commitin takip edilen dosyalarını taşır; yerel değişiklik,
        # kullanıcı dosyası, node_modules veya çalışma zamanı verisi derlenmez.
        $exportRoot = Join-Path $artifactRoot "$ReleaseName-source"
        if (Test-Path -LiteralPath $exportRoot) { throw 'Kaynak arşiv klasörü zaten var; mevcut arşiv ezilmez.' }
        $null = New-Item -ItemType Directory -Path $exportRoot
        $archive = Join-Path $exportRoot 'source.zip'
        & git archive --format=zip "--output=$archive" $revision
        if ($LASTEXITCODE -ne 0) { throw 'Git kaynak arşivi üretilemedi.' }
        $buildRoot = Join-Path $exportRoot 'tree'
        Expand-Archive -LiteralPath $archive -DestinationPath $buildRoot
    }
    $siteRoot = Join-Path $releaseRoot 'site'
    Push-Location (Join-Path $buildRoot 'frontend')
    try {
        & npm ci --legacy-peer-deps
        if ($LASTEXITCODE -ne 0) { throw 'npm ci başarısız.' }
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend üretim build başarısız.' }
    } finally { Pop-Location }
    Push-Location (Join-Path $buildRoot 'backend')
    try {
        & dotnet publish src/Ekipzen.API/Ekipzen.API.csproj -c Release -r win-x64 --self-contained false -p:UseAppHost=false -o $siteRoot
        if ($LASTEXITCODE -ne 0) { throw 'Backend publish başarısız.' }
    } finally { Pop-Location }

    $wwwroot = Join-Path $siteRoot 'wwwroot'
    $null = New-Item -ItemType Directory -Path $wwwroot -Force
    foreach ($item in Get-ChildItem -LiteralPath (Join-Path $buildRoot 'frontend/dist')) {
        Copy-Item -LiteralPath $item.FullName -Destination $wwwroot -Recurse
    }
    if (Test-Path -LiteralPath (Join-Path $siteRoot 'appsettings.Development.json')) { throw 'Development yapılandırması publish paketine sızdı.' }
    if (Test-Path -LiteralPath (Join-Path $siteRoot 'App_Data')) { throw 'Çalışma zamanı verileri publish paketine sızdı.' }
    $baseConfig = Get-Content -LiteralPath (Join-Path $siteRoot 'appsettings.json') -Raw | ConvertFrom-Json
    if ($baseConfig.Jwt.Secret -or $baseConfig.ConnectionStrings.DefaultConnection) { throw 'Temel yayın yapılandırması örnek sır veya bağlantı içeriyor.' }
    $demoMarkers = Get-ChildItem -LiteralPath (Join-Path $wwwroot 'assets') -Filter '*.js' |
        Select-String -Pattern 'demo123|ik@hrmaster.local'
    if ($demoMarkers) { throw 'Üretim JavaScript paketinde demo giriş bilgisi bulundu.' }

    $webConfigPath = Join-Path $siteRoot 'web.config'
    [xml]$webConfig = Get-Content -LiteralPath $webConfigPath -Raw
    $aspNet = $webConfig.SelectSingleNode('//aspNetCore')
    if ($null -eq $aspNet) { throw 'SDK web.config üretmedi.' }
    $aspNet.SetAttribute('hostingModel', 'inprocess')
    $aspNet.SetAttribute('stdoutLogEnabled', 'false')
    $environment = $aspNet.SelectSingleNode('environmentVariables')
    if ($null -eq $environment) {
        $environment = $webConfig.CreateElement('environmentVariables')
        $null = $aspNet.AppendChild($environment)
    }
    foreach ($key in @('ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT')) {
        $entry = $environment.SelectSingleNode("environmentVariable[@name='$key']")
        if ($null -eq $entry) {
            $entry = $webConfig.CreateElement('environmentVariable')
            $entry.SetAttribute('name', $key)
            $null = $environment.AppendChild($entry)
        }
        $entry.SetAttribute('value', 'Production')
    }
    $webConfig.Save($webConfigPath)
    Copy-Item -LiteralPath (Join-Path $buildRoot 'deployment/windows/configuration.example.json') -Destination $releaseRoot
    Copy-Item -LiteralPath (Join-Path $buildRoot 'docs/windows-pilot-yayin.md') -Destination $releaseRoot
    $files = @(Get-ChildItem -LiteralPath $siteRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{ path = [IO.Path]::GetRelativePath($siteRoot, $_.FullName); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
    [ordered]@{
        release = $ReleaseName; commit = $revision; dirtyWorkingTree = $dirty
        sourceExportedFromGit = [bool]$SourceRef; originalWorkspaceDirty = $workspaceDirty
        builtAtUtc = [DateTime]::UtcNow.ToString('o'); node = $nodeVersion; runtime = 'win-x64'; selfContained = $false
        packageLockSha256 = (Get-FileHash -LiteralPath (Join-Path $buildRoot 'frontend/package-lock.json') -Algorithm SHA256).Hash
        files = $files
        note = 'Paket üretimi, test/SQL/SMTP/IIS/TLS/pilot kabulünün yerine geçmez. Sunucuya yayın yapılmadı.'
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $releaseRoot 'manifest.json') -Encoding utf8
    Write-Output "Pilot paketi: $releaseRoot"
    if ($dirty) { Write-Warning 'Bu paket yerel prova içindir; commitlenmemiş kaynak içerir.' }
} finally { Pop-Location }
