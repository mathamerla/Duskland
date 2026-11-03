
namespace Duskland.BuildingBlocks.Application.Responses;

public record PagedList<T>(int Page, int PageSize, int TotalCount, IReadOnlyList<T> Items);

