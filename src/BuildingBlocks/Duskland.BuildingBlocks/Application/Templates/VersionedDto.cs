using System;

namespace Duskland.BuildingBlocks.Application.Templates;

public record VersionedDto<T>(Guid Version, T Data) where T : class;
