using System;

namespace Duskland.BuildingBlocks.Application.Exceptions;

public class ContractValidationException(string message) : Exception(message)
{

}
