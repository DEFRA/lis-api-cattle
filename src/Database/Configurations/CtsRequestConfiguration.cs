// <copyright file="CtsRequestConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Configurations;

using Defra.Lis.Core.Extensions;
using Defra.Lis.Entities;

public class CtsRequestConfiguration : IEntityTypeConfiguration<CtsRequest>
{
    public void Configure(EntityTypeBuilder<CtsRequest> builder)
    {
        builder.ToTable("cts_requests", "public");

        builder.HasKey(e => e.Id).HasName("cts_request_pk");

        builder.Property(e => e.Id)
            .HasColumnName(nameof(CtsRequest.Id).ToSnakeCase());

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
