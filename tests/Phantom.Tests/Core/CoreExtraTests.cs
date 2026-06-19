using Phantom.Core.Domain;
using Phantom.Core.Events;
using Phantom.Core.Exceptions;
using Phantom.Core.Results;
using Phantom.Core.Specifications;
using System.Linq.Expressions;

namespace Phantom.Tests.Core;

public class ResultAdvancedTests
{
    [Fact]
    public void Constructor_Success_With_NonEmpty_Error_Should_Throw()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var r = new TestableResult(true, "should not have error on success");
        });
    }

    [Fact]
    public void Constructor_Failure_With_Empty_Error_Should_Throw()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var r = new TestableResult(false, "");
        });
    }

    [Fact]
    public void Constructor_Failure_With_Null_Error_Should_Throw()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            var r = new TestableResult(false, null!);
        });
    }

    private class TestableResult : Result
    {
        public TestableResult(bool isSuccess, string error) : base(isSuccess, error) { }
    }

    [Fact]
    public void Map_On_Success_Should_Invoke_OnSuccess()
    {
        var result = Result.Success();
        var chained = result.Map(() => Result.Success());
        Assert.True(chained.IsSuccess);
    }

    [Fact]
    public void Map_On_Failure_Should_Passthrough_Error()
    {
        var result = Result.Failure("err");
        var chained = result.Map(() => Result.Success());
        Assert.True(chained.IsFailure);
        Assert.Equal("err", chained.Error);
    }

    [Fact]
    public void Match_On_Success_Should_Invoke_OnSuccess()
    {
        var result = Result.Success();
        var output = result.Match(onSuccess: () => 42, onFailure: _ => -1);
        Assert.Equal(42, output);
    }

    [Fact]
    public void Match_On_Failure_Should_Invoke_OnFailure_With_Error()
    {
        var result = Result.Failure("bad");
        var output = result.Match(onSuccess: () => 42, onFailure: err => err.Length);
        Assert.Equal(3, output);
    }

    [Fact]
    public void ThrowIfFailure_On_Success_Should_Return_Same_Instance()
    {
        var result = Result.Success();
        Assert.Same(result, result.ThrowIfFailure());
    }
}

public class ResultTAdvancedTests
{
    [Fact]
    public void ThrowIfFailure_On_Failure_T_Should_Throw_BusinessRuleException()
    {
        var result = Result<int>.Failure("INVARIANT_X");
        var ex = Assert.Throws<BusinessRuleException>(() => result.ThrowIfFailure());
        Assert.Contains("INVARIANT_X", ex.Message);
    }

    [Fact]
    public void ThrowIfFailure_On_Success_T_Should_Return_Same_Instance()
    {
        var result = Result<int>.Success(7);
        Assert.Same(result, result.ThrowIfFailure());
    }

    [Fact]
    public void Bind_Chain_Should_Compose()
    {
        var result = Result<int>.Success(5)
            .Bind(x => Result<int>.Success(x * 2))
            .Bind(x => Result<string>.Success($"val:{x}"));

        Assert.True(result.IsSuccess);
        Assert.Equal("val:10", result.Value);
    }

    [Fact]
    public void Map_Chain_Should_Compose()
    {
        var result = Result<int>.Success(5)
            .Map(x => x + 1)
            .Map(x => x * 10);

        Assert.True(result.IsSuccess);
        Assert.Equal(60, result.Value);
    }

    [Fact]
    public void Accessing_Value_On_Success_Should_Return_Value()
    {
        var result = Result<string>.Success("hello");
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Failure_T_Should_Have_Default_Value_Internally()
    {
        var result = Result<int>.Failure("err");
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}

public class SpecificationCompositionTests
{
    private class EvenSpec : Specification<int>
    {
        public override bool IsSatisfiedBy(int candidate) => candidate % 2 == 0;
        public override Expression<Func<int, bool>> ToExpression() => n => n % 2 == 0;
    }

    private class PositiveSpec : Specification<int>
    {
        public override bool IsSatisfiedBy(int candidate) => candidate > 0;
        public override Expression<Func<int, bool>> ToExpression() => n => n > 0;
    }

    [Fact]
    public void And_Should_Be_Transitive()
    {
        var spec = new EvenSpec().And(new PositiveSpec());
        Assert.True(spec.IsSatisfiedBy(4));
        Assert.False(spec.IsSatisfiedBy(3));
        Assert.False(spec.IsSatisfiedBy(-2));
        Assert.False(spec.IsSatisfiedBy(0));
    }

    [Fact]
    public void Or_Should_Be_Transitive()
    {
        var spec = new EvenSpec().Or(new PositiveSpec());
        Assert.True(spec.IsSatisfiedBy(4));
        Assert.True(spec.IsSatisfiedBy(3));
        Assert.True(spec.IsSatisfiedBy(-2));
        Assert.False(spec.IsSatisfiedBy(-3));
    }

    [Fact]
    public void Not_Should_Negate_Correctly()
    {
        var spec = new EvenSpec().Not();
        Assert.False(spec.IsSatisfiedBy(4));
        Assert.True(spec.IsSatisfiedBy(3));
    }

    [Fact]
    public void And_Then_Not_Should_De_Morgan_With_Or()
    {
        var a = new EvenSpec();
        var b = new PositiveSpec();

        var notAnd = a.And(b).Not();
        var orNots = a.Not().Or(b.Not());

        for (int i = -10; i <= 10; i++)
        {
            Assert.Equal(notAnd.IsSatisfiedBy(i), orNots.IsSatisfiedBy(i));
        }
    }

    [Fact]
    public void ToExpression_Of_Combined_Spec_Should_Work_After_Compile()
    {
        var spec = new EvenSpec().And(new PositiveSpec()).Not();
        var compiled = spec.ToExpression().Compile();

        Assert.True(compiled(3));
        Assert.False(compiled(4));
        Assert.True(compiled(-3));
        Assert.True(compiled(-4));
    }
}

public class ExceptionAdvancedTests
{
    [Fact]
    public void ConcurrencyException_Should_Throw_For_Null_EntityId()
    {
        Assert.Throws<ArgumentNullException>(() => new ConcurrencyException("Order", null!));
    }

    [Fact]
    public void ConcurrencyException_Should_Throw_For_Empty_EntityName()
    {
        Assert.Throws<ArgumentException>(() => new ConcurrencyException("", 1));
    }

    [Fact]
    public void ConcurrencyException_Without_Versions_Should_Allow_Null_Versions()
    {
        var ex = new ConcurrencyException("Order", 42);
        Assert.Null(ex.ExpectedVersion);
        Assert.Null(ex.ActualVersion);
        Assert.Equal("Order", ex.EntityName);
        Assert.Equal(42, ex.EntityId);
    }

    [Fact]
    public void ConcurrencyException_Message_Should_Contain_EntityName_And_Id()
    {
        var ex = new ConcurrencyException("Order", 42);
        Assert.Contains("Order", ex.Message);
        Assert.Contains("42", ex.Message);
    }

    [Fact]
    public void ConcurrencyException_Should_Be_Available_Via_DomainException_Catch()
    {
        var ex = new ConcurrencyException("Order", 42, expectedVersion: 1, actualVersion: 2);
        DomainException domainEx = ex;
        Assert.NotNull(domainEx);
        Assert.Equal(ex.Message, domainEx.Message);
    }

    [Fact]
    public void NotFoundException_Should_Throw_For_Null_EntityId()
    {
        Assert.Throws<ArgumentNullException>(() => new NotFoundException("User", null!));
    }

    [Fact]
    public void NotFoundException_Should_Throw_For_Empty_EntityName()
    {
        Assert.Throws<ArgumentException>(() => new NotFoundException("", 1));
    }

    [Fact]
    public void NotFoundException_With_String_Id_Should_Work()
    {
        var ex = new NotFoundException("User", "abc-123");
        Assert.Equal("abc-123", ex.EntityId);
        Assert.Contains("abc-123", ex.Message);
    }

    [Fact]
    public void NotFoundException_With_Guid_Id_Should_Work()
    {
        var id = Guid.NewGuid();
        var ex = new NotFoundException("User", id);
        Assert.Equal(id, ex.EntityId);
        Assert.Contains(id.ToString(), ex.Message);
    }

    [Fact]
    public void BusinessRuleException_Without_Code_Should_Have_Null_Code()
    {
        var ex = new BusinessRuleException("msg");
        Assert.Null(ex.Code);
    }

    [Fact]
    public void BusinessRuleException_With_Inner_Exception_Should_Preserve_Inner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new BusinessRuleException("outer", inner);
        Assert.Equal(inner, ex.InnerException);
    }

    [Fact]
    public void BusinessRuleException_With_Code_And_Inner_Exception_Should_Preserve_Both()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new BusinessRuleException("CODE", "outer", inner);
        Assert.Equal("CODE", ex.Code);
        Assert.Equal(inner, ex.InnerException);
    }

    [Fact]
    public void DomainException_Should_Be_Base_Of_All_Domain_Exceptions()
    {
        Assert.True(typeof(BusinessRuleException).IsSubclassOf(typeof(DomainException)));
        Assert.True(typeof(NotFoundException).IsSubclassOf(typeof(DomainException)));
        Assert.True(typeof(ConcurrencyException).IsSubclassOf(typeof(DomainException)));
    }

    [Fact]
    public void DomainException_Should_Store_Message()
    {
        var ex = new DomainException("custom domain error");
        Assert.Equal("custom domain error", ex.Message);
    }
}

public class EventAdvancedTests
{
    [Fact]
    public void DomainEvent_EventId_Should_Be_Unique()
    {
        var e1 = new TestEventA();
        var e2 = new TestEventA();
        Assert.NotEqual(e1.EventId, e2.EventId);
    }

    [Fact]
    public void DomainEvent_OccurredOn_Should_Be_Recent()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var evt = new TestEventA();
        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.InRange(evt.OccurredOn, before, after);
    }

    [Fact]
    public void IntegrationEvent_EventName_Should_Default_To_Type_Name()
    {
        var evt = new TestIntegrationEventA();
        Assert.Equal("TestIntegrationEventA", evt.EventName);
    }

    [Fact]
    public void IntegrationEvent_CorrelationId_Should_Be_Settable()
    {
        var evt = new TestIntegrationEventA { CorrelationId = "trace-1" };
        Assert.Equal("trace-1", evt.CorrelationId);
    }

    [Fact]
    public void IntegrationEvent_EventId_Should_Be_Unique()
    {
        var e1 = new TestIntegrationEventA();
        var e2 = new TestIntegrationEventA();
        Assert.NotEqual(e1.EventId, e2.EventId);
    }

    [Fact]
    public void IntegrationEvent_Should_Implement_IIntegrationEvent()
    {
        IIntegrationEvent evt = new TestIntegrationEventA();
        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.NotEqual(default, evt.OccurredOn);
        Assert.NotNull(evt.EventName);
    }

    private class TestEventA : DomainEvent { }
    private class TestIntegrationEventA : IntegrationEvent { }
}

public class IAggregateRootInterfaceTests
{
    [Fact]
    public void IAggregateRoot_Should_Only_Expose_DomainEvents_Property()
    {
        var interfaceType = typeof(IAggregateRoot);
        var members = interfaceType.GetProperties().Select(p => p.Name).ToList();
        Assert.Single(members);
        Assert.Contains(nameof(IAggregateRoot.DomainEvents), members);
    }

    [Fact]
    public void IAggregateRoot_DomainEvents_Should_Be_IReadOnlyCollection_Of_IDomainEvent()
    {
        var prop = typeof(IAggregateRoot).GetProperty(nameof(IAggregateRoot.DomainEvents));
        Assert.NotNull(prop);
        Assert.Equal(typeof(IReadOnlyCollection<IDomainEvent>), prop!.PropertyType);
    }

    [Fact]
    public void AggregateRoot_Should_Implement_IAggregateRoot_And_IAggregateRootPersistence()
    {
        var ar = new InterfaceTestAggregateRoot(Guid.NewGuid());
        Assert.IsAssignableFrom<IAggregateRoot>(ar);
    }

    [Fact]
    public void Casting_To_IAggregateRoot_Should_Not_Expose_ClearDomainEvents()
    {
        var ar = new InterfaceTestAggregateRoot(Guid.NewGuid());
        ar.DoSomething("test");

        IAggregateRoot iar = ar;
        Assert.Single(iar.DomainEvents);

        Assert.Null(typeof(IAggregateRoot).GetMethod("ClearDomainEvents"));
    }

    private class InterfaceTestAggregateRoot : AggregateRoot<Guid>
    {
        public InterfaceTestAggregateRoot(Guid id) : base(id) { }
        public void DoSomething(string data) => AddDomainEvent(new TestDomainEventA(data));
    }

    private class TestDomainEventA : DomainEvent
    {
        public string Data { get; }
        public TestDomainEventA(string data) { Data = data; }
    }
}

public class EntityEqualityEdgeCases
{
    private class EdgeTestEntity : Entity<Guid>
    {
        public EdgeTestEntity() { }
        public EdgeTestEntity(Guid id) : base(id) { }
    }

    private class OtherEntity : Entity<Guid>
    {
        public OtherEntity(Guid id) : base(id) { }
    }

    [Fact]
    public void Equals_With_Null_Should_Be_False()
    {
        var e = new EdgeTestEntity(Guid.NewGuid());
        Assert.False(e.Equals(null));
    }

    [Fact]
    public void Equals_With_Different_Type_Should_Be_False()
    {
        var id = Guid.NewGuid();
        var a = new EdgeTestEntity(id);
        var b = new OtherEntity(id);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_With_Same_Reference_Should_Be_True()
    {
        var a = new EdgeTestEntity(Guid.NewGuid());
        Assert.True(a.Equals(a));
    }

    [Fact]
    public void Operator_Equals_With_Both_Null_Should_Be_True()
    {
        EdgeTestEntity? a = null;
        EdgeTestEntity? b = null;
        Assert.True(a == b);
    }

    [Fact]
    public void Operator_Equals_With_One_Null_Should_Be_False()
    {
        EdgeTestEntity? a = null;
        var b = new EdgeTestEntity(Guid.NewGuid());
        Assert.False(a == b);
        Assert.False(b == a);
    }

    [Fact]
    public void Operator_NotEquals_With_Different_Ids_Should_Be_True()
    {
        var a = new EdgeTestEntity(Guid.NewGuid());
        var b = new EdgeTestEntity(Guid.NewGuid());
        Assert.True(a != b);
    }

    [Fact]
    public void GetHashCode_Should_Be_Stable_Across_Calls()
    {
        var e = new EdgeTestEntity(Guid.NewGuid());
        var hash1 = e.GetHashCode();
        var hash2 = e.GetHashCode();
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void GetHashCode_For_Same_Id_Same_Type_Should_Be_Equal()
    {
        var id = Guid.NewGuid();
        var a = new EdgeTestEntity(id);
        var b = new EdgeTestEntity(id);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_Object_Overload_With_Null_Should_Be_False()
    {
        var e = new EdgeTestEntity(Guid.NewGuid());
        Assert.False(e.Equals((object?)null));
    }

    [Fact]
    public void Equals_Object_Overload_With_Unrelated_Type_Should_Be_False()
    {
        var e = new EdgeTestEntity(Guid.NewGuid());
        Assert.False(e.Equals("not an entity"));
    }
}

public class ValueObjectAdvancedTests
{
    private class Point : ValueObject
    {
        public int X { get; }
        public int Y { get; }
        public Point(int x, int y) { X = x; Y = y; }
        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return X;
            yield return Y;
        }
    }

    [Fact]
    public void Equals_With_Null_Should_Be_False()
    {
        var p = new Point(1, 2);
        Assert.False(p.Equals((object?)null));
    }

    [Fact]
    public void Equals_With_Different_Type_Should_Be_False()
    {
        var p = new Point(1, 2);
        Assert.False(p.Equals("not a point"));
    }

    [Fact]
    public void Operator_Equals_With_Both_Null_Should_Be_True()
    {
        Point? a = null;
        Point? b = null;
        Assert.True(a == b);
    }

    [Fact]
    public void Operator_Equals_With_One_Null_Should_Be_False()
    {
        Point? a = null;
        var b = new Point(1, 2);
        Assert.False(a == b);
        Assert.False(b == a);
    }

    [Fact]
    public void GetHashCode_Should_Be_Stable_Across_Calls()
    {
        var p = new Point(1, 2);
        Assert.Equal(p.GetHashCode(), p.GetHashCode());
    }

    [Fact]
    public void GetHashCode_Should_Be_Equal_For_Equal_Instances()
    {
        var a = new Point(1, 2);
        var b = new Point(1, 2);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_Should_Use_Sequence_Equality_Of_Components()
    {
        var a = new Point(1, 2);
        var b = new Point(1, 2);
        var c = new Point(2, 1);
        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
    }
}
