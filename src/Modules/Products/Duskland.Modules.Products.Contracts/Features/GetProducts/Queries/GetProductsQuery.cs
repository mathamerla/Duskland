using Duskland.BuildingBlocks.Application.Requests;

namespace Duskland.Modules.Products.Contracts.Features.GetProducts.Queries;

public record GetProductsQuery(PagedQueryParameters Parameters, Guid StoreId, string SearchByName = "")
{
       public bool HasSearch =>  !string.IsNullOrWhiteSpace(SearchByName);
}
