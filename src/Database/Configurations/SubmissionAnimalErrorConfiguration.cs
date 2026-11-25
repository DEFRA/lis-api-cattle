// <copyright file="SubmissionAnimalErrorConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Configurations;

using Defra.Lis.Entities;
using Defra.Livestock.Sdk.Core.Extensions;

public class SubmissionAnimalErrorConfiguration : IEntityTypeConfiguration<SubmissionAnimalError>
{
    public void Configure(EntityTypeBuilder<SubmissionAnimalError> builder)
    {
        builder.ToTable("submission_animal_errors", "public");

        builder.HasKey(e => e.Id).HasName("submission_animal_errors_pk");

        // The entity creates its own ID, so an error added to an animal that is already stored is inserted rather
        // than mistaken for an existing row.
        builder.Property(e => e.Id)
            .HasColumnName(nameof(SubmissionAnimalError.Id).ToSnakeCase())
            .ValueGeneratedNever();

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
