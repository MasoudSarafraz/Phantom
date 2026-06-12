namespace Phantom.Core.Services;

/// <summary>
/// Marker interface for domain services. Implement this interface on classes that encapsulate
/// domain logic which does not naturally fit within an entity or value object. Domain services
/// are registered with the dependency injection container during application startup by scanning
/// for implementations of this interface.
/// </summary>
public interface IDomainService
{
}
