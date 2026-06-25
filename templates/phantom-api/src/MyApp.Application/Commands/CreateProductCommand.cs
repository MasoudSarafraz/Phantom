using Phantom.CQRS.Commands;

namespace MyApp.Application.Commands;

public class CreateProductCommand : ICommand<Guid>
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
}

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
    }
}

public record ProductDto(Guid Id, string Name, string Description, decimal Price, int StockQuantity, bool IsAvailable);

public class GetProductByIdQuery : IQuery<ProductDto>
{
    public Guid Id { get; init; }
    public GetProductByIdQuery(Guid id) { Id = id; }
}
