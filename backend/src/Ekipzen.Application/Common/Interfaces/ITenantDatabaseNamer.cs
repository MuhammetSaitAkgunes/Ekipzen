namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Kiracının katalog adını slug'dan üretir. Ad provizyonda BİR KEZ üretilir ve
/// bir daha değişmez (bkz. <c>Tenant.DatabaseName</c>).
///
/// <b>Neden statik bir yardımcı değil:</b> önek yapılandırmadan gelir
/// (<c>Tenancy:DatabasePrefix</c>). Entegrasyon testleri kendi öneklerini
/// kullanır — aksi halde test koşumu, geliştiricinin makinesindeki gerçek
/// <c>Ekipzen_demo</c> veritabanını düşürüp yeniden kurardı.
/// </summary>
public interface ITenantDatabaseNamer
{
    string For(string slug);

    /// <summary>Bu kurulumun kullandığı önek — süpürme/teşhis amaçlı.</summary>
    string Prefix { get; }
}
