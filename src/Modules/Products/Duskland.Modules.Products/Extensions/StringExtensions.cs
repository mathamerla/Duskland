using System;

namespace Duskland.Modules.Products.Extensions;

public static class StringExtensions
{
    public static string EscapeLikeInput(this string input)
    {
        if (string.IsNullOrWhiteSpace(input)) 
        {
            return input;
        }

        return input
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}
