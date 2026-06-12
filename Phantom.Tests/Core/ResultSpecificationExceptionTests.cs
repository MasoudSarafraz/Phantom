using Phantom.Core.Results;
using Phantom.Core.Specifications;
using Phantom.Core.Exceptions;
using Phantom.Core.Events;
using System.Linq.Expressions;

namespace Phantom.Tests.Core;


public class ResultTests
{
    [Fact]
    public void Success_Should_Create_Successful_Result()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Failure_Should_Create_Failed_Result()
    {
        var result = Result.Failure("Something went wrong");

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Equal("Something went wrong", result.Error);
    }

    [Fact]
    public void Match_On_Success_Should_Execute_OnSuccess()
    {
        var result = Result.Success();
        var output = result.Match(
            onSuccess: () => "ok",
            onFailure: err => $"err: {err}"
        );

        Assert.Equal("ok", output);
    }

    [Fact]
    public void Match_On_Failure_Should_Execute_OnFailure()
    {
        var result = Result.Failure("bad");
        var output = result.Match(
            onSuccess: () => "ok",
            onFailure: err => $"err: {err}"
        );

        Assert.Equal("err: bad", output);
    }
}


public class ResultTTests
{
    [Fact]
    public void Success_Should_Create_Successful_Result_With_Value()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_Should_Create_Failed_Result()
    {
        var result = Result<int>.Failure("error");

        Assert.True(result.IsFailure);
        Assert.Equal("error", result.Error);
    }

    [Fact]
    public void Accessing_Value_On_Failure_Should_Throw()
    {
        var result = Result<int>.Failure("error");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Map_Should_Transform_Success_Value()
    {
        var result = Result<int>.Success(5);
        var mapped = result.Map(x => x * 2);

        Assert.True(mapped.IsSuccess);
        Assert.Equal(10, mapped.Value);
    }

    [Fact]
    public void Map_On_Failure_Should_Passthrough()
    {
        var result = Result<int>.Failure("err");
        var mapped = result.Map(x => x * 2);

        Assert.True(mapped.IsFailure);
        Assert.Equal("err", mapped.Error);
    }

    [Fact]
    public void Bind_Should_Chain_Successful_Results()
    {
        var result = Result<int>.Success(5);
        var bound = result.Bind(x => Result<string>.Success($"val:{x}"));

        Assert.True(bound.IsSuccess);
        Assert.Equal("val:5", bound.Value);
    }

    [Fact]
    public void Bind_On_Failure_Should_Passthrough()
    {
        var result = Result<int>.Failure("err");
        var bound = result.Bind(x => Result<string>.Success($"val:{x}"));

        Assert.True(bound.IsFailure);
        Assert.Equal("err", bound.Error);
    }

    [Fact]
    public void Bind_Should_Propagate_Inner_Failure()
    {
        var result = Result<int>.Success(5);
        var bound = result.Bind(x => Result<string>.Failure("inner err"));

        Assert.True(bound.IsFailure);
        Assert.Equal("inner err", bound.Error);
    }

    [Fact]
    public void Match_Should_Pattern_Match()
    {
        var success = Result<int>.Success(10);
        Assert.Equal("10", success.Match(v => v.ToString(), e => e));

        var failure = Result<int>.Failure("bad");
        Assert.Equal("bad", failure.Match(v => v.ToString(), e => e));
    }

    [Fact]
    public void Result_SuccessT_Factory_Should_Work()
    {
        var result = Result.Success(42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Result_FailureT_Factory_Should_Work()
    {
        var result = Result.Failure<int>("err");
        Assert.True(result.IsFailure);
    }
}


public class ActiveUserSpec : Specification<string>
{
    public override bool IsSatisfiedBy(string candidate) => candidate.StartsWith("active:");

    public override Expression<Func<string, bool>> ToExpression() => s => s.StartsWith("active:");
}

public class PremiumUserSpec : Specification<string>
{
    public override bool IsSatisfiedBy(string candidate) => candidate.Contains("premium");

    public override Expression<Func<string, bool>> ToExpression() => s => s.Contains("premium");
}

public class SpecificationTests
{
    [Fact]
    public void IsSatisfiedBy_Should_Work()
    {
        var spec = new ActiveUserSpec();
        Assert.True(spec.IsSatisfiedBy("active:john"));
        Assert.False(spec.IsSatisfiedBy("inactive:john"));
    }

    [Fact]
    public void And_Should_Require_Both()
    {
        var spec = new ActiveUserSpec().And(new PremiumUserSpec());

        Assert.True(spec.IsSatisfiedBy("active:premium_user"));
        Assert.False(spec.IsSatisfiedBy("active:normal_user"));
        Assert.False(spec.IsSatisfiedBy("inactive:premium_user"));
    }

    [Fact]
    public void Or_Should_Require_Either()
    {
        var spec = new ActiveUserSpec().Or(new PremiumUserSpec());

        Assert.True(spec.IsSatisfiedBy("active:normal_user"));
        Assert.True(spec.IsSatisfiedBy("inactive:premium_user"));
        Assert.False(spec.IsSatisfiedBy("inactive:normal_user"));
    }

    [Fact]
    public void Not_Should_Negate()
    {
        var spec = new ActiveUserSpec().Not();

        Assert.False(spec.IsSatisfiedBy("active:john"));
        Assert.True(spec.IsSatisfiedBy("inactive:john"));
    }

    [Fact]
    public void And_With_Null_Should_Throw()
    {
        var spec = new ActiveUserSpec();
        Assert.Throws<ArgumentNullException>(() => spec.And(null!));
    }

    [Fact]
    public void Or_With_Null_Should_Throw()
    {
        var spec = new ActiveUserSpec();
        Assert.Throws<ArgumentNullException>(() => spec.Or(null!));
    }

    [Fact]
    public void ToExpression_Should_Return_Expression()
    {
        var spec = new ActiveUserSpec();
        var expr = spec.ToExpression();

        Assert.NotNull(expr);
        Assert.NotNull(expr.Compile());

        var compiled = expr.Compile();
        Assert.True(compiled("active:test"));
        Assert.False(compiled("inactive:test"));
    }

    [Fact]
    public void And_ToExpression_Should_Combine()
    {
        var spec = new ActiveUserSpec().And(new PremiumUserSpec());
        var expr = spec.ToExpression();
        var compiled = expr.Compile();

        Assert.True(compiled("active:premium"));
        Assert.False(compiled("active:normal"));
    }

    [Fact]
    public void Or_ToExpression_Should_Combine()
    {
        var spec = new ActiveUserSpec().Or(new PremiumUserSpec());
        var expr = spec.ToExpression();
        var compiled = expr.Compile();

        Assert.True(compiled("active:normal"));
        Assert.True(compiled("inactive:premium"));
        Assert.False(compiled("inactive:normal"));
    }

    [Fact]
    public void Not_ToExpression_Should_Negate()
    {
        var spec = new ActiveUserSpec().Not();
        var expr = spec.ToExpression();
        var compiled = expr.Compile();

        Assert.False(compiled("active:test"));
        Assert.True(compiled("inactive:test"));
    }
}


public class ExceptionTests
{
    [Fact]
    public void NotFoundException_Should_Store_EntityInfo()
    {
        var ex = new NotFoundException("User", 42);

        Assert.Equal("User", ex.EntityName);
        Assert.Equal(42, ex.EntityId);
        Assert.Contains("User", ex.Message);
        Assert.Contains("42", ex.Message);
    }

    [Fact]
    public void NotFoundException_Null_EntityName_Should_Throw()
    {
        Assert.Throws<ArgumentException>(() => new NotFoundException("", 42));
    }

    [Fact]
    public void ConcurrencyException_Should_Store_VersionInfo()
    {
        var ex = new ConcurrencyException("Order", 1, expectedVersion: 3, actualVersion: 5);

        Assert.Equal("Order", ex.EntityName);
        Assert.Equal(3, ex.ExpectedVersion);
        Assert.Equal(5, ex.ActualVersion);
    }

    [Fact]
    public void BusinessRuleException_With_Code()
    {
        var ex = new BusinessRuleException("RULE001", "Cannot delete active order");

        Assert.Equal("RULE001", ex.Code);
        Assert.Equal("Cannot delete active order", ex.Message);
    }

    [Fact]
    public void DomainException_Is_Base()
    {
        var ex = new DomainException("domain error");
        Assert.Equal("domain error", ex.Message);
    }
}


public class TestIntegrationEvent : IntegrationEvent
{
    public string Payload { get; }
    public TestIntegrationEvent(string payload) { Payload = payload; }
}

public class EventTests
{
    [Fact]
    public void DomainEvent_Should_Have_EventId_And_OccurredOn()
    {
        var evt = new TestDomainEvent("test");

        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.NotEqual(default, evt.OccurredOn);
    }

    [Fact]
    public void IntegrationEvent_Should_Have_Required_Properties()
    {
        var evt = new TestIntegrationEvent("data");

        Assert.NotEqual(Guid.Empty, evt.EventId);
        Assert.NotEqual(default, evt.OccurredOn);
        Assert.Equal(nameof(TestIntegrationEvent), evt.EventName);
        Assert.Null(evt.CorrelationId);
    }

    [Fact]
    public void IntegrationEvent_CorrelationId_Can_Be_Set()
    {
        var evt = new TestIntegrationEvent("data") { CorrelationId = "corr-123" };

        Assert.Equal("corr-123", evt.CorrelationId);
    }
}
