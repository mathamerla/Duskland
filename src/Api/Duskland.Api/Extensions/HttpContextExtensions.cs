using System;
using Duskland.Modules.Products.Infrastructure.DAL.Exceptions;
using Microsoft.Net.Http.Headers;

namespace Duskland.Api.Extensions;

public static class HttpContextExtensions
{
    public static Guid GetIfMatchStamp(this HttpContext context)
    {
        var headerExists = context.Request.Headers.TryGetValue(HeaderNames.IfMatch, out var ifMatchHeaderCollection);
    
        if (!headerExists)
        {
            throw new PreConditionFailedException("Missing version in the request");
        }

        var ifMatchHeader = ifMatchHeaderCollection.First()?.ToString();

        if (string.IsNullOrEmpty(ifMatchHeader))
        {
            throw new PreconditionMissingException("Missing version in the request");
        }

        var cleanETag = ifMatchHeader.Trim('"');
       
        if (!Guid.TryParse(cleanETag, out var version))
        {
            throw new PreConditionFailedException("Incorrect format of the version in the request");
        }

        return version;
    }

    public static void AddETagToResponse(this HttpContext context, Guid version)
    {
        context.Response.Headers.ETag = $"\"{version:D}\"";       
    }
}
