using System;

namespace Duskland.BuildingBlocks.Infrastructure.Exceptions;

public class PreConditionFailedException(string message, Exception? innerException = null) : Exception(message, innerException)
{

}