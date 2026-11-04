namespace DominicanTools.Server.Identifiers;

public enum IdProblem
{
    None,
    Length,
    NotDigits,
    CheckDigit,
    Zeros,
}

/// <param name="Digits">Solo los dígitos, sin guiones ni espacios (vacío si el texto traía otros caracteres).</param>
/// <param name="Formatted">Con los guiones de siempre; vacío mientras no sea válido.</param>
public readonly record struct IdResult(bool Valid, string Digits, string Formatted, IdProblem Problem)
{
    public static IdResult Invalid(string digits, IdProblem problem) => new(false, digits, "", problem);
}

internal static class Digits
{
    /// <summary>Quita guiones y espacios. Devuelve null si queda algo que no sea un dígito.</summary>
    public static string? Clean(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var buffer = new char[input.Length];
        var n = 0;
        foreach (var c in input)
        {
            if (c is '-' or ' ') continue;
            if (c is < '0' or > '9') return null;
            buffer[n++] = c;
        }
        return new string(buffer, 0, n);
    }
}
