using System.Text.Json;

namespace RAG;

/// <summary>
/// Helper methods for accessing demo product information.
/// </summary>
public static class Helpers
{
    /// <summary>
    /// Gets the path to the sample data directory.
    /// </summary>
    public static readonly string DataDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../data"));

    /// <summary>
    /// Reads all products from the sample data file.
    /// </summary>
    /// <returns>An array of available products.</returns>
    public static Product[] GetAllProducts()
        => JsonSerializer.Deserialize<Product[]>(File.ReadAllText(Path.Combine(DataDir, "products.json")))!;

    /// <summary>
    /// Prompts the user to choose a product to use for the chat demo.
    /// </summary>
    /// <returns>The selected <see cref="Product"/>.</returns>
    public static Product GetCurrentProduct()
    {
        // In a real app, the user would likely already have some context, such as being on the page
        // for a particular product, or working with a customer enquiry about a specific product.
        // In this case we'll prompt the user to pick a product ID, giving random suggestions.
        var products = GetAllProducts();

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Please enter any product ID. Suggestions:\n");
            for (var i = 0; i < 3; i++)
            {
                var suggestion = products[new Random().Next(products.Length)];
                Console.WriteLine($"   {suggestion.ProductId}: {suggestion.Brand} {suggestion.Model}");
            }

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("\n> ");
            var productId = Console.ReadLine();

            if (int.TryParse(productId, out var id) && products.FirstOrDefault(p => p.ProductId == id) is { } product)
            {
                return product;
            }
        }
    }
}
