using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class FutsalImage
{
    public int Id { get; set; }

    public int FutsalId { get; set; }
    public Futsal Futsal { get; set; } = null!;

    [MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}