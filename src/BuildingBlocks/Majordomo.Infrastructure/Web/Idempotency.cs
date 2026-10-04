using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Majordomo.Infrastructure.Web;

/// <summary>
/// Supporto all'header <c>Idempotency-Key</c> (OpenAPI v1) sui POST/PUT che modificano stato:
/// la prima risposta (status + corpo) viene memorizzata per 24 h e riproposta ai retry con la
/// stessa chiave, utente ed endpoint.
/// Nota scheletro: lo store è <see cref="IDistributedCache"/> in memoria; per più istanze dell'API
/// va sostituito con uno store condiviso (PostgreSQL o Redis) senza toccare gli endpoint.
/// </summary>
public sealed class IdempotencyFilter(IDistributedCache cache) : IEndpointFilter
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly DistributedCacheEntryOptions Retention = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var key = http.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(key))
        {
            return await next(context);
        }

        if (key.Length > 128)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Idempotency-Key oltre 128 caratteri");
        }

        var cacheKey = $"idem:{http.User.UserName()}:{http.Request.Method}:{http.Request.Path}:{key}";
        var stored = await cache.GetStringAsync(cacheKey, http.RequestAborted);
        if (stored is not null)
        {
            var replay = JsonSerializer.Deserialize<StoredResponse>(stored, Json)!;
            http.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Text(replay.Body ?? string.Empty, "application/json", statusCode: replay.Status);
        }

        var result = await next(context);
        if (result is IStatusCodeHttpResult { StatusCode: >= 200 and < 300 } statusResult)
        {
            var body = result is IValueHttpResult { Value: { } value } ? JsonSerializer.Serialize(value, value.GetType(), Json) : null;
            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(new StoredResponse(statusResult.StatusCode ?? 200, body), Json),
                Retention, http.RequestAborted);
        }

        return result;
    }

    private sealed record StoredResponse(int Status, string? Body);
}

public static class IdempotencyExtensions
{
    public static TBuilder WithIdempotency<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter<TBuilder, IdempotencyFilter>();

    internal static IServiceCollection AddIdempotency(this IServiceCollection services)
    {
        services.AddDistributedMemoryCache();
        services.AddScoped<IdempotencyFilter>();
        return services;
    }
}

/// <summary>ETag deboli derivati dal token di concorrenza (<c>xmin</c>) degli aggregati.</summary>
public static class ETags
{
    public static string From(uint version) => $"W/\"{version}\"";

    public static bool TryParse(string? header, out uint version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var raw = header.Trim();
        if (raw.StartsWith("W/", StringComparison.Ordinal))
        {
            raw = raw[2..];
        }

        return uint.TryParse(raw.Trim('"'), out version);
    }
}
