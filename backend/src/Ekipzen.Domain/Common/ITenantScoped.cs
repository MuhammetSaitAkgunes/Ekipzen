namespace Ekipzen.Domain.Common;

/// <summary>
/// Bir kiracıya ait, global query filter ile otomatik izole edilen tip.
/// Hem kalıcı varlıklar (<see cref="BaseEntity"/>) hem de SQL view read-model'leri
/// bunu uygular; <c>AppDbContext</c> tek bir reflection döngüsüyle hepsine filtre ekler.
/// Not: SQL view'ları global filtreyi baypas ettiğinden, view'ın kendisi de
/// <c>TenantId</c> kolonunu üretmeli (Faz 2 migration'ı).
/// </summary>
public interface ITenantScoped
{
    int TenantId { get; set; }
}
