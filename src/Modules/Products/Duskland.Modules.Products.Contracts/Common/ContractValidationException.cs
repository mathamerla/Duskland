using System;

namespace Duskland.Modules.Products.Contracts.Common;

/// <summary>
/// Temp placement - later if need need to be move in BuildingBlocks
/// </summary>
public class ContractValidationException(string message) : Exception(message)
{

}
