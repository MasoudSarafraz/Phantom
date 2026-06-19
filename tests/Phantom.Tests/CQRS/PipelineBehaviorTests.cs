using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Dispatchers;
using Phantom.CQRS.Exceptions;
using Phantom.CQRS.Extensions;
using Phantom.CQRS.Pipelines;
using Phantom.CQRS.Queries;
using Phantom.Core.Services;

namespace Phantom.Tests.CQRS;

public record PipelinesPingCommand(string Message) : ICommand;

public record PipelinesPingResult(string Echo) : ICommand<string>;

public record PipelinesGreetingQuery(string Name) : IQuery<string>;

public class PipelinesPingHandler : ICommandHandler<PipelinesPingCommand>
{
    public static int Invocations;
    public Task HandleAsync(PipelinesPingCommand command, CancellationToken ct = default)
    {
        Interlocked.Increment(ref Invocations);
        return Task.CompletedTask;
    }
}

public class PipelinesPingResultHandler : ICommandHandler<PipelinesPingResult, string>
{
    public Task<string> HandleAsync(PipelinesPingResult command, CancellationToken ct = default)
        => Task.FromResult(command.Echo);
}

public class PipelinesGreetingHandler : IQueryHandler<PipelinesGreetingQuery, string>
{
    public Task<string> HandleAsync(PipelinesGreetingQuery query, CancellationToken ct = default)
        => Task.FromResult($"Hello {query.Name}");
}

public record PipelinesValidatedCommand(string Email, int Age) : ICommand;

public class PipelinesValidatedCommandValidator : AbstractValidator<PipelinesValidatedCommand>
{
    public PipelinesValidatedCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Age).InclusiveBetween(18, 120);
    }
}

public class PipelinesValidatedHandler : ICommandHandler<PipelinesValidatedCommand>
{
    public Task HandleAsync(PipelinesValidatedCommand command, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class UnitOfWorkPipelineBehaviorTests
{
    private class CountingUnitOfWork : IUnitOfWork
    {
        public int SaveCount;
        public int BeginCount;
        public int CommitCount;
        public int RollbackCount;

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        { SaveCount++; return Task.FromResult(1); }

        public Task BeginTransactionAsync(CancellationToken ct = default)
        { BeginCount++; return Task.CompletedTask; }

        public Task CommitAsync(CancellationToken ct = default)
        { CommitCount++; return Task.CompletedTask; }

        public Task RollbackAsync(CancellationToken ct = default)
        { RollbackCount++; return Task.CompletedTask; }

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static UnitOfWorkPipelineBehavior<T> CreateBehavior<T>(IUnitOfWork uow) where T : notnull
        => new(uow);

    [Fact]
    public async Task Should_Call_SaveChangesAsync_After_Handler_Completes()
    {
        var uow = new CountingUnitOfWork();
        var behavior = CreateBehavior<PipelinesPingCommand>(uow);

        var invoked = false;
        await behavior.HandleAsync(
            new PipelinesPingCommand("hi"),
            () => { invoked = true; return Task.CompletedTask; },
            CancellationToken.None);

        Assert.True(invoked);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task Should_Not_Call_SaveChanges_If_Next_Throws()
    {
        var uow = new CountingUnitOfWork();
        var behavior = CreateBehavior<PipelinesPingCommand>(uow);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.HandleAsync(
                new PipelinesPingCommand("hi"),
                () => throw new InvalidOperationException("boom"),
                CancellationToken.None));

        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public void Should_Implement_IPipelineBehavior()
    {
        Assert.True(typeof(IPipelineBehavior<PipelinesPingCommand>).IsAssignableFrom(
            typeof(UnitOfWorkPipelineBehavior<PipelinesPingCommand>)));
    }
}

public class LoggingPipelineBehaviorTests
{
    [Fact]
    public async Task Should_Invoke_Next_And_Return_Normally()
    {
        var logger = new LoggerFactory().CreateLogger<LoggingPipelineBehavior<PipelinesPingCommand>>();
        var behavior = new LoggingPipelineBehavior<PipelinesPingCommand>(logger);

        var invoked = false;
        await behavior.HandleAsync(
            new PipelinesPingCommand("test"),
            () => { invoked = true; return Task.CompletedTask; },
            CancellationToken.None);

        Assert.True(invoked);
    }

    [Fact]
    public async Task Should_Propagate_Exception_From_Next()
    {
        var logger = new LoggerFactory().CreateLogger<LoggingPipelineBehavior<PipelinesPingCommand>>();
        var behavior = new LoggingPipelineBehavior<PipelinesPingCommand>(logger);

        await Assert.ThrowsAsync<ApplicationException>(async () =>
            await behavior.HandleAsync(
                new PipelinesPingCommand("test"),
                () => throw new ApplicationException("handler failed"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Should_Rethrow_OperationCanceled_When_Token_Already_Canceled()
    {
        var logger = new LoggerFactory().CreateLogger<LoggingPipelineBehavior<PipelinesPingCommand>>();
        var behavior = new LoggingPipelineBehavior<PipelinesPingCommand>(logger);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await behavior.HandleAsync(
                new PipelinesPingCommand("test"),
                () => throw new OperationCanceledException(cts.Token),
                cts.Token));
    }

    [Fact]
    public void Should_Implement_IPipelineBehavior()
    {
        Assert.True(typeof(IPipelineBehavior<PipelinesPingCommand>).IsAssignableFrom(
            typeof(LoggingPipelineBehavior<PipelinesPingCommand>)));
    }
}

public class ValidationPipelineBehaviorTests
{
    [Fact]
    public async Task Valid_Command_Should_Invoke_Next()
    {
        var behavior = new ValidationPipelineBehavior<PipelinesValidatedCommand>(
            new[] { new PipelinesValidatedCommandValidator() });

        var invoked = false;
        await behavior.HandleAsync(
            new PipelinesValidatedCommand("user@example.com", 30),
            () => { invoked = true; return Task.CompletedTask; },
            CancellationToken.None);

        Assert.True(invoked);
    }

    [Fact]
    public async Task Invalid_Command_Should_Throw_ValidationException_Without_Invoking_Next()
    {
        var behavior = new ValidationPipelineBehavior<PipelinesValidatedCommand>(
            new[] { new PipelinesValidatedCommandValidator() });

        var invoked = false;
        await Assert.ThrowsAsync<ValidationException>(async () =>
            await behavior.HandleAsync(
                new PipelinesValidatedCommand("not-an-email", 5),
                () => { invoked = true; return Task.CompletedTask; },
                CancellationToken.None));

        Assert.False(invoked);
    }

    [Fact]
    public async Task Multiple_Validators_Should_Aggregate_Errors()
    {
        var secondValidator = new InlineValidator<PipelinesValidatedCommand>();
        secondValidator.RuleFor(x => x.Email).NotEqual("forbidden@example.com").WithMessage("Forbidden email");

        IValidator<PipelinesValidatedCommand>[] validators = new[]
        {
            (IValidator<PipelinesValidatedCommand>)new PipelinesValidatedCommandValidator(),
            secondValidator
        };
        var behavior = new ValidationPipelineBehavior<PipelinesValidatedCommand>(validators);

        var ex = await Assert.ThrowsAsync<ValidationException>(async () =>
            await behavior.HandleAsync(
                new PipelinesValidatedCommand("forbidden@example.com", 5),
                () => Task.CompletedTask,
                CancellationToken.None));

        Assert.True(ex.Errors.Count() >= 2);
    }

    [Fact]
    public async Task No_Validators_Should_Invoke_Next()
    {
        var behavior = new ValidationPipelineBehavior<PipelinesValidatedCommand>(
            Array.Empty<IValidator<PipelinesValidatedCommand>>());

        var invoked = false;
        await behavior.HandleAsync(
            new PipelinesValidatedCommand("anything", 1),
            () => { invoked = true; return Task.CompletedTask; },
            CancellationToken.None);

        Assert.True(invoked);
    }
}

public class HandlerNotFoundExceptionTests
{
    [Fact]
    public void Default_Constructor_Should_Store_RequestType()
    {
        var ex = new HandlerNotFoundException(typeof(PipelinesPingCommand));
        Assert.Equal(typeof(PipelinesPingCommand), ex.RequestType);
        Assert.Contains("PipelinesPingCommand", ex.Message);
    }

    [Fact]
    public void With_Message_Should_Store_RequestType_And_Message()
    {
        var ex = new HandlerNotFoundException(typeof(PipelinesPingCommand), "custom msg");
        Assert.Equal(typeof(PipelinesPingCommand), ex.RequestType);
        Assert.Equal("custom msg", ex.Message);
    }

    [Fact]
    public void With_Inner_Exception_Should_Store_All()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new HandlerNotFoundException(typeof(PipelinesPingCommand), "outer", inner);
        Assert.Equal(typeof(PipelinesPingCommand), ex.RequestType);
        Assert.Equal(inner, ex.InnerException);
    }

    [Fact]
    public async Task Dispatcher_QueryAsync_With_Unregistered_Query_Should_Throw_HandlerNotFound()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IDispatcher, Dispatcher>();
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        await Assert.ThrowsAsync<HandlerNotFoundException>(() =>
            dispatcher.QueryAsync<string>(new PipelinesGreetingQuery("x")));
    }

    [Fact]
    public async Task Dispatcher_SendAsync_CommandWithResult_Unregistered_Should_Throw_HandlerNotFound()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IDispatcher, Dispatcher>();
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        await Assert.ThrowsAsync<HandlerNotFoundException>(() =>
            dispatcher.SendAsync<string>(new PipelinesPingResult("echo")));
    }
}

public class CommandAndQueryInterfaceTests
{
    [Fact]
    public void ICommand_Should_Be_Marker_Interface()
    {
        Assert.True(typeof(ICommand).IsAssignableFrom(typeof(PipelinesPingCommand)));
        Assert.True(typeof(ICommand).IsAssignableFrom(typeof(PipelinesValidatedCommand)));
    }

    [Fact]
    public void ICommand_TResult_Should_Be_Marker_Interface()
    {
        Assert.True(typeof(ICommand<string>).IsAssignableFrom(typeof(PipelinesPingResult)));
    }

    [Fact]
    public void IQuery_TResult_Should_Be_Marker_Interface()
    {
        Assert.True(typeof(IQuery<string>).IsAssignableFrom(typeof(PipelinesGreetingQuery)));
    }

    [Fact]
    public void ICommandHandler_Should_Be_Marker_Interface()
    {
        Assert.True(typeof(ICommandHandler<PipelinesPingCommand>).IsAssignableFrom(typeof(PipelinesPingHandler)));
    }

    [Fact]
    public void ICommandHandler_TResult_Should_Be_Marker_Interface()
    {
        Assert.True(typeof(ICommandHandler<PipelinesPingResult, string>).IsAssignableFrom(typeof(PipelinesPingResultHandler)));
    }

    [Fact]
    public void IQueryHandler_Should_Be_Marker_Interface()
    {
        Assert.True(typeof(IQueryHandler<PipelinesGreetingQuery, string>).IsAssignableFrom(typeof(PipelinesGreetingHandler)));
    }
}

public class CQRSServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPhantomCQRS_Should_Register_Dispatcher_And_Handlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantomCQRS(typeof(PipelinesPingHandler).Assembly);

        var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetService<IDispatcher>());
        Assert.NotNull(sp.GetService<ICommandHandler<PipelinesPingCommand>>());
        Assert.NotNull(sp.GetService<ICommandHandler<PipelinesPingResult, string>>());
        Assert.NotNull(sp.GetService<IQueryHandler<PipelinesGreetingQuery, string>>());
    }

    [Fact]
    public void AddPhantomCQRS_With_Null_Assemblies_Should_Throw()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => services.AddPhantomCQRS(null!));
    }

    [Fact]
    public void AddPhantomCQRS_Should_Register_LoggingPipelineBehavior()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantomCQRS(typeof(PipelinesPingHandler).Assembly);
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<IPipelineBehavior<PipelinesPingCommand>>());
    }

    [Fact]
    public void AddPhantomValidation_Should_Register_ValidationPipelineBehavior()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantomValidation();
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<IPipelineBehavior<PipelinesValidatedCommand>>());
    }

    [Fact]
    public void AddPhantomCQRS_With_Null_Assembly_In_Array_Should_Skip_It()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantomCQRS(typeof(PipelinesPingHandler).Assembly, null!);
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetService<IDispatcher>());
    }
}
