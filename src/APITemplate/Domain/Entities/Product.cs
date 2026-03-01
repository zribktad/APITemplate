namespace APITemplate.Domain.Entities;

/// <summary>
/// Core domain entity representing a product in the catalog.
/// This is the aggregate root - all business rules around products start here.
/// </summary>
public sealed class Product
{
    /// <summary>Unique identifier generated when the product is created.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name of the product. Required, max 200 characters (enforced by EF config + FluentValidation).</summary>
    public required string Name { get; set; }

    /// <summary>Optional longer description of the product.</summary>
    public string? Description { get; set; }

    /// <summary>Price with 18,2 decimal precision (enforced by EF config).</summary>
    public decimal Price { get; set; }

    /// <summary>UTC timestamp of when the product was created. Defaults to now() in the database.</summary>
    public DateTime CreatedAt { get; set; }

    public ICollection<ProductReview> Reviews { get; set; } = [];
}
