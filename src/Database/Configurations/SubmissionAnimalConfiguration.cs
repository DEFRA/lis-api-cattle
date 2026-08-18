using Defra.Lis.Core.Extensions;

namespace Lis.Cattle.Configurations;

public class SubmissionAnimalConfiguration : IEntityTypeConfiguration<SubmissionAnimal>
{
    public void Configure(EntityTypeBuilder<SubmissionAnimal> builder)
    {
        builder.ToTable(nameof(SubmissionAnimal).ToSnakeCase(), "public");

        builder.HasKey(e => e.Id).HasName("submission_animal_pk");

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("uuid_generate_v4()");

        builder.Property(e => e.SubmissionId)
            .HasColumnName(nameof(SubmissionAnimal.SubmissionId).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName(nameof(SubmissionAnimal.Status).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.EarTag)
            .HasColumnName(nameof(SubmissionAnimal.EarTag).ToSnakeCase())
            .IsRequired();

        builder.Property(e => e.DateBirth)
            .HasColumnName(nameof(SubmissionAnimal.DateBirth).ToSnakeCase());

        builder.Property(e => e.Sex)
            .HasColumnName(nameof(SubmissionAnimal.Sex).ToSnakeCase());

        builder.Property(e => e.Breed)
            .HasColumnName(nameof(SubmissionAnimal.Breed).ToSnakeCase());

        builder.Property(e => e.DamType)
            .HasColumnName(nameof(SubmissionAnimal.DamType).ToSnakeCase());

        builder.Property(e => e.DamGeneticEarTag)
            .HasColumnName(nameof(SubmissionAnimal.DamGeneticEarTag).ToSnakeCase());

        builder.Property(e => e.DamSurrogateEarTag)
            .HasColumnName(nameof(SubmissionAnimal.DamSurrogateEarTag).ToSnakeCase());

        builder.Property(e => e.SireEarTag)
            .HasColumnName(nameof(SubmissionAnimal.SireEarTag).ToSnakeCase());

        builder.Property(e => e.SireName)
            .HasColumnName(nameof(SubmissionAnimal.SireName).ToSnakeCase());

        builder.HasMany(e => e.Errors)
            .WithOne(err => err.Animal)
            .HasForeignKey(err => err.AnimalId)
            .HasConstraintName("submission_animal_errors_submission_animal_id_fk");
    }
}