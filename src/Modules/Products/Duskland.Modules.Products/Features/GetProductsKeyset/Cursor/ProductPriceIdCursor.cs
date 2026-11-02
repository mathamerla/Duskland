using System.Text.Json.Serialization;

namespace Duskland.Modules.Products.Features.GetProductsKeyset.Cursor;

internal record ProductPriceIdCursor(decimal Price, Guid Id)
{
    [JsonRequired]
    public decimal Price { get; set; } = Price;

    [JsonRequired]
    public Guid Id { get; set; } = Id;
}
