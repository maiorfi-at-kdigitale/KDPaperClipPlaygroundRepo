using System.ComponentModel.DataAnnotations;

namespace Majordomo.CapitalCom;

/// <summary>Configurazione del gateway Capital.com. Le credenziali arrivano da user-secrets / variabili d'ambiente.</summary>
public sealed class CapitalComOptions
{
    public const string Section = "CapitalCom";

    /// <summary>Host del conto Live: bloccato se <see cref="AllowLive"/> è false (ADR-0005, Demo-first).</summary>
    public const string LiveHost = "api-capital.backend-capital.com";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://demo-api-capital.backend-capital.com";

    public string? ApiKey { get; set; }

    public string? Identifier { get; set; }

    public string? Password { get; set; }

    public bool AllowLive { get; set; }

    [Range(1, 10)]
    public int MaxRequestsPerSecond { get; set; } = 10;

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>Durata massima della sessione prima del rinnovo (Capital.com: 10 minuti di inattività).</summary>
    public TimeSpan SessionLifetime { get; set; } = TimeSpan.FromMinutes(9);

    internal bool TargetsLive => Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
        && string.Equals(uri.Host, LiveHost, StringComparison.OrdinalIgnoreCase);
}
