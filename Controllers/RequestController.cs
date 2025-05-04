using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WasteCollection.Api.Data;
using WasteCollection.Api.Models;
using WasteCollection.Api.Interfaces;

namespace WasteCollection.Api.Controllers;


[Route("api/[controller]")]
[ApiController]
public class RequestsController : ControllerBase 
{
    private readonly ApplicationDbContext _context;
    private readonly IMessagePublisher _messagePublisher;

    public RequestsController(ApplicationDbContext context, IMessagePublisher messagePublisher) 
    {
        _context = context;
        _messagePublisher = messagePublisher;
    }

    // GET: api/Requests
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Request>>> GetRequests() 
    {
        var requests = await _context.Requests
                                     .OrderByDescending(r => r.SubmittedAt)
                                     .ToListAsync();
        return Ok(requests);
    }

    // GET: api/Requests/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Request>> GetRequest(int id) 
    {
        var request = await _context.Requests.FindAsync(id);

        if (request == null) 
        {
            return NotFound();
        }

        return Ok(request);
    }

    // POST: api/Requests
    [HttpPost]
    public async Task<ActionResult<Request>> PostRequest(Request request) 
    {
        request.Status = RequestStatus.Pending;
        request.SubmittedAt = DateTime.UtcNow;

        _context.Requests.Add(request);
        await _context.SaveChangesAsync();

        try
        {
            await _messagePublisher.PublishNewRequestNotification(request.Id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error publishing message for Request ID {request.Id}: {ex.Message}"); // Replace with proper ILogger later
        }        

        return CreatedAtAction(nameof(GetRequest), new { id = request.Id }, request);
    }
}