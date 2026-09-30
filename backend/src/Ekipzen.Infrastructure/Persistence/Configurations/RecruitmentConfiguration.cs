using Ekipzen.Domain.Entities.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ekipzen.Infrastructure.Persistence.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> b)
    {
        b.Property(p => p.Title).IsRequired().HasMaxLength(128);

        b.HasOne(p => p.Department)
            .WithMany()
            .HasForeignKey(p => p.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class PositionCorrectionConfiguration : IEntityTypeConfiguration<PositionCorrection>
{
    public void Configure(EntityTypeBuilder<PositionCorrection> b)
    {
        b.Property(item => item.BeforeJson).IsRequired();
        b.Property(item => item.AfterJson).IsRequired();
        b.Property(item => item.Reason).IsRequired().HasMaxLength(2000);
        b.Property(item => item.ChangedByUserId).IsRequired().HasMaxLength(450);
        b.HasOne(item => item.Position).WithMany()
            .HasForeignKey(item => item.PositionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(item => new { item.TenantId, item.PositionId, item.ToVersion }).IsUnique();
    }
}

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> b)
    {
        b.Property(c => c.Name).IsRequired().HasMaxLength(200);
        b.Property(c => c.AppliedRole).IsRequired().HasMaxLength(128);
        b.Property(c => c.Location).HasMaxLength(128);
        b.Property(c => c.Summary).HasMaxLength(2000);

        b.HasOne(c => c.Position)
            .WithMany(p => p.Candidates)
            .HasForeignKey(c => c.PositionId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(c => c.Status);
    }
}

public sealed class CandidateCorrectionConfiguration : IEntityTypeConfiguration<CandidateCorrection>
{
    public void Configure(EntityTypeBuilder<CandidateCorrection> b)
    {
        b.Property(item => item.BeforeJson).IsRequired();
        b.Property(item => item.AfterJson).IsRequired();
        b.Property(item => item.Reason).IsRequired().HasMaxLength(2000);
        b.Property(item => item.ChangedByUserId).IsRequired().HasMaxLength(450);
        b.Property(item => item.ChangedByName).IsRequired().HasMaxLength(200);
        b.HasOne(item => item.Candidate).WithMany()
            .HasForeignKey(item => item.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(item => new { item.TenantId, item.CandidateId, item.ToVersion }).IsUnique();
    }
}

public sealed class CandidateEvidenceRevisionConfiguration : IEntityTypeConfiguration<CandidateEvidenceRevision>
{
    public void Configure(EntityTypeBuilder<CandidateEvidenceRevision> b)
    {
        b.Property(item => item.BeforeJson).IsRequired();
        b.Property(item => item.AfterJson).IsRequired();
        b.Property(item => item.Reason).IsRequired().HasMaxLength(2000);
        b.Property(item => item.ChangedByUserId).IsRequired().HasMaxLength(450);
        b.Property(item => item.ChangedByName).IsRequired().HasMaxLength(200);
        b.HasOne(item => item.Candidate).WithMany().HasForeignKey(item => item.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(item => item.InterviewNote).WithMany().HasForeignKey(item => item.InterviewNoteId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(item => item.Evaluation).WithMany().HasForeignKey(item => item.EvaluationId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(item => new { item.TenantId, item.InterviewNoteId, item.Version }).IsUnique()
            .HasFilter("[InterviewNoteId] IS NOT NULL");
        b.HasIndex(item => new { item.TenantId, item.EvaluationId, item.Version }).IsUnique()
            .HasFilter("[EvaluationId] IS NOT NULL");
        b.ToTable(table => table.HasCheckConstraint("CK_CandidateEvidenceRevisions_OneSource",
            "([InterviewNoteId] IS NOT NULL AND [EvaluationId] IS NULL) OR ([InterviewNoteId] IS NULL AND [EvaluationId] IS NOT NULL)"));
    }
}

public sealed class CandidateStageDecisionConfiguration : IEntityTypeConfiguration<CandidateStageDecision>
{
    public void Configure(EntityTypeBuilder<CandidateStageDecision> b)
    {
        b.Property(item => item.Reason).IsRequired().HasMaxLength(2000);
        b.Property(item => item.EvidenceJson).IsRequired();
        b.Property(item => item.DecidedByUserId).IsRequired().HasMaxLength(450);
        b.Property(item => item.DecidedByName).IsRequired().HasMaxLength(200);
        b.HasOne(item => item.Candidate).WithMany().HasForeignKey(item => item.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(item => new { item.TenantId, item.CandidateId, item.ToVersion }).IsUnique();
    }
}

public class CandidateSkillConfiguration : IEntityTypeConfiguration<CandidateSkill>
{
    public void Configure(EntityTypeBuilder<CandidateSkill> b)
    {
        b.Property(s => s.Name).IsRequired().HasMaxLength(128);

        b.HasOne(s => s.Candidate)
            .WithMany(c => c.Skills)
            .HasForeignKey(s => s.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CandidateExperienceConfiguration : IEntityTypeConfiguration<CandidateExperience>
{
    public void Configure(EntityTypeBuilder<CandidateExperience> b)
    {
        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.Company).IsRequired().HasMaxLength(200);
        b.Property(x => x.Period).HasMaxLength(64);
        b.Property(x => x.Description).HasMaxLength(2000);

        b.HasOne(x => x.Candidate)
            .WithMany(c => c.Experiences)
            .HasForeignKey(x => x.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class InterviewNoteConfiguration : IEntityTypeConfiguration<InterviewNote>
{
    public void Configure(EntityTypeBuilder<InterviewNote> b)
    {
        b.Property(n => n.AuthorUserId).HasMaxLength(450);
        b.Property(n => n.AuthorName).IsRequired().HasMaxLength(128);
        b.Property(n => n.NoteType).IsRequired().HasMaxLength(64);
        b.Property(n => n.Text).IsRequired().HasMaxLength(4000);

        b.HasOne(n => n.Candidate)
            .WithMany(c => c.Notes)
            .HasForeignKey(n => n.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CandidateEvaluationConfiguration : IEntityTypeConfiguration<CandidateEvaluation>
{
    public void Configure(EntityTypeBuilder<CandidateEvaluation> b)
    {
        b.Property(e => e.Criterion).IsRequired().HasMaxLength(128);

        b.HasOne(e => e.Candidate)
            .WithMany(c => c.Evaluations)
            .HasForeignKey(e => e.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CandidateHistoryConfiguration : IEntityTypeConfiguration<CandidateHistory>
{
    public void Configure(EntityTypeBuilder<CandidateHistory> b)
    {
        b.Property(h => h.Event).IsRequired().HasMaxLength(500);

        b.HasOne(h => h.Candidate)
            .WithMany(c => c.History)
            .HasForeignKey(h => h.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(h => new { h.CandidateId, h.OccurredAtUtc });
    }
}
