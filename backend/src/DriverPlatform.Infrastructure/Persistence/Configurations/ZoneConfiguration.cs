using DriverPlatform.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DriverPlatform.Infrastructure.Persistence.Configurations;

internal sealed class ZoneConfiguration : IEntityTypeConfiguration<Zone>
{
    public void Configure(EntityTypeBuilder<Zone> builder)
    {
        builder.OwnsOne(z => z.NameTranslations, t => t.ToJson());
        builder.OwnsMany(z => z.Polygon, p => p.ToJson());
        builder.OwnsMany(z => z.MeetingPoints, m =>
        {
            m.ToJson();
            m.OwnsOne(p => p.NameTranslations);
            m.OwnsOne(p => p.ClientInstructionsTranslations);
        });
    }
}
