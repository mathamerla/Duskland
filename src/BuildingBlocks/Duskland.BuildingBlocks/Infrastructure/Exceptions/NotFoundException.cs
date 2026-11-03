using System;

namespace Duskland.BuildingBlocks.Infrastructure.Exceptions;

public class NotFoundException(string message) : Exception(message)
{

}
