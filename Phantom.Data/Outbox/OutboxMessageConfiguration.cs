using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phantom.Messaging.Outbox;

namespace Phantom.Data.Outbox;

/// <summary>
/// EF Core entity type configuration for <see cref="OutboxMessage"/>.
/// Configures the OutboxMessages table schema, indexes, and column constraints.
/// </summary>
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <summary>
    /// Configures the entity type for <see cref="OutboxMessage"/>.
    /// </summary>
    /// <param name="builder">The entity type builder.</param>
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
