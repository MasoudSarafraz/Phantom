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
using Phantom.Data.Specifications;

namespace ECommerce.Application.Handlers;

// ─── Product Command Handlers ────────────────────────────────

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
        var price = new Money(command.Price, command.Currency);
        var product = new Product(Guid.NewGuid(), command.Name, command.Description, price, command.StockQuantity);
        await _repository.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return product.Id;
    }
}

// ─── Product Query Handlers ──────────────────────────────────

public class GetProductByIdQueryHandler : IQueryHandler<GetProductByIdQuery, ProductDto>
{
    private readonly IRepository<Guid, Product> _repository;

    public GetProductByIdQueryHandler(IRepository<Guid, Product> repository) => _repository = repository;

    public async Task<ProductDto> HandleAsync(GetProductByIdQuery query, CancellationToken cancellationToken = default)
    {
        var product = await _repository.GetByIdAsync(query.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), query.ProductId);
        return new ProductDto(product.Id, product.Name, product.Price.Amount, product.Price.Currency, product.StockQuantity, product.IsAvailable);
    }
}

/// <summary>
/// Example: Using Specification with Paging, OrderBy, and AsNoTracking.
/// Demonstrates the enriched Specification pattern in Phantom.
/// </summary>
public class SearchProductsQueryHandler : IQueryHandler<SearchProductsQuery, PagedResult<ProductDto>>
{
    private readonly IRepository<Guid, Product> _repository;

    public SearchProductsQueryHandler(IRepository<Guid, Product> repository) => _repository = repository;

    public async Task<PagedResult<ProductDto>> HandleAsync(SearchProductsQuery query, CancellationToken cancellationToken = default)
    {
        // Build specification with keyword filter + paging + ordering + AsNoTracking
        var spec = new PagedProductSpec(keyword: query.Keyword, page: query.Page, pageSize: query.PageSize);

        // FindAsync applies the specification to the IQueryable
        var products = await _repository.FindAsync(spec, cancellationToken);

        // Count total (without paging) for pagination metadata
        var totalCount = await _repository.CountAsync(cancellationToken);

        var items = products.Select(p =>
            new ProductDto(p.Id, p.Name, p.Price.Amount, p.Price.Currency, p.StockQuantity, p.IsAvailable))
            .ToList();

        return new PagedResult<ProductDto>(items, (int)totalCount, query.Page, query.PageSize);
    }
}
