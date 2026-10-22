
namespace Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;

/// <summary>
/// To be moved to Building Blocks in the future
/// </summary>
public record PagedList<T>(int Page, int PageSize, int TotalCount, IReadOnlyList<T> Items);

