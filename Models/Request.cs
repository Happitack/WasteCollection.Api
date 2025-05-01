using System.ComponentModel.DataAnnotations;

namespace WasteCollection.Api.Models;

public enum RequestStatus
{
    Pending,
    Processing,
    Completed,
    Cancelled
}

public class Request 
{ 
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string WasteType { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ContactInfo { get; set; }

    [Required]
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ProcessedAt { get; set; }
}