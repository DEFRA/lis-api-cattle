// <copyright file="CtsRequestConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Configurations;

using Defra.Lis.Entities;
using Defra.Livestock.Sdk.Core.Extensions;

public class CtsRequestConfiguration : IEntityTypeConfiguration<CtsRequest>
{
    public void Configure(EntityTypeBuilder<CtsRequest> builder)
    {
        builder.ToTable("cts_requests", "public");

        builder.HasKey(e => e.Id).HasName("cts_request_pk");

        // The entity creates its own ID, so a request recorded against a bundle that is already stored is inserted
        // rather than mistaken for an existing row.
        builder.Property(e => e.Id)
            .HasColumnName(nameof(CtsRequest.Id).ToSnakeCase())
            .ValueGeneratedNever();

        builder.Property(e => e.SubmissionId)
            .HasColumnName(nameof(CtsRequest.SubmissionId).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.TxnId)
            .HasColumnName(nameof(CtsRequest.TxnId).ToSnakeCase())
            .HasMaxLength(CtsRequest.MaxTxnIdLength)
            .IsRequired();

        builder.Property(e => e.Operation)
            .HasColumnName(nameof(CtsRequest.Operation).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.Payload)
            .HasColumnName(nameof(CtsRequest.Payload).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName(nameof(CtsRequest.CreatedAt).ToSnakeCase())
            .HasDefaultValueSql("now()")
            .IsRequired();

        builder.Property(e => e.SentAt)
            .HasColumnName(nameof(CtsRequest.SentAt).ToSnakeCase());

        builder.Property(e => e.ReceiptNumber)
            .HasColumnName(nameof(CtsRequest.ReceiptNumber).ToSnakeCase());

        builder.Property(e => e.LastError)
            .HasColumnName(nameof(CtsRequest.LastError).ToSnakeCase());

        builder.HasIndex(e => e.SubmissionId)
            .IsUnique()
            .HasDatabaseName("cts_request_submission_id_unique");

        builder.HasIndex(e => e.TxnId)
            .IsUnique()
            .HasDatabaseName("cts_request_txn_id_unique");
    }
}
