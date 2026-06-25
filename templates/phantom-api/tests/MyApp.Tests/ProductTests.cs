using MyApp.Application.Commands;
using MyApp.Domain.Entities;
using MyApp.Infrastructure.Persistence;
using Phantom.Testing;
using Phantom.Testing.Assertions;
using Xunit;

namespace MyApp.Tests;

public class ProductTests : IAsyncLifetime
{
    private PhantomTestFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = await new PhantomTestBuilder()
            .WithAssembliesFrom(typeof(CreateProductCommand).Assembly, typeof(MyAppDbContext).Assembly)
            .WithDbContext<MyAppDbContext>()
            .WithFluentValidation()
            .WithAuditable()
            .BuildAsync();
    }

    public Task DisposeAsync() => _fixture.DisposeAsync().AsTask();

    [Fact]
    public async Task CreateProduct_Should_Return_ProductId()
    {
        using var scope = _fixture.CreateTestScope();
        var productId = await scope.Dispatcher.SendAsync<Guid>(new CreateProductCommand
        {
            Name = "Widget",
            Description = "A useful widget",
            Price = 9.99m,
            StockQuantity = 100
        });

        Assert.NotEqual(Guid.Empty, productId);

        var stored = await scope.DbContext<MyAppDbContext>().Products.FindAsync(productId);
        Assert.NotNull(stored);
        Assert.Equal("Widget", stored!.Name);
    }

    [Fact]
    public async Task CreateProduct_With_Invalid_Name_Should_Throw()
    {
        using var scope = _fixture.CreateTestScope();
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            scope.Dispatcher.SendAsync<Guid>(new CreateProductCommand
            {
                Name = "",
                Price = 9.99m,
                StockQuantity = 10
            }).AsTask());
    }
}
