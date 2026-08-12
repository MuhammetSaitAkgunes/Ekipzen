<#
.SYNOPSIS
    Yedekleme tatbikatını Windows Görev Zamanlayıcı'ya günlük görev olarak kaydeder.

.DESCRIPTION
    backup-restore-drill.ps1'i her gün belirtilen saatte çalıştıran bir zamanlanmış
    görev oluşturur. Elle çalıştırılan yedek, er ya da geç çalıştırılmayan yedektir.

    YÖNETİCİ HAKKI GEREKİR: görev SYSTEM hesabıyla çalışacak şekilde kaydedilir,
    çünkü SQL Server yedek dizinine yazma izni kullanıcı hesabında genelde yoktur.

    Bu script sistemde KALICI yapılandırma oluşturur. Ne yapacağını önce
    -WhatIf ile görebilirsiniz.

.PARAMETER Database
    YALNIZ bu veritabanını yedekle. Boş bırakın: tatbikat o zaman platform +
    paylaşılan katalog + HER MÜŞTERİ veritabanını kapsar (kiracı listesi
    platformdan keşfedilir). Tek ad vermek diğer müşterileri yedeksiz bırakır.

.PARAMETER BackupPath
    Yedek klasörü. SQL Server servis hesabının yazabildiği bir yol olmalı:
    sqlcmd -S localhost -E -h -1 -W -Q "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(400));"

.PARAMETER OffsitePath
    İkinci (off-site) kopya hedefi. Ağ paylaşımı verilirse SYSTEM hesabının
    o paylaşıma erişebildiğinden emin olun.

.PARAMETER LogPath
    Sonuçların JSON satırı olarak yazılacağı dosya.

.PARAMETER StoragePath
    Yüklenen evrak dizini (App_Data/storage).

.PARAMETER AlertWebhookUrl
    Başarısızlıkta POST edilecek adres (Slack/Teams uyumlu {text} gövdesi).

.PARAMETER At
    Günlük çalışma saati. Varsayılan 02:00 (düşük trafik).

.PARAMETER TaskName
    Görev adı. Varsayılan: IKPro-YedekTatbikati

.EXAMPLE
    # Önce ne yapacağını gör:
    pwsh scripts/register-backup-task.ps1 -BackupPath "C:\SQLYedek" -WhatIf

.EXAMPLE
    # Yönetici PowerShell'de kaydet:
    pwsh scripts/register-backup-task.ps1 -BackupPath "C:\SQLYedek" `
        -OffsitePath "\\yedek-sunucu\ikpro" -LogPath "C:\SQLYedek\tatbikat.jsonl"

.EXAMPLE
    # Kaldırmak için:
    Unregister-ScheduledTask -TaskName IKPro-YedekTatbikati -Confirm:$false
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    # Boş bırakılırsa tatbikat TÜM veritabanlarını (platform + paylaşılan + her
    # müşteri kataloğu) kapsar. Normal kullanım budur; tek bir ad vermek yalnız
    # hedefli bir tatbikat içindir ve DİĞER MÜŞTERİLERİ YEDEKSİZ BIRAKIR.
    [string]$Database,
    [Parameter(Mandatory = $true)][string]$BackupPath,
    [string]$OffsitePath,
    [string]$LogPath,
    [string]$StoragePath,
    [string]$AlertWebhookUrl,
    [string]$At = "02:00",
    [string]$TaskName = "IKPro-YedekTatbikati"
)

$ErrorActionPreference = "Stop"

$drillScript = Join-Path $PSScriptRoot "backup-restore-drill.ps1"
if (-not (Test-Path $drillScript)) {
    Write-Error "Tatbikat script'i bulunamadı: $drillScript"
    exit 1
}

# Argümanlar tırnaklanır: yollar boşluk içerebilir ("C:\Program Files\...").
$argList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$drillScript`"",
    "-BackupPath", "`"$BackupPath`"")
if ($Database) { $argList += @("-Database", "`"$Database`"") }
if ($OffsitePath) { $argList += @("-OffsitePath", "`"$OffsitePath`"") }
if ($LogPath) { $argList += @("-LogPath", "`"$LogPath`"") }
if ($StoragePath) { $argList += @("-StoragePath", "`"$StoragePath`"") }
if ($AlertWebhookUrl) { $argList += @("-AlertWebhookUrl", "`"$AlertWebhookUrl`"") }

$arguments = $argList -join " "

Write-Host "Görev adı  : $TaskName"
Write-Host "Çalışma    : her gün $At"
Write-Host "Komut      : powershell.exe $arguments"
Write-Host ""

if ($PSCmdlet.ShouldProcess($TaskName, "Zamanlanmış görev kaydet")) {
    $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $arguments
    $trigger = New-ScheduledTaskTrigger -Daily -At $At
    # SYSTEM: SQL Server yedek dizinine yazma izni kullanıcıda genelde yok.
    $principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable `
        -DontStopOnIdleEnd -ExecutionTimeLimit (New-TimeSpan -Hours 4)

    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
        -Principal $principal -Settings $settings -Force | Out-Null

    Write-Host "Görev kaydedildi." -ForegroundColor Green
    Write-Host ""
    Write-Host "Doğrulama — görevi hemen elle tetikleyip sonucu görün:"
    Write-Host "  Start-ScheduledTask -TaskName $TaskName"
    Write-Host "  Get-ScheduledTaskInfo -TaskName $TaskName | Select LastRunTime, LastTaskResult"
    Write-Host ""
    Write-Host "LastTaskResult 0 = başarılı. Sıfırdan farklıysa log dosyasına bakın."
}
else {
    Write-Host "(Deneme modu — görev kaydedilmedi.)"
}
