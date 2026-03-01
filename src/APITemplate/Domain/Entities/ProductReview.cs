namespace APITemplate.Domain.Entities;

public sealed class ProductReview
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public required string ReviewerName { get; set; }
    public string? Comment { get; set; }
    public int Rating { get; set; }
    public DateTime CreatedAt { get; set; }

    public Product Product { get; set; } = null!;
}
