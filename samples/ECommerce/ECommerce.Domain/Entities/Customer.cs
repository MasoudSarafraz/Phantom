using ECommerce.Domain.ValueObjects;
using Phantom.Core.Domain;

namespace ECommerce.Domain.Entities;

public class Customer : AuditableSoftDeleteEntity<Guid>
{
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public EmailAddress Email { get; private set; } = default!;

    private Customer() { }

    public Customer(Guid id, string firstName, string lastName, EmailAddress email) : base(id)
    {
        FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName));
        LastName = lastName ?? throw new ArgumentNullException(nameof(lastName));
        Email = email ?? throw new ArgumentNullException(nameof(email));
    }

    public string FullName => $"{FirstName} {LastName}";

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName));
        LastName = lastName ?? throw new ArgumentNullException(nameof(lastName));
    }
}
