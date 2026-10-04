namespace Majordomo.Strategy.Domain;

public sealed record Signal(SignalDirection Direction, double Strength, double Confidence);

/// <summary>Strategie di segnale come funzioni pure (stesso input, stesso output: backtest == produzione).</summary>
public static class SignalStrategies
{
    /// <summary>
    /// ExpectedReturnThreshold: rendimento atteso a fine orizzonte oltre soglia; la confidenza è la quota
    /// della banda q10–q90 dalla stessa parte del prezzo corrente.
    /// </summary>
    public static Signal ExpectedReturnThreshold(
        double lastClose, double expectedClose, double q10, double q90, double thresholdPct, double minConfidence, bool allowShort)
    {
        if (lastClose <= 0 || double.IsNaN(expectedClose) || q90 < q10)
        {
            return new Signal(SignalDirection.Flat, 0, 0);
        }

        var expectedReturnPct = (expectedClose - lastClose) / lastClose * 100.0;
        var width = Math.Max(q90 - q10, double.Epsilon);
        var confidence = expectedReturnPct >= 0
            ? Math.Clamp((q90 - lastClose) / width, 0, 1)
            : Math.Clamp((lastClose - q10) / width, 0, 1);
        var strength = Math.Abs(expectedReturnPct);

        if (strength < thresholdPct || confidence < minConfidence)
        {
            return new Signal(SignalDirection.Flat, strength, confidence);
        }

        if (expectedReturnPct < 0 && !allowShort)
        {
            return new Signal(SignalDirection.Flat, strength, confidence);
        }

        return new Signal(expectedReturnPct > 0 ? SignalDirection.Long : SignalDirection.Short, strength, confidence);
    }
}
