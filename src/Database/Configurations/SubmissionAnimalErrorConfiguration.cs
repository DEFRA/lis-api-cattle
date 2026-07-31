using Defra.Lis.Core.Extensions;

namespace Lis.Cattle.Configurations;

public class SubmissionAnimalErrorConfiguration : IEntityTypeConfiguration<SubmissionAnimalError>
{
    public void Configure(EntityTypeBuilder<SubmissionAnimalError> builder)
    {
        builder.ToTable(nameof(SubmissionAnimalError).ToSnakeCase(), "public");

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

        
        
    }
}
