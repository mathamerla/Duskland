using System;

namespace Duskland.Api.Modules.Products.Requests;

public record CreateProductRequest(string SKU, string Name, decimal Price);