using System;

namespace Duskland.BuildingBlocks.Application.Responses;

public record KeysetPagedList<T>(int PageSize, string NextCursor, IEnumerable<T> Items)
{
    public bool HasMore => !string.IsNullOrEmpty(NextCursor);
}
