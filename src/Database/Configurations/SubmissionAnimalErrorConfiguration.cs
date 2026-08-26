// <copyright file="SubmissionAnimalErrorConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Configurations;

using Defra.Lis.Core.Extensions;
using Defra.Lis.Entities;

public class SubmissionAnimalErrorConfiguration : IEntityTypeConfiguration<SubmissionAnimalError>
{
    public void Configure(EntityTypeBuilder<SubmissionAnimalError> builder)
    {
        builder.ToTable("submission_animal_errors", "public");

        builder.HasKey(e => e.Id).HasName("submission_animal_errors_pk");

        builder.Property(e => e.Id)
            .HasColumnName(nameof(SubmissionAnimalError.Id).ToSnakeCase());

        builder.Property(e => e.AnimalId)
            .HasColumnName(nameof(SubmissionAnimalError.AnimalId).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.ErrorCode)
            .HasColumnName(nameof(SubmissionAnimalError.ErrorCode).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.ErrorText)
            .HasColumnName(nameof(SubmissionAnimalError.ErrorText).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName(nameof(SubmissionAnimalError.CreatedAt).ToSnakeCase())
            .HasDefaultValueSql("now()");
    }
}
