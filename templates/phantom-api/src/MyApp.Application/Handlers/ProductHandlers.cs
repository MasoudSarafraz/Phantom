using FluentValidation;
using MyApp.Domain.Entities;
using Phantom.CQRS.Commands;
using Phantom.CQRS.Queries;
using Phantom.Core.Exceptions;
using Phantom.Core.Services;

namespace MyApp.Application.Handlers;

public class CreateProductCommandHandler : ICommandHandler<CreateProductCommand, Guid>
{
    private readonly IRepository<Guid, Product> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductCommandHandler(IRepository<Guid, Product> repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> HandleAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
    {
        var product = new Product(Guid.NewGuid(), command.Name, command.Description, command.Price, command.StockQuantity);
        await _repository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return product.Id;
    }
}

public class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductDto>
{
    private readonly IRepository<Guid, Product> _repository;

    public GetProductByIdQueryHandler(IRepository<Guid, Product> repository)
    {
        _repository = repository;
    }

    public async Task<ProductDto> HandleAsync(GetProductByIdQuery query, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), query.Id);

        return new ProductDto(product.Id, product.Name, product.Description, product.Price, product.StockQuantity, product.IsAvailable);
    }
}
