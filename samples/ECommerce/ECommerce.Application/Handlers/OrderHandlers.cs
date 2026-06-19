using ECommerce.Application.Commands;
using ECommerce.Application.Queries;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Specifications;
using ECommerce.Application.Specifications;
using ECommerce.Domain.ValueObjects;
using Phantom.Core.Exceptions;
using Phantom.Core.Services;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Queries;

namespace ECommerce.Application.Handlers;

// ─── Order Command Handlers ──────────────────────────────────

/// <summary>
/// Example: Creating an aggregate root with domain events.
/// When order.Confirm() is called, it raises OrderPlacedEvent.
/// Phantom's PhantomDbContext automatically:
///   1. Saves the outbox message in the same transaction (atomic)
///   2. OutboxProcessor publishes it to the messaging channel later
/// </summary>
public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    private readonly IRepository<Guid, Order> _orderRepository;
    private readonly IRepository<Guid, Customer> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderCommandHandler(
        IRepository<Guid, Order> orderRepository,
        IRepository<Guid, Customer> customerRepository,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByIdAsync(command.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), command.CustomerId);

        var order = new Order(Guid.NewGuid(), command.CustomerId, command.ShippingAddress);

        foreach (var line in command.Lines)
        {
            var unitPrice = new Money(line.UnitPrice, line.Currency);
            var orderLine = new OrderLine(line.ProductId, line.ProductName, line.Quantity, unitPrice);
            order.AddLine(orderLine);
        }

        // This raises OrderPlacedEvent internally — Phantom handles it via outbox
        order.Confirm();

        await _orderRepository.AddAsync(order, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return order.Id;
    }
}

public class ShipOrderCommandHandler : ICommandHandler<ShipOrderCommand>
{
    private readonly IRepository<Guid, Order> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ShipOrderCommandHandler(IRepository<Guid, Order> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(ShipOrderCommand command, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), command.OrderId);
        order.Ship(command.TrackingNumber);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public class CancelOrderCommandHandler : ICommandHandler<CancelOrderCommand>
{
    private readonly IRepository<Guid, Order> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelOrderCommandHandler(IRepository<Guid, Order> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(CancelOrderCommand command, CancellationToken cancellationToken = default)
    {
        var order = await _repository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), command.OrderId);
        order.Cancel();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

// ─── Order Query Handlers ────────────────────────────────────

/// <summary>
/// Example: Query with Specification + Include (eager loading).
/// Uses OrderWithLinesSpec to load Order + OrderLines in a single query.
/// </summary>
public class GetOrderByIdQueryHandler : IQueryHandler<GetOrderByIdQuery, OrderDto>
{
    private readonly IRepository<Guid, Order> _repository;

    public GetOrderByIdQueryHandler(IRepository<Guid, Order> repository) => _repository = repository;

    public async Task<OrderDto> HandleAsync(GetOrderByIdQuery query, CancellationToken cancellationToken = default)
    {
        // Option 1: Simple GetById (doesn't load Lines)
        // var order = await _repository.GetByIdAsync(query.OrderId, cancellationToken);

        // Option 2: Using Specification with Include — loads Lines in same query
        var spec = new OrderWithLinesSpec(query.OrderId);
        var order = (await _repository.FindAsync(spec, cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException(nameof(Order), query.OrderId);

        var total = order.CalculateTotal();
        return new OrderDto(order.Id, order.CustomerId, order.Status, order.ShippingAddress, total.Amount, total.Currency);
    }
}
