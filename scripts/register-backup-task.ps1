<#
.SYNOPSIS
    Yedekleme görevlerini Windows Görev Zamanlayıcı'ya kaydeder.

.DESCRIPTION
    İKİ görev kaydedilir; ikisi de zorunludur ve ayrı ayrı seçilemez:

      1. GÜNLÜK TAM YEDEK + TATBİKAT (backup-restore-drill.ps1)
      2. SAATLİK İŞLEM GÜNLÜĞÜ YEDEĞİ (backup-log.ps1)

    İkincisi neden isteğe bağlı değil: RPO taahhüdümüz 1 saat ve bunu SADECE
    saatlik günlük yedeği sağlar. Yalnız günlük tam yedek alınırsa gerçek RPO
    24 saate çıkar — üstelik bu, kimse fark etmeden olur. "Yedeğim var" diyen
    ama bir günlük veriyi kaybeden kurulum, tam olarak bu ayarın atlanmasıyla
    oluşur; bu yüzden burada seçenek sunulmuyor.

    Elle çalıştırılan yedek, er ya da geç çalıştırılmayan yedektir.

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
    Günlük tam yedeğin çalışma saati. Varsayılan 02:00 (düşük trafik).
    Saatlik günlük yedeği bundan bağımsız, her saat başı çalışır.

.PARAMETER TaskName
    Günlük görevin adı. Varsayılan: Ekipzen-YedekTatbikati
    Saatlik görev "<TaskName>-Log" adıyla kaydedilir.

.PARAMETER TaskUser
    Görevin çalışacağı Windows hesabı. Varsayılan SYSTEM (şifre saklamaz).
    Hangisi seçilirse seçilsin, o hesabın SQL Server'da SYSADMIN olması gerekir:
    yedek script'i tüm kiracı veritabanlarını okur, tatbikat için veritabanı
    oluşturup düşürür ve HENÜZ VAR OLMAYAN kiracıları da kapsamak zorundadır.

.EXAMPLE
    # Önce ne yapacağını gör:
    powershell -File scripts\register-backup-task.ps1 -BackupPath "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\Ekipzen" -WhatIf

.EXAMPLE
    # Yönetici PowerShell'de kaydet:
    powershell -File scripts\register-backup-task.ps1 -BackupPath "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\Ekipzen" `
        -OffsitePath "\\yedek-sunucu\ekipzen" -LogPath "C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\Ekipzen\tatbikat.jsonl"

.EXAMPLE
    # Kaldırmak için:
    Unregister-ScheduledTask -TaskName Ekipzen-YedekTatbikati -Confirm:$false
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
    [string]$TaskName = "Ekipzen-YedekTatbikati",

    # Görevin çalışacağı Windows hesabı. Varsayılan SYSTEM (şifre saklamayı
    # gerektirmez). HANGİSİ SEÇİLİRSE SEÇİLSİN, o hesabın SQL Server'da sysadmin
    # yetkisi olmalıdır — bkz. yedekleme-ve-kurtarma.md, "beşinci tuzak".
    [string]$TaskUser = "SYSTEM"
)

$ErrorActionPreference = "Stop"

# Yönetici kontrolü ÖNCE: görev SYSTEM hesabıyla kaydedilecek ve bu yükseltilmiş
# hak ister. Kontrol olmasaydı hata, Register-ScheduledTask'tan gelen anlaşılmaz
# bir COM hatası olarak çıkardı ("Erişim engellendi" / HRESULT 0x80070005) ve
# operatör sebebini aramakla vakit kaybederdi.
# -WhatIf modunda kontrol atlanır: deneme modu sistemi değiştirmiyor.
if (-not $WhatIfPreference) {
    $kimlik = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    if (-not $kimlik.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Error ("Bu script YÖNETİCİ olarak çalıştırılmalıdır — görev SYSTEM hesabıyla " +
            "kaydediliyor. PowerShell'i 'Yönetici olarak çalıştır' ile açıp tekrar deneyin. " +
            "(Ne yapacağını görmek için -WhatIf yönetici olmadan da çalışır.)")
        exit 1
    }
}

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

# Saatlik günlük yedeği: aynı yollar, aynı log dosyası, aynı uyarı hedefi.
$logScript = Join-Path $PSScriptRoot "backup-log.ps1"
if (-not (Test-Path $logScript)) {
    Write-Error "Günlük yedeği script'i bulunamadı: $logScript"
    exit 1
}
$logArgList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$logScript`"",
    "-BackupPath", "`"$BackupPath`"")
if ($Database) { $logArgList += @("-Database", "`"$Database`"") }
if ($LogPath) { $logArgList += @("-LogPath", "`"$LogPath`"") }
if ($AlertWebhookUrl) { $logArgList += @("-AlertWebhookUrl", "`"$AlertWebhookUrl`"") }
$logArguments = $logArgList -join " "
$logTaskName = "$TaskName-Log"

Write-Host "Görev 1 (günlük tam yedek + tatbikat)"
Write-Host "  ad       : $TaskName"
Write-Host "  çalışma  : her gün $At"
Write-Host "  komut    : powershell.exe $arguments"
Write-Host ""
Write-Host "Görev 2 (saatlik işlem günlüğü — RPO 1 saat bunun üzerinde durur)"
Write-Host "  ad       : $logTaskName"
Write-Host "  çalışma  : her saat başı"
Write-Host "  komut    : powershell.exe $logArguments"
Write-Host ""

# Görev hesabı. SYSTEM varsayılandır çünkü şifre saklamayı gerektirmez; ayrı bir
# hizmet hesabı verilirse Görev Zamanlayıcı şifresini soracaktır.
#
# DİKKAT: bu hesabın SQL Server'da SYSADMIN olması gerekir. Yedek script'i tüm
# kiracı veritabanlarını okur, geri yükleme tatbikatı için veritabanı oluşturup
# düşürür ve HENÜZ VAR OLMAYAN (yeni provizyonlanacak) kiracıları da kapsamak
# zorundadır. Veritabanı-başına yetki vermek bu yüzden çalışmaz: her yeni müşteri
# yedeklemeyi sessizce kırardı.
$principal = if ($TaskUser -eq "SYSTEM") {
    New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
}
else {
    New-ScheduledTaskPrincipal -UserId $TaskUser -LogonType Password -RunLevel Highest
}

if ($PSCmdlet.ShouldProcess($TaskName, "Günlük tam yedek görevini kaydet")) {
    $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $arguments
    $trigger = New-ScheduledTaskTrigger -Daily -At $At
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable `
        -DontStopOnIdleEnd -ExecutionTimeLimit (New-TimeSpan -Hours 4)

    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
        -Principal $principal -Settings $settings -Force | Out-Null

    Write-Host "Günlük görev kaydedildi." -ForegroundColor Green
}

if ($PSCmdlet.ShouldProcess($logTaskName, "Saatlik günlük yedeği görevini kaydet")) {
    $logAction = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $logArguments
    # Bugünün başından itibaren saat başı, süresiz. Süre sınırı 1 saatten KISA
    # tutulur: bir koşum takılırsa bir sonrakiyle üst üste binmemeli.
    $logTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date).Date `
        -RepetitionInterval (New-TimeSpan -Hours 1)
    $logSettings = New-ScheduledTaskSettingsSet -StartWhenAvailable `
        -DontStopOnIdleEnd -ExecutionTimeLimit (New-TimeSpan -Minutes 45) `
        -MultipleInstances IgnoreNew

    Register-ScheduledTask -TaskName $logTaskName -Action $logAction -Trigger $logTrigger `
        -Principal $principal -Settings $logSettings -Force | Out-Null

    Write-Host "Saatlik görev kaydedildi." -ForegroundColor Green
}

if (-not $WhatIfPreference) {
    Write-Host ""
    Write-Host "SON ADIM — bu yapılmadan görevler ÇALIŞIR AMA BAŞARISIZ OLUR:" -ForegroundColor Yellow
    $sqlHesap = if ($TaskUser -eq "SYSTEM") { "NT AUTHORITY\SYSTEM" } else { $TaskUser }
    Write-Host "  Görev hesabının SQL Server'da sysadmin olması gerekir. Kontrol/verme:" -ForegroundColor Yellow
    Write-Host "    sqlcmd -S localhost -E -Q `"ALTER SERVER ROLE sysadmin ADD MEMBER [$sqlHesap];`"" -ForegroundColor Yellow
    Write-Host "  Gerekçe: yedek script'i TÜM kiracı veritabanlarını okur ve henüz var" -ForegroundColor Yellow
    Write-Host "  olmayanları da kapsamak zorundadır; veritabanı-başına yetki her yeni" -ForegroundColor Yellow
    Write-Host "  müşteride sessizce kırılırdı." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Kaldırmak için:" -ForegroundColor Yellow
    Write-Host "  Unregister-ScheduledTask -TaskName $TaskName -Confirm:`$false"
    Write-Host "  Unregister-ScheduledTask -TaskName $logTaskName -Confirm:`$false"
}
else {
    Write-Host "(Deneme modu - görevler kaydedilmedi.)"
}
