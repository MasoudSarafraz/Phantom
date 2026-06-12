using Phantom.Core.Domain;
using Phantom.Core.Events;

namespace Phantom.Tests.Core;

// ─── Test Helpers ────────────────────────────────────────────────

public class TestEntity : Entity<Guid>
{
    public TestEntity(Guid id) : base(id) { }
    public TestEntity() { }
}

public class AnotherEntity : Entity<Guid>
{
    public AnotherEntity(Guid id) : base(id) { }
}

public class LongIdEntity : Entity<long>
{
    public LongIdEntity(long id) : base(id) { }
}

public class TestDomainEvent : DomainEvent
{
    public string Data { get; }
    public TestDomainEvent(string data) { Data = data; }
}

public class TestAggregateRoot : AggregateRoot<Guid>
{
    public TestAggregateRoot(Guid id) : base(id) { }
    public TestAggregateRoot() { }

    public void DoSomething(string eventData)
    {
        AddDomainEvent(new TestDomainEvent(eventData));
    }
}

// ─── Entity Tests ────────────────────────────────────────────────

public class EntityTests
{
    [Fact]
    public void Entities_With_Same_Id_And_Type_Should_Be_Equal()
    {
        var id = Guid.NewGuid();
        var e1 = new TestEntity(id);
        var e2 = new TestEntity(id);

        Assert.Equal(e1, e2);
        Assert.True(e1 == e2);
        Assert.False(e1 != e2);
        Assert.Equal(e1.GetHashCode(), e2.GetHashCode());
    }

    [Fact]
    public void Entities_With_Different_Id_Should_Not_Be_Equal()
    {
        var e1 = new TestEntity(Guid.NewGuid());
        var e2 = new TestEntity(Guid.NewGuid());

        Assert.NotEqual(e1, e2);
        Assert.True(e1 != e2);
    }

    [Fact]
    public void Entities_With_Same_Id_But_Different_Type_Should_Not_Be_Equal()
    {
        var id = Guid.NewGuid();
        var e1 = new TestEntity(id);
        var e2 = new AnotherEntity(id);

        Assert.NotEqual<Entity<Guid>>(e1, e2);
    }

    [Fact]
    public void Transient_Entities_Should_Not_Be_Equal()
    {
        var e1 = new TestEntity();
        var e2 = new TestEntity();

        // Two different transient entities should NOT be equal
        Assert.NotEqual(e1, e2);
    }

    [Fact]
    public void Same_Transient_Entity_Reference_Should_Be_Equal()
    {
        var e1 = new TestEntity();
        Assert.Equal(e1, e1); // ReferenceEquals
    }

    [Fact]
    public void IsTransient_Should_Return_True_For_Default_Id()
    {
        var entity = new TestEntity();
        Assert.True(entity.IsTransient());
    }

    [Fact]
    public void IsTransient_Should_Return_False_For_NonDefault_Id()
    {
        var entity = new TestEntity(Guid.NewGuid());
        Assert.False(entity.IsTransient());
    }

    [Fact]
    public void Entity_With_Long_Id_Should_Work()
    {
        var entity = new LongIdEntity(42);
        Assert.Equal(42, entity.Id);
        Assert.False(entity.IsTransient());
    }

    [Fact]
    public void Entity_Version_Defaults_To_Zero()
    {
        var entity = new TestEntity(Guid.NewGuid());
        Assert.Equal(0, entity.Version);
    }
}

// ─── ValueObject Tests ──────────────────────────────────────────

public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}

public class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }

    public Address(string street, string city)
    {
        Street = street;
        City = city;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
    }
}

public class ValueObjectTests
{
    [Fact]
    public void ValueObjects_With_Same_Components_Should_Be_Equal()
    {
        var m1 = new Money(100, "USD");
        var m2 = new Money(100, "USD");

        Assert.Equal(m1, m2);
        Assert.True(m1 == m2);
        Assert.Equal(m1.GetHashCode(), m2.GetHashCode());
    }

    [Fact]
    public void ValueObjects_With_Different_Components_Should_Not_Be_Equal()
    {
        var m1 = new Money(100, "USD");
        var m2 = new Money(200, "USD");

        Assert.NotEqual(m1, m2);
        Assert.True(m1 != m2);
    }

    [Fact]
    public void ValueObjects_With_Different_Currency_Should_Not_Be_Equal()
    {
        var m1 = new Money(100, "USD");
        var m2 = new Money(100, "EUR");

        Assert.NotEqual(m1, m2);
    }

    [Fact]
    public void Different_ValueObject_Types_Should_Not_Be_Equal()
    {
        var money = new Money(100, "USD");
        var address = new Address("100", "USD");

        Assert.NotEqual<ValueObject>(money, address);
    }

    [Fact]
    public void GetHashCode_Should_Have_Good_Distribution()
    {
        // XOR bug test: Money(5, "USD") and Money(5, "USD") where both components
        // have the same hash should NOT produce 0
        var m1 = new Money(5, "USD");
        var m2 = new Money(5, "USD");

        Assert.Equal(m1.GetHashCode(), m2.GetHashCode());
        Assert.NotEqual(0, m1.GetHashCode()); // XOR of identical values should not be 0
    }

    [Fact]
    public void ValueObject_IEquatable_Should_Work()
    {
        ValueObject m1 = new Money(100, "USD");
        ValueObject m2 = new Money(100, "USD");

        Assert.True(m1.Equals(m2));
    }
}

// ─── AggregateRoot Tests ────────────────────────────────────────

public class AggregateRootTests
{
    [Fact]
    public void AddDomainEvent_Should_Add_Event()
    {
        var ar = new TestAggregateRoot(Guid.NewGuid());
        ar.DoSomething("test-data");

        Assert.Single(ar.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_Should_Remove_All_Events()
    {
        var ar = new TestAggregateRoot(Guid.NewGuid());
        ar.DoSomething("data1");
        ar.DoSomething("data2");

        Assert.Equal(2, ar.DomainEvents.Count);
        ar.ClearDomainEvents();
        Assert.Empty(ar.DomainEvents);
    }

    [Fact]
    public void DomainEvents_Should_Preserve_Order()
    {
        var ar = new TestAggregateRoot(Guid.NewGuid());
        ar.DoSomething("first");
        ar.DoSomething("second");

        var events = ar.DomainEvents.Cast<TestDomainEvent>().ToList();
        Assert.Equal("first", events[0].Data);
        Assert.Equal("second", events[1].Data);
    }

    [Fact]
    public void AggregateRoot_Should_Implement_IAggregateRoot()
    {
        IAggregateRoot ar = new TestAggregateRoot(Guid.NewGuid());
        Assert.NotNull(ar);
    }
}

// ─── SoftDeleteEntity Tests ─────────────────────────────────────

public class TestSoftDeleteEntity : SoftDeleteEntity<Guid>
{
    public TestSoftDeleteEntity(Guid id) : base(id) { }
    public TestSoftDeleteEntity() { }
}

public class SoftDeleteEntityTests
{
    [Fact]
    public void SoftDelete_Should_Set_IsDeleted_True()
    {
        var entity = new TestSoftDeleteEntity(Guid.NewGuid());
        Assert.False(entity.IsDeleted);

        entity.SoftDelete();

        Assert.True(entity.IsDeleted);
        Assert.NotNull(entity.DeletedAt);
    }

    [Fact]
    public void SoftDelete_Should_Be_Idempotent()
    {
        var entity = new TestSoftDeleteEntity(Guid.NewGuid());
        entity.SoftDelete();
        var deletedAt1 = entity.DeletedAt;

        entity.SoftDelete(); // should not throw or change
        Assert.Equal(deletedAt1, entity.DeletedAt);
    }

    [Fact]
    public void Restore_Should_Set_IsDeleted_False()
    {
        var entity = new TestSoftDeleteEntity(Guid.NewGuid());
        entity.SoftDelete();
        Assert.True(entity.IsDeleted);

        entity.Restore();

        Assert.False(entity.IsDeleted);
    }

    [Fact]
    public void Restore_Should_Be_Idempotent()
    {
        var entity = new TestSoftDeleteEntity(Guid.NewGuid());
        entity.Restore(); // not deleted yet
        Assert.False(entity.IsDeleted);
    }

    [Fact]
    public void SoftDeleteEntity_Should_Implement_ISoftDeletable()
    {
        ISoftDeletable entity = new TestSoftDeleteEntity(Guid.NewGuid());
        Assert.False(entity.IsDeleted);
    }
}

// ─── AuditableEntity Tests ──────────────────────────────────────

public class TestAuditableEntity : AuditableEntity<Guid>, IAuditable
{
    public TestAuditableEntity(Guid id) : base(id) { }
    public TestAuditableEntity() { }
}

public class AuditableEntityTests
{
    [Fact]
    public void SetCreated_Should_Set_CreatedAt_And_CreatedBy()
    {
        var entity = new TestAuditableEntity(Guid.NewGuid());
        entity.SetCreated("user1");

        Assert.NotEqual(default, entity.CreatedAt);
        Assert.Equal("user1", entity.CreatedBy);
    }

    [Fact]
    public void SetCreated_Should_Be_Idempotent()
    {
        var entity = new TestAuditableEntity(Guid.NewGuid());
        entity.SetCreated("user1");
        var createdAt = entity.CreatedAt;

        entity.SetCreated("user2"); // should not override
        Assert.Equal("user1", entity.CreatedBy);
        Assert.Equal(createdAt, entity.CreatedAt);
    }

    [Fact]
    public void SetUpdated_Should_Set_UpdatedAt_And_UpdatedBy()
    {
        var entity = new TestAuditableEntity(Guid.NewGuid());
        entity.SetUpdated("user2");

        Assert.NotNull(entity.UpdatedAt);
        Assert.Equal("user2", entity.UpdatedBy);
    }

    [Fact]
    public void AuditableEntity_Should_Implement_IAuditable()
    {
        IAuditable entity = new TestAuditableEntity(Guid.NewGuid());
        Assert.NotNull(entity);
    }
}

// ─── AuditableSoftDeleteEntity Tests ────────────────────────────

public class TestAuditableSoftDeleteEntity : AuditableSoftDeleteEntity<Guid>, IAuditable
{
    public TestAuditableSoftDeleteEntity(Guid id) : base(id) { }
    public TestAuditableSoftDeleteEntity() { }
}

public class AuditableSoftDeleteEntityTests
{
    [Fact]
    public void SoftDelete_Should_Set_DeletedBy()
    {
        var entity = new TestAuditableSoftDeleteEntity(Guid.NewGuid());
        entity.SoftDelete("admin");

        Assert.True(entity.IsDeleted);
        Assert.Equal("admin", entity.DeletedBy);
        Assert.NotNull(entity.DeletedAt);
    }

    [Fact]
    public void SoftDelete_Should_Be_Idempotent()
    {
        var entity = new TestAuditableSoftDeleteEntity(Guid.NewGuid());
        entity.SoftDelete("admin");
        var deletedAt1 = entity.DeletedAt;

        entity.SoftDelete("other");
        Assert.Equal("admin", entity.DeletedBy); // not overwritten
    }

    [Fact]
    public void Should_Implement_ISoftDeletable()
    {
        ISoftDeletable entity = new TestAuditableSoftDeleteEntity(Guid.NewGuid());
        Assert.False(entity.IsDeleted);
    }

    [Fact]
    public void Should_Implement_IAuditable()
    {
        IAuditable entity = new TestAuditableSoftDeleteEntity(Guid.NewGuid());
        Assert.NotNull(entity);
    }
}
