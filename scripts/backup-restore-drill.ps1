<#
.SYNOPSIS
    İK Pro veritabanları için yedek alma + geri yükleme tatbikatı.

.DESCRIPTION
    Yedeğin VAR OLMASI yeterli değildir; geri yüklenebildiği kanıtlanmalıdır.
    Script her veritabanı için tam yedek alır, yedeği AYRI bir ada geri yükler,
    tablo satır sayılarını kaynakla karşılaştırır ve tatbikat kopyasını düşürür.

    KİRACI BAŞINA VERİTABANI: İK Pro'da artık tek bir veritabanı yoktur. Her
    müşterinin verisi kendi kataloğundadır. Yedeklenecekler PLATFORM
    VERİTABANINDAN keşfedilir:

      1. IKProPlatform  - kiracı kimliği, yönlendirme dizini, abonelikler.
                          Onsuz diğer yedekler SAHİPSİZ kalır: hangi kataloğun
                          hangi müşteriye ait olduğunu yalnız o bilir.
      2. IKProDb        - paylaşılan katalog (bugün boş, tesisat gereği duruyor).
      3. IKPro_{slug}   - her müşteri için bir tane, Tenants tablosundan okunur.

    Keşif kaynağı neden platform tablosu, sunucudaki ad deseni değil: desen,
    silinmiş bir müşteriden kalan artık katalogu da yedekler ve gerçek müşteri
    listesiyle hiç doğrulanmaz. Tersi durum - platformda YAZILI olup sunucuda
    BULUNMAYAN katalog - gerçek bir arızadır ve tatbikatı durdurur.

    GÜVENLİK: Geri yükleme her zaman "<Database>_RestoreDrill" adına yapılır.
    Hedef adın kaynakla aynı olması reddedilir; mevcut veri asla üzerine yazılmaz.

.PARAMETER Database
    YALNIZ bu veritabanını tatbik et (keşif yapılmaz). Tek müşteriye hedefli
    tatbikat için.

.PARAMETER PlatformDatabase
    Kiracı listesinin okunacağı platform veritabanı. Varsayılan: IKProPlatform

.PARAMETER SharedDatabase
    Paylaşılan katalog. Varsayılan: IKProDb

.PARAMETER BackupPath
    Yedek dosyalarının yazılacağı klasör. SQL Server servis hesabının yazma izni
    olmalıdır.

.PARAMETER ServerInstance
    SQL Server örneği. Varsayılan: localhost

.PARAMETER KeepRestoredCopy
    Verilirse tatbikat kopyaları silinmez (elle inceleme için).

.EXAMPLE
    # Tüm veritabanları (platform + paylaşılan + her müşteri):
    pwsh scripts/backup-restore-drill.ps1 -BackupPath C:\SQLYedek

.EXAMPLE
    # Yalnız tek bir müşteri:
    pwsh scripts/backup-restore-drill.ps1 -Database IKPro_acme -BackupPath C:\SQLYedek

.OUTPUTS
    Çıkış kodu 0 = TÜM veritabanlarının tatbikatı başarılı. 1 = en az biri
    başarısız. Kısmi başarı, başarı değildir: yedeklenmemiş tek müşteri bile
    felaket anında telafi edilemez.
#>
[CmdletBinding()]
param(
    # Bos birakilirsa yedeklenecekler platformdan KESFEDILIR.
    [string]$Database,
    [Parameter(Mandatory = $true)][string]$BackupPath,
    [string]$PlatformDatabase = "IKProPlatform",
    [string]$SharedDatabase = "IKProDb",
    [string]$ServerInstance = "localhost",
    [switch]$KeepRestoredCopy,

    # Yüklenen evrak/foto/logo dizini (App_Data/storage). Verilirse KİRACI BAŞINA
    # ayrı zip üretilir ({db}-tenant-{id}-{damga}.zip). Veritabanı tek başına yetmez:
    # dosyalar diskte durur, DB yalnız yollarını tutar.
    [string]$StoragePath,

    # İkinci (off-site) kopya hedefi. Yedek sunucuyla aynı diskte durursa disk
    # arızasında veriyle birlikte gider — bu yüzden ayrı konuma kopyalanır.
    [string]$OffsitePath,

    # Her koşumun sonucu JSON satırı olarak buraya eklenir (izleme/denetim izi).
    [string]$LogPath,

    # Başarısızlıkta POST edilir. Sessiz başarısızlık en tehlikeli durumdur:
    # yedek alınmıyorsa bunu felaket anında öğrenmemek gerekir.
    [string]$AlertWebhookUrl
)

$ErrorActionPreference = "Stop"
$script:startedAt = Get-Date
$script:storageArchives = @()
$script:offsiteCopies = @()

function Write-DrillResult {
    <#
      Sonucu JSON satırı olarak loglar ve başarısızlıkta uyarı gönderir.
      Uyarı gönderimi ASLA tatbikatın sonucunu değiştirmez (best-effort).
    #>
    param([bool]$Success, [string]$Message, [hashtable]$Details = @{}, [string]$ForDatabase = "")

    $record = [ordered]@{
        zaman    = (Get-Date).ToString("o")
        surenSn  = [int]((Get-Date) - $script:startedAt).TotalSeconds
        sunucu   = $ServerInstance
        veritabani = $(if ($ForDatabase) { $ForDatabase } else { "TUMU" })
        basarili = $Success
        mesaj    = $Message
    }
    foreach ($k in $Details.Keys) { $record[$k] = $Details[$k] }
    $json = ($record | ConvertTo-Json -Compress -Depth 4)

    if ($LogPath) {
        try {
            $dir = Split-Path -Parent $LogPath
            if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
            Add-Content -Path $LogPath -Value $json -Encoding utf8
        }
        catch { Write-Host "UYARI: sonuç loglanamadı — $_" -ForegroundColor Yellow }
    }

    if (-not $Success -and $AlertWebhookUrl) {
        try {
            Invoke-RestMethod -Uri $AlertWebhookUrl -Method Post -ContentType "application/json" `
                -Body (@{ text = "İK Pro yedek tatbikatı BAŞARISIZ ($(if ($ForDatabase) { $ForDatabase } else { 'tumu' })): $Message" } | ConvertTo-Json) `
                -TimeoutSec 20 | Out-Null
            Write-Host "       uyarı gönderildi"
        }
        catch { Write-Host "UYARI: uyarı gönderilemedi — $_" -ForegroundColor Yellow }
    }
}

function Invoke-Sql {
    param([string]$Query, [string]$OnDatabase = "master", [string]$Separator = "|")
    # Sütun ayracı açıkça verilir: dosya yolları boşluk içerebilir
    # ("C:\Program Files\..."), boşlukla ayrıştırma yolu ortadan böler.
    $output = & sqlcmd -S $ServerInstance -d $OnDatabase -E -b -h -1 -W -s $Separator -Q $Query 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL hatası: $output" }
    return $output
}

function Get-TableRowCounts {
    param([string]$OnDatabase)
    # sys.partitions üzerinden satır sayısı: büyük tabloda COUNT(*) taramasından hızlı.
    $query = @"
SET NOCOUNT ON;
SELECT t.name + '=' + CAST(SUM(p.rows) AS varchar(20))
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
GROUP BY t.name
ORDER BY t.name;
"@
    $rows = Invoke-Sql -Query $query -OnDatabase $OnDatabase
    $map = @{}
    foreach ($line in $rows) {
        $text = "$line".Trim()
        if ($text -match '^(.+)=(\d+)$') { $map[$Matches[1]] = [int]$Matches[2] }
    }
    return $map
}

function Invoke-DatabaseDrill {
    <#
      TEK bir veritabanı için tam tatbikat: yedek -> doğrula -> ayrı isme geri
      yükle -> satır sayılarını karşılaştır -> temizle.

      Başarı/başarısızlık DÖNDÜRÜR, script'i sonlandırmaz: bir müşterinin yedeği
      patladığında geri kalan müşterilerin tatbikatı yapılmadan kalmamalı. Toplam
      sonuç en sonda hesaplanır.
    #>
    param([Parameter(Mandatory = $true)][string]$Database)

    $drillDatabase = "${Database}_RestoreDrill"
    if ($drillDatabase -eq $Database) {
        Write-Host "Tatbikat hedefi kaynak veritabanıyla aynı olamaz ($Database)." -ForegroundColor Red
        return $false
    }

    Write-Host ""
    Write-Host "=== $Database ===" -ForegroundColor Cyan

$script:storageArchives = @()
    $script:offsiteCopies = @()

    try {
    if (-not (Test-Path $BackupPath)) { New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null }
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupFile = Join-Path (Resolve-Path $BackupPath) "$Database-$stamp.bak"

    # Kaynak sayımı yedekten ÖNCE alınır. Yedek anlık görüntüdür; yedek sırasında
    # yazma olursa geri yüklenen kopya CANLI kaynakla birebir tutmaz. Önce/sonra
    # aralığı alınıp geri yüklenen değerin bu aralıkta olması beklenir — aksi hâlde
    # trafik alan bir üretim veritabanında tatbikat sürekli yanlış alarm verirdi.
    $before = Get-TableRowCounts -OnDatabase $Database

    Write-Host "[1/5] Yedek alınıyor: $backupFile"
    Invoke-Sql -Query "BACKUP DATABASE [$Database] TO DISK = N'$backupFile' WITH INIT, CHECKSUM, STATS = 25;" | Out-Null

    Write-Host "[2/5] Yedek bütünlüğü doğrulanıyor (RESTORE VERIFYONLY)"
    Invoke-Sql -Query "RESTORE VERIFYONLY FROM DISK = N'$backupFile' WITH CHECKSUM;" | Out-Null

    Write-Host "[3/5] Ayrı isme geri yükleniyor: $drillDatabase"
    $fileList = Invoke-Sql -Query "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'$backupFile';"
    $moves = @()
    foreach ($line in $fileList) {
        # FILELISTONLY sütunları: LogicalName | PhysicalName | Type | ...
        $parts = "$line" -split '\|'
        if ($parts.Count -ge 2 -and $parts[1].Trim() -match '\.(mdf|ndf|ldf)$') {
            $logical = $parts[0].Trim()
            $ext = [System.IO.Path]::GetExtension($parts[1].Trim())
            $newPath = Join-Path (Resolve-Path $BackupPath) "$drillDatabase-$logical$ext"
            $moves += "MOVE N'$logical' TO N'$newPath'"
        }
    }
    if ($moves.Count -eq 0) { throw "Yedek dosya listesi okunamadı; geri yükleme yapılamıyor." }

    Invoke-Sql -Query @"
IF DB_ID('$drillDatabase') IS NOT NULL
BEGIN
    ALTER DATABASE [$drillDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$drillDatabase];
END
RESTORE DATABASE [$drillDatabase] FROM DISK = N'$backupFile' WITH $($moves -join ', '), REPLACE, RECOVERY;
"@ | Out-Null

    Write-Host "[4/5] Satır sayıları karşılaştırılıyor"
    $after = Get-TableRowCounts -OnDatabase $Database
    $restored = Get-TableRowCounts -OnDatabase $drillDatabase

    $mismatches = @()
    $drifted = 0
    foreach ($table in $before.Keys) {
        if (-not $restored.ContainsKey($table)) { $mismatches += "${table}: geri yüklemede YOK"; continue }

        $low = [Math]::Min($before[$table], $after[$table])
        $high = [Math]::Max($before[$table], $after[$table])
        if ($low -ne $high) { $drifted++ }

        if ($restored[$table] -lt $low -or $restored[$table] -gt $high) {
            $mismatches += "${table}: geri yükleme $($restored[$table]), beklenen aralık $low-$high"
        }
    }

    Write-Host ("       karşılaştırılan tablo: {0}, toplam satır: {1}" -f `
        $before.Count, ($before.Values | Measure-Object -Sum).Sum)
    if ($drifted -gt 0) {
        Write-Host "       not: yedek sırasında $drifted tabloda yazma oldu; aralık kontrolü uygulandı"
    }

    if ($mismatches.Count -gt 0) {
        Write-Host "TATBİKAT BAŞARISIZ — satır sayıları tutmuyor:" -ForegroundColor Red
        $mismatches | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        Write-DrillResult -Success $false -Message "Satır sayıları tutmuyor" `
            -Details @{ tutmayan = $mismatches } -ForDatabase $Database
        return $false
    }

    # --- Evrak dosyaları (KİRACI BAŞINA) ---------------------------------
    # Veritabanı tek başına yeterli değil: özlük evrakları, fotoğraflar ve şirket
    # logosu diskte durur, DB yalnız yollarını tutar. Sadece DB geri yüklenirse
    # kayıtlar var ama dosyalar yok olur.
    #
    # Her kiracı için AYRI arşiv üretilir. Böylece tek müşterinin dosyaları
    # diğerlerine dokunmadan geri yüklenebilir ve müşteri ayrıldığında KVKK gereği
    # YALNIZ onun yedeği imha edilebilir — paylaşımlı tek arşivde bu mümkün değildir.
    if ($StoragePath) {
        Write-Host "[4b] Evrak dosyaları kiracı başına arşivleniyor: $StoragePath"
        if (-not (Test-Path $StoragePath)) {
            throw "Evrak dizini bulunamadı: $StoragePath"
        }

        # Kiracı listesi klasör adlarından gelir; yedek script'i DB şemasına bağlı olmamalı.
        $kiraciKlasorleri = @(Get-ChildItem -Path $StoragePath -Directory -Filter "tenant-*" -ErrorAction SilentlyContinue)
        if ($kiraciKlasorleri.Count -eq 0) {
            # Hata değil: henüz hiçbir kiracı dosya yüklememiş olabilir.
            Write-Host "       kiracı klasörü yok; evrak arşivi üretilmedi"
        }

        foreach ($klasor in $kiraciKlasorleri) {
            $arsiv = Join-Path (Resolve-Path $BackupPath) "$Database-$($klasor.Name)-$stamp.zip"
            Compress-Archive -Path (Join-Path $klasor.FullName "*") -DestinationPath $arsiv -Force -ErrorAction Stop
            $script:storageArchives += $arsiv

            $dosyaSayisi = (Get-ChildItem -Path $klasor.FullName -Recurse -File).Count
            $arsivBoyut = [Math]::Round((Get-Item $arsiv).Length / 1KB, 1)
            Write-Host ("       {0}: {1} dosya → {2} KB" -f $klasor.Name, $dosyaSayisi, $arsivBoyut)
        }
    }

    # --- Off-site kopya --------------------------------------------------
    # Yedek, kaynak sunucuyla aynı diskte durursa disk arızasında veriyle birlikte
    # gider. İkinci konuma kopyalanır ve kopya BOYUT olarak doğrulanır.
    if ($OffsitePath) {
        Write-Host "[4c] Off-site kopyalanıyor: $OffsitePath"
        if (-not (Test-Path $OffsitePath)) { New-Item -ItemType Directory -Path $OffsitePath -Force | Out-Null }

        foreach ($kaynak in (@($backupFile) + $script:storageArchives) | Where-Object { $_ }) {
            $hedef = Join-Path (Resolve-Path $OffsitePath) (Split-Path -Leaf $kaynak)
            Copy-Item -Path $kaynak -Destination $hedef -Force -ErrorAction Stop
            $kaynakBoyut = (Get-Item $kaynak).Length
            $hedefBoyut = (Get-Item $hedef).Length
            if ($kaynakBoyut -ne $hedefBoyut) {
                throw "Off-site kopya eksik: $(Split-Path -Leaf $kaynak) ($kaynakBoyut → $hedefBoyut bayt)"
            }
            $script:offsiteCopies += $hedef
            Write-Host ("       {0} kopyalandı ve doğrulandı" -f (Split-Path -Leaf $kaynak))
        }
    }

    Write-Host "[5/5] Temizlik"
    if ($KeepRestoredCopy) {
        Write-Host "       tatbikat kopyası korundu: $drillDatabase"
    }
    else {
        Invoke-Sql -Query @"
ALTER DATABASE [$drillDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE [$drillDatabase];
"@ | Out-Null
        Write-Host "       tatbikat kopyası düşürüldü"
    }

    Write-Host ""
    Write-Host "TATBİKAT BAŞARILI — yedek alındı, geri yüklendi ve doğrulandı." -ForegroundColor Green
    Write-Host "Yedek dosyası: $backupFile"

    Write-DrillResult -Success $true -Message "Tatbikat başarılı" -Details @{
        yedekDosyasi  = $backupFile
        tabloSayisi   = $before.Count
        toplamSatir   = ($before.Values | Measure-Object -Sum).Sum
        evrakArsivleri = $script:storageArchives
        offsiteKopya  = $script:offsiteCopies
    } -ForDatabase $Database
    return $true
}
catch {
    $hata = "$_"
    Write-Host "TATBİKAT BAŞARISIZ: $hata" -ForegroundColor Red
    Write-DrillResult -Success $false -Message $hata -ForDatabase $Database
    return $false
}
}

function Get-DrillTargets {
    <#
      Yedeklenecek veritabanlarını PLATFORM tablosundan keşfeder — gerekçesi
      dosya başlığındaki .DESCRIPTION bölümünde.
    #>
    $hedefler = [System.Collections.Generic.List[string]]::new()

    # Platform HER ZAMAN ve İLK sırada: onsuz diğer yedekler sahipsiz kalır.
    $hedefler.Add($PlatformDatabase)

    $paylasilan = @(Invoke-Sql -Query "SET NOCOUNT ON; SELECT CASE WHEN DB_ID('$SharedDatabase') IS NULL THEN 0 ELSE 1 END;")
    if (($paylasilan | Select-Object -First 1) -match '1') { $hedefler.Add($SharedDatabase) }

    $satirlar = @(Invoke-Sql -OnDatabase $PlatformDatabase -Query @"
SET NOCOUNT ON;
SELECT t.DatabaseName + '|' + CAST(CASE WHEN DB_ID(t.DatabaseName) IS NULL THEN 0 ELSE 1 END AS varchar(1))
FROM Tenants t
WHERE t.DatabaseName IS NOT NULL
ORDER BY t.DatabaseName;
"@)

    $eksikler = @()
    foreach ($satir in $satirlar) {
        $parcalar = "$satir".Trim() -split '\|'
        if ($parcalar.Count -lt 2) { continue }
        $ad = $parcalar[0].Trim()
        if (-not $ad) { continue }
        if ($parcalar[1].Trim() -eq '1') { $hedefler.Add($ad) } else { $eksikler += $ad }
    }

    if ($eksikler.Count -gt 0) {
        # Yedeklenecek bir şey yok ama bu iyi haber DEĞİL: platform o müşterinin
        # verisinin orada olduğunu sanıyor. Sessizce atlamak, felaket anında
        # "yedeği hiç yokmuş" sürprizine dönüşür.
        throw ("Platformda kayıtlı ama sunucuda BULUNMAYAN katalog(lar): {0}. " -f ($eksikler -join ", ")) +
              "Yedek alınmadan önce bu tutarsızlık giderilmelidir."
    }

    return $hedefler
}

try {
    if (-not (Test-Path $BackupPath)) { New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null }

    $hedefler = @(if ($Database) { $Database } else { Get-DrillTargets })

    Write-Host ("Tatbikat edilecek veritabanı sayısı: {0}" -f $hedefler.Count)
    Write-Host ("  {0}" -f ($hedefler -join ", "))

    $basarisizlar = @()
    foreach ($hedef in $hedefler) {
        if (-not (Invoke-DatabaseDrill -Database $hedef)) { $basarisizlar += $hedef }
    }

    Write-Host ""
    if ($basarisizlar.Count -gt 0) {
        Write-Host ("TATBİKAT BAŞARISIZ - {0}/{1} veritabanı: {2}" -f `
            $basarisizlar.Count, $hedefler.Count, ($basarisizlar -join ", ")) -ForegroundColor Red
        Write-DrillResult -Success $false -Message "Bazı veritabanlarının tatbikatı başarısız" `
            -Details @{ basarisizVeritabanlari = $basarisizlar; toplamVeritabani = $hedefler.Count }
        exit 1
    }

    Write-Host ("TATBİKAT BAŞARILI - {0} veritabanının tamamı yedeklendi, geri yüklendi ve doğrulandı." -f `
        $hedefler.Count) -ForegroundColor Green
    Write-DrillResult -Success $true -Message "Tüm veritabanlarının tatbikatı başarılı" `
        -Details @{ veritabanlari = $hedefler; toplamVeritabani = $hedefler.Count }
    exit 0
}
catch {
    $hata = "$_"
    Write-Host "TATBİKAT BAŞARISIZ: $hata" -ForegroundColor Red
    Write-DrillResult -Success $false -Message $hata
    exit 1
}
