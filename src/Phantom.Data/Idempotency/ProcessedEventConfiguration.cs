using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Phantom.Infrastructure.Abstractions.Idempotency;

namespace Phantom.Data.Idempotency;

public class ProcessedEventConfiguration : IEntityTypeConfiguration<ProcessedEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedEvent> builder)
    {
        builder.ToTable("ProcessedEvents");

        builder.HasKey(e => e.EventId);

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.ProcessedAt)
            .IsRequired();

        builder.HasIndex(e => e.EventType);

        builder.HasIndex(e => e.ProcessedAt)
            .HasDatabaseName("IX_ProcessedEvents_ProcessedAt");
    }
}
