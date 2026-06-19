using ECommerce.Application.Commands;
using ECommerce.Application.Queries;
using Microsoft.AspNetCore.Mvc;
using Phantom.CQRS.Dispatchers;

namespace ECommerce.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public OrdersController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id)
    {
        var order = await _dispatcher.QueryAsync(new GetOrderByIdQuery(id));
        return Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateOrderCommand command)
    {
        var id = await _dispatcher.SendAsync<Guid>(command);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPost("{id:guid}/ship")]
    public async Task<IActionResult> Ship(Guid id, [FromBody] ShipOrderRequest request)
    {
        await _dispatcher.SendAsync(new ShipOrderCommand(id, request.TrackingNumber));
        return NoContent();
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await _dispatcher.SendAsync(new CancelOrderCommand(id));
        return NoContent();
    }
}

public record ShipOrderRequest(string TrackingNumber);
