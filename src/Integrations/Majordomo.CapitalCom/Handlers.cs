using System.Diagnostics;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using Majordomo.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Majordomo.CapitalCom;

/// <summary>Rate limiter condiviso (singleton) per tutte le chiamate verso Capital.com.</summary>
internal sealed class CapitalRateLimiter : IDisposable
{
    private readonly TokenBucketRateLimiter _limiter;

    public CapitalRateLimiter(IOptions<CapitalComOptions> options)
    {
        var perSecond = options.Value.MaxRequestsPerSecond;
        _limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = perSecond,
            TokensPerPeriod = perSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 100,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken) => _limiter.AcquireAsync(1, cancellationToken);

    public void Dispose() => _limiter.Dispose();
}

/// <summary>Limita le richieste in uscita a <see cref="CapitalComOptions.MaxRequestsPerSecond"/> (vincolo del broker).</summary>
internal sealed class OutboundRateLimitHandler(CapitalRateLimiter limiter) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        using var lease = await limiter.AcquireAsync(cancellationToken);
        MajordomoTelemetry.CapitalApiRateLimitWait.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        if (!lease.IsAcquired)
        {
            throw new CapitalComException("Coda del rate limiter verso Capital.com piena.", System.Net.HttpStatusCode.TooManyRequests);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Mantiene i token di sessione (CST / X-SECURITY-TOKEN) e li rinnova alla scadenza.</summary>
internal sealed class CapitalSession(
    IHttpClientFactory httpClientFactory,
    IOptions<CapitalComOptions> options,
    TimeProvider clock,
    ILogger<CapitalSession> logger)
{
    public const string HttpClientName = "capital-session";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SessionTokens? _tokens;

    public async Task<SessionTokens> GetAsync(CancellationToken cancellationToken)
    {
        var current = _tokens;
        if (current is not null && current.ExpiresAt > clock.GetUtcNow())
        {
            return current;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_tokens is not null && _tokens.ExpiresAt > clock.GetUtcNow())
            {
                return _tokens;
            }

            _tokens = await CreateAsync(cancellationToken);
            return _tokens;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _tokens = null;

    private async Task<SessionTokens> CreateAsync(CancellationToken cancellationToken)
    {
        var o = options.Value;
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/session")
        {
            Content = JsonContent.Create(new { identifier = o.Identifier, password = o.Password, encryptedPassword = false }),
        };
        request.Headers.Add("X-CAP-API-KEY", o.ApiKey);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new CapitalComException("Apertura della sessione Capital.com fallita.", response.StatusCode);
        }

        var cst = response.Headers.TryGetValues("CST", out var c) ? c.FirstOrDefault() : null;
        var security = response.Headers.TryGetValues("X-SECURITY-TOKEN", out var s) ? s.FirstOrDefault() : null;
        if (string.IsNullOrEmpty(cst) || string.IsNullOrEmpty(security))
        {
            throw new CapitalComException("Sessione Capital.com senza token CST/X-SECURITY-TOKEN.", response.StatusCode);
        }

        logger.LogInformation("Sessione Capital.com aperta su {BaseUrl}", o.BaseUrl);
        return new SessionTokens(cst, security, clock.GetUtcNow().Add(o.SessionLifetime));
    }

    internal sealed record SessionTokens(string Cst, string SecurityToken, DateTimeOffset ExpiresAt);
}

/// <summary>Aggiunge API key e token di sessione; su 401 invalida la sessione (rinnovo alla richiesta successiva).</summary>
internal sealed class SessionHandler(CapitalSession session, IOptions<CapitalComOptions> options) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tokens = await session.GetAsync(cancellationToken);
        request.Headers.Remove("X-CAP-API-KEY");
        request.Headers.Add("X-CAP-API-KEY", options.Value.ApiKey);
        request.Headers.Add("CST", tokens.Cst);
        request.Headers.Add("X-SECURITY-TOKEN", tokens.SecurityToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            session.Invalidate();
        }

        return response;
    }
}
