using Defra.Lis.Core.Extensions;

namespace Lis.Cattle.Configurations;

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
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

        builder.HasIndex(e => e.ClientReference)
            .HasDatabaseName("submission_client_reference_index");

        builder.HasIndex(e => e.CountyParishHolding)
            .HasDatabaseName("submission_county_parish_holding_index");

        builder.HasMany(e => e.Animals)
            .WithOne(a => a.Submission)
            .HasForeignKey(a => a.SubmissionId)
            .HasConstraintName("submission_animal_submission_id_fk");
    }
}