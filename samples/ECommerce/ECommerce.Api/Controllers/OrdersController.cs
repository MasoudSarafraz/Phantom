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

    /// <summary>
    /// GET /api/orders/{id}
    /// Example: Uses OrderWithLinesSpec to eagerly load OrderLines in a single SQL query
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id)
    {
        var order = await _dispatcher.QueryAsync(new GetOrderByIdQuery(id));
        return Ok(order);
    }

    /// <summary>
    /// POST /api/orders — Create and confirm order
    /// When order.Confirm() is called, it raises OrderPlacedEvent.
    /// Phantom's outbox pattern ensures the event is persisted atomically
    /// with the order, then published to the messaging channel asynchronously.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Guid>> Create([FromBody] CreateOrderCommand command)
    {
        var id = await _dispatcher.SendAsync<Guid>(command);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>
    /// POST /api/orders/{id}/ship — Ship order
    /// Raises OrderShippedEvent → published to "orders" channel
    /// </summary>
    [HttpPost("{id:guid}/ship")]
    public async Task<IActionResult> Ship(Guid id, [FromBody] ShipOrderRequest request)
    {
        await _dispatcher.SendAsync(new ShipOrderCommand(id, request.TrackingNumber));
        return NoContent();
    }

    /// <summary>
    /// POST /api/orders/{id}/cancel — Cancel order
    /// Business rule: shipped/delivered orders cannot be cancelled
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        await _dispatcher.SendAsync(new CancelOrderCommand(id));
        return NoContent();
    }
}

public record ShipOrderRequest(string TrackingNumber);
