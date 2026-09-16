using Ekipzen.Domain.Entities.Identity;
using Ekipzen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public sealed class IdentityProvisioningOperationConfiguration
    : IEntityTypeConfiguration<IdentityProvisioningOperation>
{
    public void Configure(EntityTypeBuilder<IdentityProvisioningOperation> builder)
    {
        builder.Property(operation => operation.Email).HasMaxLength(256);
        builder.Property(operation => operation.DisplayName).HasMaxLength(256);
        builder.Property(operation => operation.LastError).HasMaxLength(2000);

        builder.HasIndex(operation => new
        {
            operation.TenantId,
            operation.Status,
            operation.NextAttemptAtUtc,
        });

        builder.HasIndex(operation => new
        {
            operation.TenantId,
            operation.CandidateId,
            operation.Type,
        });

        builder.HasIndex(operation => new
        {
            operation.TenantId,
            operation.OffboardingCaseId,
        });

        builder.HasOne(operation => operation.Employee)
            .WithMany()
            .HasForeignKey(operation => operation.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(operation => operation.Candidate)
            .WithMany()
            .HasForeignKey(operation => operation.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(operation => operation.OffboardingCase)
            .WithMany()
            .HasForeignKey(operation => operation.OffboardingCaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}