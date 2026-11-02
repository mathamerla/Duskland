using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Duskland.Modules.Products.Helpers;

public static class CursorHelper<T> where T : class
{

 public static string Serialize(T cursorInput) 
    {
        var json = JsonSerializer.Serialize(cursorInput);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static T? Deserialize(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) 
        {
            return null;
        }

        try
        {
            var bytes = WebEncoders.Base64UrlDecode(cursor);
            var json = Encoding.UTF8.GetString(bytes);

            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return null; 
        }
    }
}

