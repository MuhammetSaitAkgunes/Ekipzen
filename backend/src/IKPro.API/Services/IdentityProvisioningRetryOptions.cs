namespace IKPro.API.Services;

public sealed class IdentityProvisioningRetryOptions
{
    public const string SectionName = "IdentityProvisioning";

    public bool Enabled { get; init; } = true;
    public int IntervalSeconds { get; init; } = 60;
    public int BatchSize { get; init; } = 50;
}
