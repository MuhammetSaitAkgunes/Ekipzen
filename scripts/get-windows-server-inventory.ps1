#Requires -Version 5.1
<#
.SYNOPSIS
Pilot sunucusunda salt okunur Windows/IIS/.NET/SQL sürüm envanteri üretir.
Şifre, bağlantı dizesi veya personel verisi toplamaz; hiçbir özellik kurmaz.
#>
$ErrorActionPreference = 'Stop'
$os = Get-CimInstance Win32_OperatingSystem
$iis = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\InetStp' -ErrorAction SilentlyContinue
$sqlInstances = @()
$instanceRoot = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL'
if (Test-Path -LiteralPath $instanceRoot) {
    $instances = Get-ItemProperty -LiteralPath $instanceRoot
    foreach ($property in $instances.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' }) {
        $setup = Get-ItemProperty -LiteralPath "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$($property.Value)\Setup" -ErrorAction SilentlyContinue
        $sqlInstances += [pscustomobject]@{ Instance = $property.Name; Version = $setup.Version; Edition = $setup.Edition }
    }
}
$runtimes = @()
if (Get-Command dotnet -ErrorAction SilentlyContinue) { $runtimes = @(dotnet --list-runtimes) }
[pscustomobject]@{
    Windows = $os.Caption
    Version = $os.Version
    Architecture = $os.OSArchitecture
    IisVersion = $iis.VersionString
    AspNetCoreModuleInstalled = (Test-Path -LiteralPath "$env:ProgramFiles\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll")
    DotNetRuntimes = $runtimes
    SqlInstances = $sqlInstances
    Note = 'Yalnız sürüm envanteridir; TLS, lisans hakkı, ACL, SMTP, SQL bağlantısı ve geri yükleme uygunluğunu kanıtlamaz.'
} | ConvertTo-Json -Depth 4
