using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phantom.Infrastructure.Abstractions.Outbox;

namespace Phantom.Data.Outbox;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.Channel)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(m => m.IsPublished);
        builder.HasIndex(m => m.CreatedAt);
    }
}
