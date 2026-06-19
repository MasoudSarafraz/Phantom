using ECommerce.Application.Commands;
using ECommerce.Application.Queries;
using Microsoft.AspNetCore.Mvc;
using Phantom.Core.Results;
using Phantom.CQRS.Dispatchers;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public CustomersController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>
    /// GET /api/customers/{id} — Simple GetById query
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> GetById(Guid id)
    {
        var customer = await _dispatcher.QueryAsync(new GetCustomerByIdQuery(id));
        return Ok(customer);
    }

    /// <summary>
    /// GET /api/customers/{id}/orders?page=1&amp;pageSize=10
    /// Example: List customer orders using Specification with Include + Paging
    /// </summary>
    [HttpGet("{id:guid}/orders")]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetOrders(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _dispatcher.QueryAsync(new GetCustomerOrdersQuery(id, page, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// POST /api/customers — Create customer
    /// Example: Handler returns Result&lt;Guid&gt; instead of throwing.
    /// The Result monad lets us return proper HTTP status codes without exception overhead.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateCustomerCommand command)
    {
        var result = await _dispatcher.SendAsync<Result<Guid>>(command);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);
    }
}
