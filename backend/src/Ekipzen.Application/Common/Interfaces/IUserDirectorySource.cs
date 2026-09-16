namespace Ekipzen.Application.Common.Interfaces;

/// <summary>
/// Bir kiracının kullanıcı e-postalarını, dizin yeniden kurmak için verir.
/// Identity Infrastructure katmanında olduğu için Application katmanı ona
/// doğrudan bakamaz; bu arayüz aradaki köprüdür.
/// </summary>
public interface IUserDirectorySource
{
    Task<IReadOnlyList<string>> NormalizedEmailsAsync(int tenantId, CancellationToken cancellationToken);
}
