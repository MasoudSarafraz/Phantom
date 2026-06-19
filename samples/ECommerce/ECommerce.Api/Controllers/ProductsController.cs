using ECommerce.Application.Commands;
using ECommerce.Application.Queries;
using Microsoft.AspNetCore.Mvc;
using Phantom.Core.Results;
using Phantom.CQRS.Dispatchers;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ProductsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>
    /// GET /api/products/{id} — Simple GetById query
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id)
    {
        var product = await _dispatcher.QueryAsync(new GetProductByIdQuery(id));
        return Ok(product);
    }

    /// <summary>
    /// GET /api/products?keyword=laptop&amp;page=1&amp;pageSize=20
    /// Example: Search products using Specification with Paging + OrderBy + AsNoTracking
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> Search(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _dispatcher.QueryAsync(new SearchProductsQuery(keyword, page, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// POST /api/products — Create a new product
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateProductCommand command)
    {
        var id = await _dispatcher.SendAsync<Guid>(command);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }
}
