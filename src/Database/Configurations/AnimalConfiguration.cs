// <copyright file="AnimalConfiguration.cs" company="Defra">
// Copyright (c) Defra. All rights reserved.
// </copyright>

namespace Defra.Lis.Database.Configurations;

using Defra.Lis.Entities;
using Defra.Livestock.Sdk.Core.Extensions;

public class AnimalConfiguration : IEntityTypeConfiguration<Animal>
{
    public void Configure(EntityTypeBuilder<Animal> builder)
    {
        builder.ToTable("animals", "public");

        builder.HasKey(e => e.Id).HasName("animal_pk");

        // The entity creates its own ID, so an animal record is always inserted rather than mistaken for an
        // existing row.
        builder.Property(e => e.Id)
            .HasColumnName(nameof(Animal.Id).ToSnakeCase())
            .ValueGeneratedNever();

        // One record per animal: an ear tag is never issued to two animals.
        builder.Property(e => e.EarTag)
            .HasColumnName(nameof(Animal.EarTag).ToSnakeCase())
            .IsRequired();

        builder.HasIndex(e => e.EarTag)
            .IsUnique()
            .HasDatabaseName("animal_ear_tag_unique");

        builder.Property(e => e.Status)
            .HasColumnName(nameof(Animal.Status).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.DateBirth)
            .HasColumnName(nameof(Animal.DateBirth).ToSnakeCase());

        builder.Property(e => e.Sex)
            .HasColumnName(nameof(Animal.Sex).ToSnakeCase());

        builder.Property(e => e.Breed)
            .HasColumnName(nameof(Animal.Breed).ToSnakeCase());

        builder.Property(e => e.DamType)
            .HasColumnName(nameof(Animal.DamType).ToSnakeCase());

        builder.Property(e => e.DamGeneticEarTag)
            .HasColumnName(nameof(Animal.DamGeneticEarTag).ToSnakeCase());

        builder.Property(e => e.DamSurrogateEarTag)
            .HasColumnName(nameof(Animal.DamSurrogateEarTag).ToSnakeCase());

        builder.Property(e => e.SireEarTag)
            .HasColumnName(nameof(Animal.SireEarTag).ToSnakeCase());

        builder.Property(e => e.SireName)
            .HasColumnName(nameof(Animal.SireName).ToSnakeCase());

        builder.Property(e => e.SubmissionId)
            .HasColumnName(nameof(Animal.SubmissionId).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.SubmissionAnimalId)
            .HasColumnName(nameof(Animal.SubmissionAnimalId).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName(nameof(Animal.CreatedAt).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName(nameof(Animal.UpdatedAt).ToSnakeCase());

        builder.HasOne<Submission>()
            .WithMany()
            .HasForeignKey(e => e.SubmissionId)
            .HasConstraintName("animal_submission_id_fk");

        builder.HasOne<SubmissionAnimal>()
            .WithOne()
            .HasForeignKey<Animal>(e => e.SubmissionAnimalId)
            .HasConstraintName("animal_submission_animal_id_fk");

        builder.HasIndex(e => e.SubmissionAnimalId)
            .IsUnique()
            .HasDatabaseName("animal_submission_animal_id_unique");

        builder.HasMany(e => e.Errors)
            .WithOne()
            .HasForeignKey(err => err.AnimalId)
            .HasConstraintName("animal_error_animal_id_fk");

        builder.Navigation(e => e.Errors).HasField("errors");
    }
}
