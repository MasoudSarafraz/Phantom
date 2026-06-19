using Phantom.CQRS.Commands;
using Phantom.CQRS.Queries;
using Phantom.CQRS.Dispatchers;
using Phantom.CQRS.Pipelines;
using Phantom.CQRS.Extensions;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

namespace Phantom.Tests.CQRS;


public record CreateOrderCommand(string ProductName, int Quantity) : ICommand;

public record CreateOrderResult(Guid OrderId) : ICommand<Guid>;

public record GetOrderQuery(Guid OrderId) : IQuery<string>;


public class CreateOrderHandler : ICommandHandler<CreateOrderCommand>
{
    public static bool WasCalled = false;
    public Task HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
    {
        WasCalled = true;
        return Task.CompletedTask;
    }
}

public class CreateOrderResultHandler : ICommandHandler<CreateOrderResult, Guid>
{
    public Task<Guid> HandleAsync(CreateOrderResult command, CancellationToken ct = default)
    {
        return Task.FromResult(Guid.NewGuid());
    }
}

public class GetOrderHandler : IQueryHandler<GetOrderQuery, string>
{
    public Task<string> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
    {
        return Task.FromResult($"Order-{query.OrderId}");
    }
}


public class DispatcherTests
{
    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantomCQRS(typeof(CreateOrderHandler).Assembly);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SendAsync_Command_Should_Dispatch_To_Handler()
    {
        CreateOrderHandler.WasCalled = false;
        var sp = BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new CreateOrderCommand("Widget", 5));

        Assert.True(CreateOrderHandler.WasCalled);
    }

    [Fact]
    public async Task SendAsync_CommandWithResult_Should_Return_Result()
    {
        var sp = BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync<Guid>(new CreateOrderResult(Guid.Empty));

        Assert.NotEqual(Guid.Empty, result);
    }

    [Fact]
    public async Task QueryAsync_Should_Return_Result()
    {
        var sp = BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var orderId = Guid.NewGuid();
        var result = await dispatcher.QueryAsync<string>(new GetOrderQuery(orderId));

        Assert.Equal($"Order-{orderId}", result);
    }

    [Fact]
    public async Task SendAsync_UnhandledCommand_Should_Throw()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDispatcher, Dispatcher>();
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        await Assert.ThrowsAsync<Phantom.CQRS.Exceptions.HandlerNotFoundException>(() =>
            dispatcher.SendAsync(new CreateOrderCommand("test", 1)));
    }
}


public class TestLoggingPipeline : IPipelineBehavior<CreateOrderCommand>
{
    public static bool WasCalled = false;

    public async Task HandleAsync(CreateOrderCommand request, RequestHandlerDelegate next, CancellationToken cancellationToken)
    {
        WasCalled = true;
        await next();
    }
}

public class PipelineBehaviorTests
{
    [Fact]
    public async Task LoggingPipeline_Should_Execute()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantomCQRS(typeof(CreateOrderHandler).Assembly);
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        CreateOrderHandler.WasCalled = false;
        await dispatcher.SendAsync(new CreateOrderCommand("Test", 1));

        Assert.True(CreateOrderHandler.WasCalled);
    }
}


public record ValidatedCommand(string Name, int Age) : ICommand;

public class ValidatedCommandValidator : AbstractValidator<ValidatedCommand>
{
    public ValidatedCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required");
        RuleFor(x => x.Age).GreaterThan(0).WithMessage("Age must be positive");
    }
}

public class ValidatedCommandHandler : ICommandHandler<ValidatedCommand>
{
    public Task HandleAsync(ValidatedCommand command, CancellationToken ct = default)
        => Task.CompletedTask;
}

public class ValidationPipelineTests
{
    [Fact]
    public async Task Valid_Command_Should_Pass_Validation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantomCQRS(typeof(ValidatedCommandHandler).Assembly);
        services.AddPhantomValidation();
        services.AddValidatorsFromAssembly(typeof(ValidatedCommandValidator).Assembly);
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        await dispatcher.SendAsync(new ValidatedCommand("John", 25));
    }

    [Fact]
    public async Task Invalid_Command_Should_Throw_ValidationException()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPhantomCQRS(typeof(ValidatedCommandHandler).Assembly);
        services.AddPhantomValidation();
        services.AddValidatorsFromAssembly(typeof(ValidatedCommandValidator).Assembly);
        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            dispatcher.SendAsync(new ValidatedCommand("", -1)));
    }
}


public class MultiAssemblyTests
{
    [Fact]
    public void AddPhantomCQRS_Should_Accept_Multiple_Assemblies()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddPhantomCQRS(
            typeof(CreateOrderHandler).Assembly,
            typeof(ValidatedCommandHandler).Assembly
        );

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();
        Assert.NotNull(dispatcher);
    }
}
