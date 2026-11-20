using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class SiteContentConfiguration : IEntityTypeConfiguration<SiteContent>
{
    public void Configure(EntityTypeBuilder<SiteContent> builder)
    {
        builder.Property(c => c.Key).HasMaxLength(100);
        builder.HasIndex(c => c.Key).IsUnique();
        foreach (var language in new[] { nameof(SiteContent.En), nameof(SiteContent.It), nameof(SiteContent.De), nameof(SiteContent.Fr) })
        {
            builder.Property(language).HasMaxLength(Application.Content.SiteContentService.MaxTextLength);
        }
    }
}
