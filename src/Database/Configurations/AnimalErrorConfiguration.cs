// <copyright file="AnimalErrorConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Configurations;

using Defra.Lis.Entities;
using Defra.Livestock.Sdk.Core.Extensions;

public class AnimalErrorConfiguration : IEntityTypeConfiguration<AnimalError>
{
    public void Configure(EntityTypeBuilder<AnimalError> builder)
    {
        builder.ToTable("animal_errors", "public");

        builder.HasKey(e => e.Id).HasName("animal_error_pk");

        // The entity creates its own ID, so an error added to an animal that is already stored is inserted rather
        // than mistaken for an existing row.
        builder.Property(e => e.Id)
            .HasColumnName(nameof(AnimalError.Id).ToSnakeCase())
            .ValueGeneratedNever();

        builder.Property(e => e.AnimalId)
            .HasColumnName(nameof(AnimalError.AnimalId).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.ErrorCode)
            .HasColumnName(nameof(AnimalError.ErrorCode).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.ErrorText)
            .HasColumnName(nameof(AnimalError.ErrorText).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.Source)
            .HasColumnName(nameof(AnimalError.Source).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName(nameof(AnimalError.CreatedAt).ToSnakeCase())
            .IsRequired();

        // Recording the same CTS response twice must not store the same error twice.
        builder.HasIndex(e => new { e.AnimalId, e.ErrorCode })
            .IsUnique()
            .HasDatabaseName("animal_error_animal_id_error_code_unique");
    }
}
