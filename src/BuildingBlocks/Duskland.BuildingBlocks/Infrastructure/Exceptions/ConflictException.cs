using System;

namespace Duskland.BuildingBlocks.Infrastructure.Exceptions;

public class ConflictException(string message, Exception? innerException = null) : Exception(message, innerException)
{
    
}
