using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs.Futsal;

public class CreateReviewDto
{
    /// <summary>Required when Reviews:RequireVerifiedBooking is true.</summary>
    [MaxLength(30)]
    public string? ReferenceCode { get; set; }

    [MaxLength(20)]
    public string? PlayerContact { get; set; }

    /// <summary>Alias the frontend sends.</summary>
    [MaxLength(20)]
    public string? ContactNumber { get; set; }

    [MaxLength(80)]
    public string? ReviewerName { get; set; }

    [MaxLength(80)]
    public string? PlayerName { get; set; }

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    public string? Contact => !string.IsNullOrWhiteSpace(PlayerContact) ? PlayerContact : ContactNumber;
    public string? Name => !string.IsNullOrWhiteSpace(ReviewerName) ? ReviewerName : PlayerName;
}