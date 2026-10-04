using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Majordomo.SharedKernel;
using Majordomo.Strategy.Contracts;

namespace Majordomo.Strategy.Application;

/// <summary>Valida i parametri del job contro trading-job-parameters.v1.schema.json (risorsa incorporata).</summary>
public sealed class ParametersValidator
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly JsonSchema _schema;

    public ParametersValidator()
    {
        using var stream = typeof(ParametersValidator).Assembly.GetManifestResourceStream("trading-job-parameters.v1.schema.json")
            ?? throw new InvalidOperationException("Schema dei parametri non incorporato nell'assembly.");
        using var reader = new StreamReader(stream);
        _schema = JsonSchema.FromText(reader.ReadToEnd());
    }

    /// <summary>Valida e restituisce il JSON normalizzato; lancia <see cref="RequestValidationException"/> (422) se non valido.</summary>
    public string ValidateAndNormalize(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            throw new RequestValidationException("parameters", "I parametri devono essere un oggetto JSON.");
        }

        var result = _schema.Evaluate(parameters, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (!result.IsValid)
        {
            var errors = (result.Details ?? [])
                .Where(d => d.Errors is { Count: > 0 })
                .GroupBy(d => "parameters" + d.InstanceLocation.ToString())
                .ToDictionary(g => g.Key, g => g.SelectMany(d => d.Errors!.Values).Distinct().ToArray());
            throw new RequestValidationException(errors.Count > 0 ? errors : new Dictionary<string, string[]> { ["parameters"] = ["Parametri non conformi allo schema v1."] });
        }

        return parameters.GetRawText();
    }

    public static JobParameters Parse(string json) =>
        JsonSerializer.Deserialize<JobParameters>(json, Json)
        ?? throw new InvalidOperationException("Parametri del job vuoti.");

    public static JsonNode? AsNode(string json) => JsonNode.Parse(json);
}
