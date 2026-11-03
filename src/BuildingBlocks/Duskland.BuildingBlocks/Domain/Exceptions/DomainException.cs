using System;

namespace Duskland.BuildingBlocks.Domain.Exceptions;

public class DomainException(string message) : Exception(message)
{
}