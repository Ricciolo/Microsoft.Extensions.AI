namespace RAG;

/// <summary>
/// Represents a product in the demo catalog.
/// </summary>
public class Product
{
    /// <summary>
    /// Gets or sets the unique identifier of the product.
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the product category.
    /// </summary>
    public int CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the product brand.
    /// </summary>
    public required string Brand { get; set; }

    /// <summary>
    /// Gets or sets the product model.
    /// </summary>
    public required string Model { get; set; }

    /// <summary>
    /// Gets or sets the product description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the product price.
    /// </summary>
    public double Price { get; set; }
}
