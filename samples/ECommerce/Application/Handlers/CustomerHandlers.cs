using ECommerce.Application.Commands;
using ECommerce.Application.Queries;
using ECommerce.Domain.Entities;
using ECommerce.Domain.ValueObjects;
using Phantom.Core.Services;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Queries;
using Phantom.Core.Exceptions;

namespace ECommerce.Application.Handlers;

public class CreateCustomerCommandHandler : ICommandHandler<CreateCustomerCommand, Guid>
{
    private readonly IRepository<Guid, Customer> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCustomerCommandHandler(IRepository<Guid, Customer> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> HandleAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        var email = new EmailAddress(command.Email);
        var customer = new Customer(Guid.NewGuid(), command.FirstName, command.LastName, email);
        await _repository.AddAsync(customer, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return customer.Id;
    }
}

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
