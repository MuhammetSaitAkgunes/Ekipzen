using FluentAssertions;
using Ekipzen.Application.Common.Interfaces;
using Ekipzen.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ekipzen.Tests.Unit.Storage;

/// <summary>
/// Depo kiracıya bağlıdır: her işlem {kök}/tenant-{id}/ altında geçer.
/// Ön ek her zaman AKTİF kiracıdan uygulandığı için kiracılar arası kaçış
/// yapısal olarak imkânsızdır.
/// </summary>
public class LocalFileStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ekipzen-storage-{Guid.NewGuid():N}");
    private readonly TestKiraci _kiraci = new(1);

    private sealed class TestKiraci(int? tenantId) : ICurrentTenant
    {
        public int? TenantId { get; private set; } = tenantId;
        public int TenantIdOrThrow() => TenantId ?? throw new InvalidOperationException("Aktif kiracı yok.");
        public void Impersonate(int id) => TenantId = id;
    }

    private LocalFileStorage Depo(ICurrentTenant? kiraci = null) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Root"] = _root })
            .Build(),
            kiraci ?? _kiraci);

    private static Stream Icerik(string metin) => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(metin));

    [Fact]
    public async Task SaveAsync_DosyayiKiraciKlasorununAltinaYazar()
    {
        var depo = Depo();

        var stored = await depo.SaveAsync(Icerik("evrak"), "ozluk.pdf", "documents-emp-5", CancellationToken.None);

        // Dönen yol kiracıdan BAĞIMSIZ görelidir — veritabanı bunu saklar.
        stored.Path.Should().StartWith("documents-emp-5/");
        stored.Path.Should().NotContain("tenant-");

        // Diskte ise kiracı klasörünün altındadır.
        var beklenen = Path.Combine(
            _root,
            LocalFileStorage.TenantFolder(1),
            stored.Path.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(beklenen).Should().BeTrue();
    }

    [Fact]
    public async Task OpenReadAsync_BaskaKiracininDosyasiniOkuyamaz()
    {
        var kiraciA = new TestKiraci(1);
        var yol = (await Depo(kiraciA)
            .SaveAsync(Icerik("gizli"), "a.pdf", "documents-emp-5", CancellationToken.None)).Path;

        var kiraciB = new TestKiraci(2);

        // Aynı göreli yol, farklı kiracı: B'nin alanında böyle bir dosya yok.
        await FluentActions.Invoking(() => Depo(kiraciB).OpenReadAsync(yol, CancellationToken.None))
            .Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task OpenReadAsync_KiraciAlanindanKacanYolReddedilir()
    {
        var depo = Depo();
        var kacis = Path.Combine("..", LocalFileStorage.TenantFolder(2), "documents-emp-5", "x.pdf");

        await FluentActions.Invoking(() => depo.OpenReadAsync(kacis, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveAsync_KiraciBaglamiYoksaReddedilir()
    {
        var kiracisiz = new TestKiraci(null);

        await FluentActions
            .Invoking(() => Depo(kiracisiz).SaveAsync(Icerik("x"), "a.pdf", "documents-emp-5", CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteTenantSpaceAsync_TumDosyaTurleriniSiler_BaskaKiraciyaDokunmaz()
    {
        var kiraciA = new TestKiraci(1);
        var depoA = Depo(kiraciA);
        await depoA.SaveAsync(Icerik("evrak"), "a.pdf", "documents-emp-5", CancellationToken.None);
        await depoA.SaveAsync(Icerik("foto"), "a.png", "photos", CancellationToken.None);
        await depoA.SaveAsync(Icerik("logo"), "a.png", "branding", CancellationToken.None);

        var kiraciB = new TestKiraci(2);
        var bYolu = (await Depo(kiraciB)
            .SaveAsync(Icerik("b"), "b.pdf", "documents-emp-9", CancellationToken.None)).Path;

        await depoA.DeleteTenantSpaceAsync(1, CancellationToken.None);

        Directory.Exists(Path.Combine(_root, LocalFileStorage.TenantFolder(1)))
            .Should().BeFalse("kiracının tüm dosya alanı silinmeli");

        await using var stream = await Depo(kiraciB).OpenReadAsync(bYolu, CancellationToken.None);
        stream.Should().NotBeNull("başka kiracının alanına dokunulmamalı");
    }

    [Fact]
    public async Task DeleteTenantSpaceAsync_AlanYoksaSessizceGecer()
    {
        await FluentActions.Invoking(() => Depo().DeleteTenantSpaceAsync(99, CancellationToken.None))
            .Should().NotThrowAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }
}
