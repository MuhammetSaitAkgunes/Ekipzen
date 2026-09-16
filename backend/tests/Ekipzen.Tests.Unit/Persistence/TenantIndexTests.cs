using FluentAssertions;
using Ekipzen.Domain.Common;
using Ekipzen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ekipzen.Tests.Unit.Persistence;

/// <summary>
/// Kiracı kapsamlı her tablo TenantId ile BAŞLAYAN bir indekse sahip olmalı.
///
/// Gerekçe: global query filter yüzünden uygulamadaki HER sorgu "TenantId = @p"
/// içerir. İndeks yoksa her sorgu tam tablo taraması yapar — tek bir uç noktayı
/// değil sistemin tamamını yavaşlatır ve ölçek büyüdükçe baskın maliyet olur.
///
/// Bu test kuralı modelde sabitler: yeni bir kiracı varlığı eklendiğinde indeks
/// otomatik gelmezse derleme değil ama test kırılır.
/// </summary>
public class TenantIndexTests
{
    private static AppDbContext ModelIcinContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            // Model kurmak için bağlantı açılmaz; dizenin geçerli olması yeterli.
            .UseSqlServer("Server=model-only;Database=model-only;Trusted_Connection=True;")
            .Options);

    [Fact]
    public void KiraciKapsamliHerTablo_TenantIdIleBaslayanIndekseSahiptir()
    {
        using var context = ModelIcinContext();

        // Kural ITenantScoped'a değil, TenantId SÜTUNUNA bağlanır: Identity varlıkları
        // (ApplicationUser, RefreshToken) ITenantScoped değildir ama TenantId taşır ve
        // kiracı bazında sorgulanır — onlar da kapsama girmeli.
        var indekssiz = context.Model.GetEntityTypes()
            .Where(e => e.FindProperty(nameof(ITenantScoped.TenantId)) is not null)
            // SQL view'larına indeks eklenemez; onlar altındaki tablolardan beslenir.
            .Where(e => e.GetViewName() is null)
            .Where(e => !e.GetIndexes().Any(i =>
                i.Properties.Count > 0 && i.Properties[0].Name == nameof(ITenantScoped.TenantId)))
            .Select(e => e.ClrType.Name)
            .OrderBy(name => name)
            .ToArray();

        indekssiz.Should().BeEmpty(
            "TenantId ile başlayan indeksi olmayan tablolar var: {0}", string.Join(", ", indekssiz));
    }

    /// <summary>
    /// Kiracı kapsamlı bir varlıktaki TEKİL indeks, kiracı sınırıyla çevrelenmiş
    /// olmalıdır. Aksi halde bir müşterinin verisi diğerinin veri girmesini
    /// engeller — üstelik hata mesajı komşu kiracıda o değerin var olduğunu
    /// sızdırır.
    ///
    /// İki kabul edilebilir biçim var:
    ///   1) İndeks TenantId ile BAŞLAR (ör. Departments: TenantId + Name).
    ///   2) İndeksin ilk sütunu, kiracı kapsamlı başka bir varlığa giden FK'dır
    ///      (ör. AttendanceRecords: EmployeeId + WorkDate — çalışan zaten tek bir
    ///      kiracıya ait olduğu için tekillik dolaylı olarak kiracıyla sınırlıdır).
    ///
    /// Kasıtlı GLOBAL tekillikler (Tenants.Slug, RefreshTokens.Token,
    /// Employees.UserId) bu kuralın dışındadır ve aşağıda tek tek listelenir —
    /// listeye eklemek bilinçli bir karar olmalıdır.
    /// </summary>
    [Fact]
    public void KiraciKapsamliTekilIndeksler_KiraciSinirindaKalir()
    {
        // Bilinçli global tekillikler: "VarlıkAdı.SütunAdı".
        string[] kasitliGloballer =
        [
            "Tenant.Slug",              // kiracı slug'ı platform genelinde tektir
            // RefreshToken BU MODELDE DEĞİL: platform veritabanına taşındı (Faz 2a),
            // dolayısıyla buradaki kural onu hiç değerlendirmiyor. Listede tutmak
            // "korunuyor" yanılsaması yaratırdı — koruma testinin güvenilirliği,
            // içindeki her satırın gerçekten bir şey ifade etmesine bağlıdır.
            "Employee.UserId",          // bir Identity kullanıcısı tek personele bağlanır

            // Login kiracı SORULMADAN yapılır: kullanıcı yalnız e-posta + parola
            // girer, kiracı ondan çözülür. Bu tasarımın kaçınılmaz sonucu, bir
            // e-posta adresinin sistemde tek bir kiracıya ait olmasıdır. İki farklı
            // müşteride çalışan kişi iki ayrı adres kullanmak zorundadır.
            // Değiştirilmek istenirse login akışına kiracı seçimi eklenmelidir.
            "ApplicationUser.NormalizedUserName",
        ];

        using var context = ModelIcinContext();

        var kacaklar = context.Model.GetEntityTypes()
            .Where(e => e.FindProperty(nameof(ITenantScoped.TenantId)) is not null)
            .Where(e => e.GetViewName() is null)
            .SelectMany(e => e.GetIndexes()
                .Where(i => i.IsUnique && i.Properties.Count > 0)
                .Where(i => !KiraciSinirindaMi(i.Properties[0]))
                .Select(i => $"{e.ClrType.Name}.{string.Join("+", i.Properties.Select(p => p.Name))}"))
            .Where(ad => !kasitliGloballer.Contains(ad))
            .OrderBy(ad => ad)
            .ToArray();

        kacaklar.Should().BeEmpty(
            "kiracı sınırını aşan tekil indeksler var — ikinci müşteri bu değerleri " +
            "kullanamaz: {0}", string.Join(", ", kacaklar));
    }

    // İlk sütun ya TenantId'dir ya da kiracı kapsamlı bir varlığa giden FK'dır.
    private static bool KiraciSinirindaMi(Microsoft.EntityFrameworkCore.Metadata.IProperty ilkSutun) =>
        ilkSutun.Name == nameof(ITenantScoped.TenantId)
        || ilkSutun.GetContainingForeignKeys().Any(fk =>
            fk.Properties[0] == ilkSutun
            && fk.PrincipalEntityType.FindProperty(nameof(ITenantScoped.TenantId)) is not null);
}
