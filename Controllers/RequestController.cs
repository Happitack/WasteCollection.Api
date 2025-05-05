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

    private readonly ILogger<RequestsController> _logger;

    public RequestsController(
        ApplicationDbContext context,
        IMessagePublisher messagePublisher,
        ILogger<RequestsController> logger) // Inject ILogger
    {
        _context = context;
        _messagePublisher = messagePublisher;
        _logger = logger; // Assign the injected logger to the field
    }

    // GET: api/Requests
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Request>>> GetRequests()
    {
        _logger.LogInformation("Getting all requests, ordered by SubmittedAt ascending.");
        try
        {
            var requests = await _context.Requests
                                         .OrderBy(r => r.SubmittedAt)
                                         .ToListAsync();
            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all requests.");
            return StatusCode(500, "Internal server error retrieving requests.");
        }
    }

    // GET: api/Requests/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Request>> GetRequest(int id)
    {
        _logger.LogInformation("Getting request with ID: {RequestId}", id);
        try
        {
            var request = await _context.Requests.FindAsync(id);

            if (request == null)
            {
                _logger.LogWarning("Request with ID: {RequestId} not found.", id);
                return NotFound();
            }

            return Ok(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving request with ID: {RequestId}", id);
            return StatusCode(500, $"Internal server error retrieving request {id}.");
        }
    }

    // POST: api/Requests
    [HttpPost]
    public async Task<ActionResult<Request>> PostRequest(Request request)
    {
        _logger.LogInformation("Attempting to create a new request.");
        // Basic validation or use Data Annotations
        if (!ModelState.IsValid)
        {
             _logger.LogWarning("Invalid model state for new request.");
             return BadRequest(ModelState);
        }

        request.Id = 0;
        request.Status = RequestStatus.Pending;
        request.SubmittedAt = DateTime.UtcNow;
        request.ProcessedAt = null; 

        try
        {
            _context.Requests.Add(request);
            await _context.SaveChangesAsync();
             _logger.LogInformation("New request created with ID: {RequestId}", request.Id);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error saving new request to database.");
             return StatusCode(500, "Internal server error saving request.");
        }


        try
        {
            await _messagePublisher.PublishNewRequestNotification(request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message for Request ID {RequestId} after creation.", request.Id);
        }

        return CreatedAtAction(nameof(GetRequest), new { id = request.Id }, request);
    }

    // PUT: api/Requests/{id}/complete
    [HttpPut("{id}/complete")]
    public async Task<IActionResult> MarkRequestComplete(int id)
    {
        var request = await _context.Requests.FindAsync(id);

        if (request == null)
        {
            _logger.LogWarning("Attempted to complete non-existent Request ID: {RequestId}", id);
            return NotFound($"Request with ID {id} not found.");
        }

        if (request.Status == RequestStatus.Completed || request.Status == RequestStatus.Cancelled)
        {
             _logger.LogWarning("Attempted to complete Request ID: {RequestId} which is already in status {Status}", id, request.Status);
             return Conflict($"Request {id} is already in status '{request.Status}'.");
        }

        request.Status = RequestStatus.Completed;
        request.ProcessedAt = DateTime.UtcNow;

        _context.Entry(request).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Request ID: {RequestId} marked as Completed.", id);
        }
        catch (DbUpdateConcurrencyException ex)
        {
             _logger.LogError(ex, "Concurrency error updating Request ID: {RequestId}", id);
            if (!await RequestExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    // Helper method to check if a request exists
    private async Task<bool> RequestExists(int id)
    {
        return await _context.Requests.AnyAsync(e => e.Id == id);
    }
}