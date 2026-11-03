using System;

namespace Duskland.BuildingBlocks.Infrastructure.Exceptions;

public class PreconditionMissingException(string message, Exception? innerException = null) : Exception(message, innerException)
{

}