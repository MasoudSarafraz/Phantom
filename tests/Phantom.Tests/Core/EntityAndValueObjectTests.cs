using Phantom.Core.Domain;
using Phantom.Core.Events;

namespace Phantom.Tests.Core;


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

        Assert.NotEqual(e1, e2);
    }

    [Fact]
    public void Same_Transient_Entity_Reference_Should_Be_Equal()
    {
        var e1 = new TestEntity();
        Assert.Equal(e1, e1);
    }

    [Fact]
    public void Transient_Entities_Should_Have_Stable_HashCode_By_Reference()
    {
        // Regression test: previously, two transient entities returned HashCode.Combine(GetType(), default!)
        // which collided in Dictionary/HashSet. Now they fall back to RuntimeHelpers.GetHashCode(this).
        var e1 = new TestEntity();
        var e2 = new TestEntity();

        // Reference identity hashcode should be stable across calls.
        Assert.Equal(e1.GetHashCode(), e1.GetHashCode());
        Assert.Equal(e2.GetHashCode(), e2.GetHashCode());

        // Two distinct transient entities should have (with overwhelming probability) distinct hashes.
        Assert.NotEqual(e1.GetHashCode(), e2.GetHashCode());
    }

    [Fact]
    public void HashSet_Should_Distinguish_Distinct_Transient_Entities()
    {
        // Regression test: a HashSet would previously collapse distinct transient entities into one slot.
        var set = new HashSet<TestEntity>();
        var e1 = new TestEntity();
        var e2 = new TestEntity();

        Assert.True(set.Add(e1));
        Assert.True(set.Add(e2));
        Assert.Equal(2, set.Count);
    }

    [Fact]
    public void Persisted_Entities_Should_Hash_By_Id_And_Type()
    {
        var id = Guid.NewGuid();
        var e1 = new TestEntity(id);
        var e2 = new TestEntity(id);

        Assert.Equal(e1.GetHashCode(), e2.GetHashCode());
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
        var m1 = new Money(5, "USD");
        var m2 = new Money(5, "USD");

        Assert.Equal(m1.GetHashCode(), m2.GetHashCode());
        Assert.NotEqual(0, m1.GetHashCode());
    }

    [Fact]
    public void ValueObject_IEquatable_Should_Work()
    {
        ValueObject m1 = new Money(100, "USD");
        ValueObject m2 = new Money(100, "USD");

        Assert.True(m1.Equals(m2));
    }
}


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

    [Fact]
    public void IAggregateRoot_Interface_Should_Not_Expose_ClearDomainEvents()
    {
        // The public IAggregateRoot contract must NOT include ClearDomainEvents.
        // This is an infrastructure concern that should only be invoked through
        // the internal IAggregateRootPersistence pathway.
        var interfaceType = typeof(IAggregateRoot);
        var method = interfaceType.GetMethod(nameof(AggregateRoot<Guid>.ClearDomainEvents));

        Assert.Null(method);
    }
}


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

        entity.SoftDelete();
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
        entity.Restore();
        Assert.False(entity.IsDeleted);
    }

    [Fact]
    public void SoftDeleteEntity_Should_Implement_ISoftDeletable()
    {
        ISoftDeletable entity = new TestSoftDeleteEntity(Guid.NewGuid());
        Assert.False(entity.IsDeleted);
    }
}


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

        entity.SetCreated("user2");
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


public class TestAuditableSoftDeleteEntity : AuditableSoftDeleteEntity<Guid>, IAuditable
{
    public TestAuditableSoftDeleteEntity(Guid id) : base(id) { }
    public TestAuditableSoftDeleteEntity() { }
}


public class TestAuditableAggregateRoot : AuditableAggregateRoot<Guid>
{
    public TestAuditableAggregateRoot(Guid id) : base(id) { }
    public TestAuditableAggregateRoot() { }

    public void DoWork(string data) => AddDomainEvent(new TestDomainEvent(data));
}

public class TestSoftDeleteAggregateRoot : SoftDeleteAggregateRoot<Guid>
{
    public TestSoftDeleteAggregateRoot(Guid id) : base(id) { }
    public TestSoftDeleteAggregateRoot() { }
}

public class TestAuditableSoftDeleteAggregateRoot : AuditableSoftDeleteAggregateRoot<Guid>
{
    public TestAuditableSoftDeleteAggregateRoot(Guid id) : base(id) { }
    public TestAuditableSoftDeleteAggregateRoot() { }

    public void DoWork(string data) => AddDomainEvent(new TestDomainEvent(data));
}


public class AuditableAggregateRootTests
{
    [Fact]
    public void Should_Inherit_From_AggregateRoot()
    {
        var ar = new TestAuditableAggregateRoot(Guid.NewGuid());
        Assert.IsAssignableFrom<AggregateRoot<Guid>>(ar);
        Assert.IsAssignableFrom<IAggregateRoot>(ar);
    }

    [Fact]
    public void Should_Implement_IAuditable()
    {
        IAuditable ar = new TestAuditableAggregateRoot(Guid.NewGuid());
        Assert.NotNull(ar);
    }

    [Fact]
    public void Should_Raise_Domain_Events_Like_AggregateRoot()
    {
        var ar = new TestAuditableAggregateRoot(Guid.NewGuid());
        ar.DoWork("audit-event");
        Assert.Single(ar.DomainEvents);
    }

    [Fact]
    public void SetCreated_Should_Populate_CreatedAt_And_CreatedBy()
    {
        var ar = new TestAuditableAggregateRoot(Guid.NewGuid());
        ar.SetCreated("user1");

        Assert.NotEqual(default, ar.CreatedAt);
        Assert.Equal("user1", ar.CreatedBy);
    }

    [Fact]
    public void SetUpdated_Should_Populate_UpdatedAt_And_UpdatedBy()
    {
        var ar = new TestAuditableAggregateRoot(Guid.NewGuid());
        ar.SetUpdated("user2");

        Assert.NotNull(ar.UpdatedAt);
        Assert.Equal("user2", ar.UpdatedBy);
    }
}


public class SoftDeleteAggregateRootTests
{
    [Fact]
    public void Should_Inherit_From_AggregateRoot()
    {
        var ar = new TestSoftDeleteAggregateRoot(Guid.NewGuid());
        Assert.IsAssignableFrom<AggregateRoot<Guid>>(ar);
    }

    [Fact]
    public void Should_Implement_ISoftDeletable()
    {
        ISoftDeletable ar = new TestSoftDeleteAggregateRoot(Guid.NewGuid());
        Assert.False(ar.IsDeleted);
    }

    [Fact]
    public void SoftDelete_Should_Set_IsDeleted_True()
    {
        var ar = new TestSoftDeleteAggregateRoot(Guid.NewGuid());
        ar.SoftDelete();

        Assert.True(ar.IsDeleted);
        Assert.NotNull(ar.DeletedAt);
    }

    [Fact]
    public void Restore_Should_Set_IsDeleted_False()
    {
        var ar = new TestSoftDeleteAggregateRoot(Guid.NewGuid());
        ar.SoftDelete();
        ar.Restore();

        Assert.False(ar.IsDeleted);
        Assert.Null(ar.DeletedAt);
    }
}


public class AuditableSoftDeleteAggregateRootTests
{
    [Fact]
    public void Should_Inherit_From_AggregateRoot()
    {
        var ar = new TestAuditableSoftDeleteAggregateRoot(Guid.NewGuid());
        Assert.IsAssignableFrom<AggregateRoot<Guid>>(ar);
    }

    [Fact]
    public void Should_Implement_Both_IAuditable_And_ISoftDeletable()
    {
        var ar = new TestAuditableSoftDeleteAggregateRoot(Guid.NewGuid());
        Assert.IsAssignableFrom<IAuditable>(ar);
        Assert.IsAssignableFrom<ISoftDeletable>(ar);
    }

    [Fact]
    public void SoftDelete_With_User_Should_Set_DeletedBy()
    {
        var ar = new TestAuditableSoftDeleteAggregateRoot(Guid.NewGuid());
        ar.SoftDelete("admin");

        Assert.True(ar.IsDeleted);
        Assert.Equal("admin", ar.DeletedBy);
    }

    [Fact]
    public void SetCreated_Should_Work_On_AuditableSoftDelete_AggregateRoot()
    {
        var ar = new TestAuditableSoftDeleteAggregateRoot(Guid.NewGuid());
        ar.SetCreated("creator");

        Assert.Equal("creator", ar.CreatedBy);
        Assert.NotEqual(default, ar.CreatedAt);
    }

    [Fact]
    public void Should_Raise_Domain_Events()
    {
        // Verify that being auditable + soft-deletable does not break the core aggregate behavior.
        var ar = new TestAuditableSoftDeleteAggregateRoot(Guid.NewGuid());
        ar.DoWork("audit-softdelete-event");
        Assert.Single(ar.DomainEvents);
    }
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
        Assert.Equal("admin", entity.DeletedBy);
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
