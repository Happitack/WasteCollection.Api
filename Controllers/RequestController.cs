using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WasteCollection.Api.Data;
using WasteCollection.Api.Models;

namespace WasteCollection.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RequestsController : ControllerBase 
{
    private readonly ApplicationDbContext _context;

    public RequestsController(ApplicationDbContext context) 
    {
        _context = context;
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

        return CreatedAtAction(nameof(GetRequest), new { id = request.Id }, request);
    }
}