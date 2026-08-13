<#
.SYNOPSIS
    İK Pro veritabanlarının işlem günlüğü (LOG) yedeğini alır. RPO'yu GERÇEK yapan
    parça budur.

.DESCRIPTION
    Tam yedek tek başına RPO = 1 saat vaadini karşılamaz: iki tam yedek arasında
    ne olduysa kaybolur. "En fazla 1 saatlik veri kaybı" ancak SAATLİK işlem
    günlüğü yedeğiyle mümkündür ve `RESTORE ... STOPAT` de bu günlüklere dayanır.

    Yedeklenecek veritabanları tam yedekle AYNI kaynaktan (platform tablosu)
    keşfedilir — ortak mantık IKProBackup.psm1 içindedir. İki liste ayrışırsa
    sonuç sessiz olur: tam yedeği alınan ama günlüğü alınmayan müşteri, felaket
    anına kadar fark edilmez.

    ÜÇ ÖZEL DURUM ELE ALINIR:

    1. YENİ KİRACI. Provizyon her an yeni bir veritabanı yaratabilir ve o
       veritabanının henüz TAM YEDEĞİ YOKTUR. `BACKUP LOG` bu durumda hata verir
       ("no current database backup"). Script bunu tespit edip ÖNCE tam yedek
       alır. Aksi halde gün içinde açılan bir müşteri, ertesi güne kadar
       tamamen korumasız kalırdı.

    2. SIMPLE KURTARMA MODELİ. Log yedeği alınamaz. Bu, o veritabanı için RPO
       taahhüdünün geçersiz olması demektir — sessizce atlanmaz, BAŞARISIZLIK
       sayılır ve düzeltme komutu yazılır.

    3. ESKİ DOSYALARIN TEMİZLİĞİ. Saatlik yedek × 7 gün × N müşteri, hızla binlerce
       dosya eder. Saklama süresi dolan .trn dosyaları silinir; tam yedekler
       (.bak) BU SCRIPT TARAFINDAN SİLİNMEZ — onların ömrü ayrı bir karardır ve
       yanlışlıkla zincirin temelini silmek en pahalı hatadır.

.PARAMETER BackupPath
    Yedek klasörü. SQL Server servis hesabının yazma izni olmalıdır.

.PARAMETER ServerInstance
    SQL Server örneği. Varsayılan: localhost

.PARAMETER RetentionDays
    .trn dosyalarının saklama süresi (gün). Varsayılan 7 — yedek planındaki
    değerle aynı.

.PARAMETER Database
    YALNIZ bu veritabanının günlüğünü al (keşif yapılmaz).

.EXAMPLE
    pwsh scripts/backup-log.ps1 -BackupPath "C:\SQLYedek"

.OUTPUTS
    Çıkış kodu 0 = tüm veritabanlarının günlüğü alındı. 1 = en az biri
    başarısız. Kısmi başarı, başarı değildir.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$BackupPath,
    [string]$Database,
    [string]$PlatformDatabase = "IKProPlatform",
    [string]$SharedDatabase = "IKProDb",
    [string]$ServerInstance = "localhost",
    [int]$RetentionDays = 7,
    [string]$LogPath,
    [string]$AlertWebhookUrl
)

$ErrorActionPreference = "Stop"
Import-Module (Join-Path $PSScriptRoot "IKProBackup.psm1") -Force

$script:startedAt = Get-Date

function Backup-OneDatabaseLog {
    param([Parameter(Mandatory = $true)][string]$Database)

    $model = Get-IKProRecoveryModel -ServerInstance $ServerInstance -Database $Database
    if ($model -ne "FULL") {
        $mesaj = "Kurtarma modeli $model — işlem günlüğü yedeği ALINAMAZ, RPO (1 saat) bu " +
                 "veritabanı için geçersiz. Düzeltme: ALTER DATABASE [$Database] SET RECOVERY FULL;"
        Write-Host "  $Database : $mesaj" -ForegroundColor Red
        Write-IKProResult -Islem "log-yedegi" -Success $false -Message $mesaj `
            -ServerInstance $ServerInstance -ForDatabase $Database `
            -LogPath $LogPath -AlertWebhookUrl $AlertWebhookUrl -StartedAt $script:startedAt
        return $false
    }

    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $logDosya = Join-Path $BackupPath "$Database-$stamp.trn"
    $logKomut = "BACKUP LOG [$Database] TO DISK = N'$logDosya' WITH INIT, CHECKSUM;"

    # ZİNCİRİN TEMELİ: log yedeği, geçerli bir tam yedeğe dayanmadan alınamaz
    # (Msg 4214). Bunun sebebini ÖNCEDEN TAHMİN ETMEK yerine duruma TEPKİ
    # veriyoruz — sebep birden çok olabilir ve listelemeye kalkmak eksik kalır:
    #   * yeni provizyonlanan kiracı (hiç tam yedeği yok),
    #   * kurtarma modeli SIMPLE'a alınıp FULL'e döndürülmüş (zincir kırılır ama
    #     msdb'deki ESKİ tam yedek kaydı hâlâ durur — "tam yedeği var mı?" diye
    #     sormak burada YANILTIR; bu tuzağa bir kez düşüldü),
    #   * veritabanı yedekten geri yüklenmiş.
    # Denemek ve hataya göre davranmak, hepsini tek yoldan çözer.
    try {
        Invoke-IKProSql -ServerInstance $ServerInstance -Query $logKomut | Out-Null
    }
    catch {
        if ("$_" -notmatch '4214|no current database backup') { throw }

        $tamDosya = Join-Path $BackupPath "$Database-$stamp-TABAN.bak"
        Write-Host "  $Database : log zinciri yok — önce TABAN tam yedeği alınıyor"
        Invoke-IKProSql -ServerInstance $ServerInstance `
            -Query "BACKUP DATABASE [$Database] TO DISK = N'$tamDosya' WITH INIT, CHECKSUM;" | Out-Null

        # Tek sefer yeniden dene. İkinci kez 4214 gelirse bu artık "temeli yok"
        # değil başka bir arızadır ve yukarı fırlamalı — sonsuz döngü kurmuyoruz.
        Invoke-IKProSql -ServerInstance $ServerInstance -Query $logKomut | Out-Null
    }

    $boyutKb = [Math]::Round((Get-Item $logDosya).Length / 1KB, 1)
    Write-Host ("  {0} : {1} KB" -f $Database, $boyutKb)

    Write-IKProResult -Islem "log-yedegi" -Success $true -Message "İşlem günlüğü alındı" `
        -ServerInstance $ServerInstance -ForDatabase $Database `
        -Details @{ dosya = $logDosya; boyutKb = $boyutKb } `
        -LogPath $LogPath -AlertWebhookUrl $AlertWebhookUrl -StartedAt $script:startedAt
    return $true
}

function Remove-ExpiredLogBackups {
    <#
      Saklama süresi dolan .trn dosyalarını siler.

      YALNIZ .trn: tam yedekler (.bak) bu script tarafından ASLA silinmez. Bir
      log zinciri, dayandığı tam yedek olmadan işe yaramaz; yanlışlıkla temeli
      silmek, elde binlerce günlük dosyası olup hiçbirini kullanamamak demektir.
    #>
    $sinir = (Get-Date).AddDays(-[Math]::Abs($RetentionDays))
    $eskiler = @(Get-ChildItem -Path $BackupPath -Filter "*.trn" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -lt $sinir })

    if ($eskiler.Count -eq 0) { return }

    foreach ($dosya in $eskiler) { Remove-Item -Path $dosya.FullName -Force -ErrorAction SilentlyContinue }
    Write-Host ("Saklama süresi dolan {0} günlük dosyası silindi ({1} günden eski)." -f `
        $eskiler.Count, $RetentionDays)
}

try {
    if (-not (Test-Path $BackupPath)) { New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null }
    $BackupPath = (Resolve-Path $BackupPath).Path

    $hedefler = @(if ($Database) { $Database } else {
        Get-IKProBackupTargets -ServerInstance $ServerInstance `
            -PlatformDatabase $PlatformDatabase -SharedDatabase $SharedDatabase
    })

    Write-Host ("İşlem günlüğü yedeği — {0} veritabanı" -f $hedefler.Count)

    $basarisizlar = @()
    foreach ($hedef in $hedefler) {
        # Bir veritabanının hatası DİĞERLERİNİ durdurmaz: sıradaki tek bir arıza,
        # geri kalan müşterileri o saat için korumasız bırakmamalı.
        try {
            if (-not (Backup-OneDatabaseLog -Database $hedef)) { $basarisizlar += $hedef }
        }
        catch {
            Write-Host "  $hedef : $_" -ForegroundColor Red
            Write-IKProResult -Islem "log-yedegi" -Success $false -Message "$_" `
                -ServerInstance $ServerInstance -ForDatabase $hedef `
                -LogPath $LogPath -AlertWebhookUrl $AlertWebhookUrl -StartedAt $script:startedAt
            $basarisizlar += $hedef
        }
    }

    Remove-ExpiredLogBackups

    if ($basarisizlar.Count -gt 0) {
        Write-Host ("LOG YEDEĞİ BAŞARISIZ — {0}/{1}: {2}" -f `
            $basarisizlar.Count, $hedefler.Count, ($basarisizlar -join ", ")) -ForegroundColor Red
        Write-IKProResult -Islem "log-yedegi-ozet" -Success $false `
            -Message "Bazı veritabanlarının günlüğü alınamadı" -ServerInstance $ServerInstance `
            -Details @{ basarisizVeritabanlari = $basarisizlar; toplamVeritabani = $hedefler.Count } `
            -LogPath $LogPath -AlertWebhookUrl $AlertWebhookUrl -StartedAt $script:startedAt
        exit 1
    }

    Write-Host ("LOG YEDEĞİ TAMAM — {0} veritabanı." -f $hedefler.Count) -ForegroundColor Green
    Write-IKProResult -Islem "log-yedegi-ozet" -Success $true -Message "Tüm günlükler alındı" `
        -ServerInstance $ServerInstance `
        -Details @{ veritabanlari = $hedefler; toplamVeritabani = $hedefler.Count } `
        -LogPath $LogPath -AlertWebhookUrl $AlertWebhookUrl -StartedAt $script:startedAt
    exit 0
}
catch {
    $hata = "$_"
    Write-Host "LOG YEDEĞİ BAŞARISIZ: $hata" -ForegroundColor Red
    Write-IKProResult -Islem "log-yedegi-ozet" -Success $false -Message $hata `
        -ServerInstance $ServerInstance -LogPath $LogPath -AlertWebhookUrl $AlertWebhookUrl `
        -StartedAt $script:startedAt
    exit 1
}
