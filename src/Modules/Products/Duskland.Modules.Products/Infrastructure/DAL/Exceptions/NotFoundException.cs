using System;

namespace Duskland.Modules.Products.Infrastructure.DAL.Exceptions;

/// <summary>
/// Temp placement - later if need need to be move in BuildingBlocks
/// </summary>
public class NotFoundException(string message) : Exception(message)
{

}