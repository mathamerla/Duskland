using Duskland.Modules.Products.Contracts.Common;

namespace Duskland.Modules.Products.Contracts.Features.GetProducts.Dtos;

public enum Direction
{
    DESC,
    ASC
}

public class PagedQueryParameters(int page, int pageSize, Direction direction, string? sortBy)
{
    public int Page { get; set; } = page;
    public int PageSize { get; set; } = pageSize;
    public Direction Direction { get; set; } = direction;
    public string SortBy { get; set; } = sortBy ?? string.Empty;

    public bool HasSorting => !string.IsNullOrWhiteSpace(SortBy);

    public int SkipCount ()
    {
        try
        {
            return checked((Page - 1) * PageSize);   
        }
        catch (OverflowException)
        {
            throw new ContractValidationException("This page is not supported for that page size");
        }
    }
}
