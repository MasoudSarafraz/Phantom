using MyApp.Application.Commands;
using Microsoft.AspNetCore.Mvc;
using Phantom.CQRS.Dispatchers;

namespace MyApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ProductsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id)
    {
        var product = await _dispatcher.QueryAsync(new GetProductByIdQuery(id));
        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateProductCommand command)
    {
        var id = await _dispatcher.SendAsync<Guid>(command);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }
}
