namespace Ekipzen.Domain.Common;

/// <summary>
/// Base for all persisted entities with an integer identity key.
/// <para>
/// Multi-tenancy: her kalıcı varlık bir kiracıya (<see cref="TenantId"/>) aittir.
/// Bu alan <c>AuditableEntityInterceptor</c> tarafından ekleme anında aktif
/// kiracıyla otomatik damgalanır ve <c>AppDbContext</c>'teki global query filter
/// ile her sorguda otomatik izole edilir — böylece bir kiracı asla başka bir
/// kiracının verisini göremez.
/// </para>
/// </summary>
public abstract class BaseEntity : ITenantScoped
{
    public int Id { get; set; }

    /// <summary>Sahip kiracı (multi-tenant izolasyon anahtarı).</summary>
    public int TenantId { get; set; }
}
