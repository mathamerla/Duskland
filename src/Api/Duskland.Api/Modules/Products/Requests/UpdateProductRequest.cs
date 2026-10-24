using System;

namespace Duskland.Api.Modules.Products.Requests;

public record UpdateProductRequest(Guid Id, string SKU, string Name, decimal Price);

