// <copyright file="SubmissionConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Configurations;

using Defra.Lis.Entities;
using Defra.Livestock.Sdk.Core.Extensions;

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public const string RowVersion = "RowVersion";

    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.ToTable("submissions", "public");

        builder.HasKey(e => e.Id).HasName("submission_pk");

        builder.Property(e => e.Id)
            .HasColumnName(nameof(Submission.Id).ToSnakeCase());

        builder.Property(e => e.ClientReference)
            .HasColumnName(nameof(Submission.ClientReference).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.CountyParishHolding)
            .HasColumnName(nameof(Submission.CountyParishHolding).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.SubmittedBy)
            .HasColumnName(nameof(Submission.SubmittedBy).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName(nameof(Submission.Status).ToSnakeCase());

        builder.Property(e => e.CreatedAt)
            .HasColumnName(nameof(Submission.CreatedAt).ToSnakeCase())
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName(nameof(Submission.UpdatedAt).ToSnakeCase());

        builder.Property(e => e.CorrelationId)
            .HasColumnName(nameof(Submission.CorrelationId).ToSnakeCase());

        builder.Property(e => e.CtsReceiptNumber)
            .HasColumnName(nameof(Submission.CtsReceiptNumber).ToSnakeCase());

        builder.Property(e => e.CtsSentAt)
            .HasColumnName(nameof(Submission.CtsSentAt).ToSnakeCase());

        builder.Property(e => e.ErrorCode)
            .HasColumnName(nameof(Submission.ErrorCode).ToSnakeCase());

        builder.Property(e => e.ErrorText)
            .HasColumnName(nameof(Submission.ErrorText).ToSnakeCase());

        builder.Property(e => e.CompletedAt)
            .HasColumnName(nameof(Submission.CompletedAt).ToSnakeCase());

        builder.Property(e => e.CtsLastPolledAt)
            .HasColumnName(nameof(Submission.CtsLastPolledAt).ToSnakeCase());

        builder.Ignore(e => e.HasCtsOutcome);

        // Postgres row version (the xmin system column), so a save that overwrites another writer's change fails.
        builder.Property<uint>(RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.HasIndex(e => e.ClientReference)
            .HasDatabaseName("submission_client_reference_index");

        builder.HasIndex(e => e.CountyParishHolding)
            .HasDatabaseName("submission_county_parish_holding_index");

        builder.HasMany(e => e.Animals)
            .WithOne(a => a.Submission)
            .HasForeignKey(a => a.SubmissionId)
            .HasConstraintName("submission_animal_submission_id_fk");

        builder.HasOne(e => e.CtsRequest)
            .WithOne(r => r.Submission)
            .HasForeignKey<CtsRequest>(r => r.SubmissionId)
            .HasConstraintName("cts_request_submission_id_fk");
    }
}
