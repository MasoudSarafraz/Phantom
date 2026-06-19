using ECommerce.Application.Commands;
using ECommerce.Application.Queries;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Specifications;
using ECommerce.Application.Specifications;
using ECommerce.Domain.ValueObjects;
using Phantom.Core.Exceptions;
using Phantom.Core.Results;
using Phantom.Core.Services;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Queries;

namespace ECommerce.Application.Handlers;

// ─── Customer Command Handlers ───────────────────────────────

/// <summary>
/// Example: Command handler using Result monad for graceful error handling.
/// Instead of throwing exceptions for expected business scenarios,
/// returns Result.Success() or Result.Failure() — letting the caller decide.
/// </summary>
public class CreateCustomerCommandHandler : ICommandHandler<CreateCustomerCommand, Result<Guid>>
{
    private readonly IRepository<Guid, Customer> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerCommandHandler(IRepository<Guid, Customer> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> HandleAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        // Validate email using ValueObject — throws on invalid format
        EmailAddress email;
        try
        {
            email = new EmailAddress(command.Email);
        }
        catch (ArgumentException ex)
        {
            // Return failure instead of throwing — cleaner for expected validation errors
            return Result.Failure<Guid>(ex.Message);
        }

        var customer = new Customer(Guid.NewGuid(), command.FirstName, command.LastName, email);
        await _repository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(customer.Id);
    }
}

// ─── Customer Query Handlers ─────────────────────────────────

public class GetCustomerByIdQueryHandler : IQueryHandler<GetCustomerByIdQuery, CustomerDto>
{
    private readonly IRepository<Guid, Customer> _repository;

    public GetCustomerByIdQueryHandler(IRepository<Guid, Customer> repository) => _repository = repository;

    public async Task<CustomerDto> HandleAsync(GetCustomerByIdQuery query, CancellationToken cancellationToken = default)
    {
        var customer = await _repository.GetByIdAsync(query.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), query.CustomerId);
        return new CustomerDto(customer.Id, customer.FirstName, customer.LastName, customer.Email.Value, customer.FullName);
    }
}

/// <summary>
/// Example: List orders for a customer using Specification + Paging.
/// </summary>
public class GetCustomerOrdersQueryHandler : IQueryHandler<GetCustomerOrdersQuery, PagedResult<OrderDto>>
{
    private readonly IRepository<Guid, Order> _repository;

    public GetCustomerOrdersQueryHandler(IRepository<Guid, Order> repository) => _repository = repository;

    public async Task<PagedResult<OrderDto>> HandleAsync(GetCustomerOrdersQuery query, CancellationToken cancellationToken = default)
    {
        // Use OrderWithLinesSpec with customer filter — eager-loads OrderLines
        var spec = new OrderWithLinesSpec();
        var allOrders = await _repository.FindAsync(spec, cancellationToken);

        // Filter by customer in memory (simple example; for production, create a dedicated spec)
        var customerOrders = allOrders
            .Where(o => o.CustomerId == query.CustomerId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var items = customerOrders.Select(o =>
        {
            var total = o.CalculateTotal();
            return new OrderDto(o.Id, o.CustomerId, o.Status, o.ShippingAddress, total.Amount, total.Currency);
        }).ToList();

        var totalCount = allOrders.Count(o => o.CustomerId == query.CustomerId);

        return new PagedResult<OrderDto>(items, totalCount, query.Page, query.PageSize);
    }
}
