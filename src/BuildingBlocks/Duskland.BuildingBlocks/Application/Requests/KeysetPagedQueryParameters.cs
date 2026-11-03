using System;

namespace Duskland.BuildingBlocks.Application.Requests;

public record KeysetPagedQueryParameters(string Cursor = "", int PageSize = 20);
