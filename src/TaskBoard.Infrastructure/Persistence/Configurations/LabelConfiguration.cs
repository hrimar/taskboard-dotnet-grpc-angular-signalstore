using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskBoard.Domain;

namespace TaskBoard.Infrastructure.Persistence.Configurations;

public class LabelConfiguration : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("Labels");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name)
            .IsRequired()
            .HasMaxLength(50);

        // "#RRGGBB" is always 7 characters.
        builder.Property(l => l.Color)
            .IsRequired()
            .HasMaxLength(7);

        // Implicit many-to-many: EF creates a hidden join table (LabelTaskItem)
        // with the two foreign keys, no join entity class needed on our side.
        builder.HasMany(l => l.Tasks)
            .WithMany(t => t.Labels)
            .UsingEntity(j => j.ToTable("TaskItemLabels"));
    }
}
