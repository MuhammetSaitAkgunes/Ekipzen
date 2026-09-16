<#
.SYNOPSIS
    Ekipzen yedekleme script'lerinin ortak parçaları.

.DESCRIPTION
    Tam yedek tatbikatı (backup-restore-drill.ps1) ve saatlik işlem günlüğü
    yedeği (backup-log.ps1) AYNI veritabanı kümesi üzerinde çalışmak zorundadır.

    Keşif mantığı iki script'e ayrı ayrı yazılsaydı zamanla ayrışırdı ve sonuç
    sessiz olurdu: tam yedeği alınan ama log yedeği alınmayan bir müşteri,
    felaket anına kadar fark edilmez. Bu yüzden ortak kod tek bir yerde durur.
#>

function Invoke-EkipzenSql {
    <#
      sqlcmd sarmalayıcısı. Sütun ayracı AÇIKÇA verilir: dosya yolları boşluk
      içerebilir ("C:\Program Files\...") ve boşlukla ayrıştırma yolu ortadan böler.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$ServerInstance,
        [Parameter(Mandatory = $true)][string]$Query,
        [string]$OnDatabase = "master",
        [string]$Separator = "|"
    )

    $output = & sqlcmd -S $ServerInstance -d $OnDatabase -E -b -h -1 -W -s $Separator -Q $Query 2>&1
    if ($LASTEXITCODE -ne 0) { throw "SQL hatası: $output" }
    return $output
}

function Get-EkipzenBackupTargets {
    <#
      Yedeklenecek veritabanlarını PLATFORM tablosundan keşfeder.

      Neden platform tablosu, sunucudaki ad deseni değil: desen, silinmiş bir
      müşteriden kalan artık katalogu da yedekler ve gerçek müşteri listesiyle hiç
      doğrulanmaz. Tersi durum — platformda YAZILI olup sunucuda BULUNMAYAN
      katalog — sessiz geçilmemesi gereken bir arızadır ve istisna fırlatır.

      Sıra bilinçlidir: platform İLK. Onsuz diğer yedekler sahipsiz kalır —
      hangi kataloğun hangi müşteriye ait olduğunu yalnız o bilir.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$ServerInstance,
        [string]$PlatformDatabase = "EkipzenPlatform",
        [string]$SharedDatabase = "EkipzenDb"
    )

    $hedefler = [System.Collections.Generic.List[string]]::new()
    $hedefler.Add($PlatformDatabase)

    $paylasilan = @(Invoke-EkipzenSql -ServerInstance $ServerInstance `
        -Query "SET NOCOUNT ON; SELECT CASE WHEN DB_ID('$SharedDatabase') IS NULL THEN 0 ELSE 1 END;")
    if (($paylasilan | Select-Object -First 1) -match '1') { $hedefler.Add($SharedDatabase) }

    $satirlar = @(Invoke-EkipzenSql -ServerInstance $ServerInstance -OnDatabase $PlatformDatabase -Query @"
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
        throw ("Platformda kayıtlı ama sunucuda BULUNMAYAN katalog(lar): {0}. " -f ($eksikler -join ", ")) +
              "Yedek alınmadan önce bu tutarsızlık giderilmelidir."
    }

    return $hedefler
}

function Get-EkipzenRecoveryModel {
    param(
        [Parameter(Mandatory = $true)][string]$ServerInstance,
        [Parameter(Mandatory = $true)][string]$Database
    )

    $sonuc = Invoke-EkipzenSql -ServerInstance $ServerInstance `
        -Query "SET NOCOUNT ON; SELECT recovery_model_desc FROM sys.databases WHERE name = '$Database';"
    return "$($sonuc | Select-Object -First 1)".Trim()
}

function Write-EkipzenResult {
    <#
      Sonucu JSON satırı olarak loglar ve başarısızlıkta uyarı gönderir.
      Uyarı gönderimi ASLA işin sonucunu değiştirmez (best-effort): yedek
      alındıysa webhook'un ulaşamaması yedeği geçersiz kılmaz.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Islem,
        [Parameter(Mandatory = $true)][bool]$Success,
        [Parameter(Mandatory = $true)][string]$Message,
        [string]$ServerInstance = "",
        [string]$ForDatabase = "",
        [hashtable]$Details = @{},
        [string]$LogPath,
        [string]$AlertWebhookUrl,
        [datetime]$StartedAt = (Get-Date)
    )

    $record = [ordered]@{
        zaman      = (Get-Date).ToString("o")
        islem      = $Islem
        surenSn    = [int]((Get-Date) - $StartedAt).TotalSeconds
        sunucu     = $ServerInstance
        veritabani = $(if ($ForDatabase) { $ForDatabase } else { "TUMU" })
        basarili   = $Success
        mesaj      = $Message
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
            $hedef = if ($ForDatabase) { $ForDatabase } else { "tumu" }
            Invoke-RestMethod -Uri $AlertWebhookUrl -Method Post -ContentType "application/json" `
                -Body (@{ text = "Ekipzen $Islem BAŞARISIZ ($hedef): $Message" } | ConvertTo-Json) `
                -TimeoutSec 20 | Out-Null
        }
        catch { Write-Host "UYARI: uyarı gönderilemedi — $_" -ForegroundColor Yellow }
    }
}

Export-ModuleMember -Function Invoke-EkipzenSql, Get-EkipzenBackupTargets, Get-EkipzenRecoveryModel, Write-EkipzenResult
