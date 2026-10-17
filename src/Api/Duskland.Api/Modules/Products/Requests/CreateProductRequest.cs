using System;

namespace Duskland.Api.Modules.Products.Requests;

record CreateProductRequest(string SKU, string Name, decimal Price);