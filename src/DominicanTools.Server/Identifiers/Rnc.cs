namespace DominicanTools.Server.Identifiers;

/// <summary>RNC de 9 dígitos: 8 de cuerpo y un dígito verificador (módulo 11).</summary>
public static class Rnc
{
    private static readonly int[] Weights = [7, 9, 8, 6, 5, 4, 3, 2];

    public static IdResult Validate(string? input)
    {
        var digits = Digits.Clean(input);
        if (digits is null) return IdResult.Invalid("", IdProblem.NotDigits);
        if (digits.Length != 9) return IdResult.Invalid(digits, IdProblem.Length);
        if (digits.AsSpan(0, 8).IndexOfAnyExcept('0') < 0) return IdResult.Invalid(digits, IdProblem.Zeros);
        if (digits[8] - '0' != CheckDigit(digits)) return IdResult.Invalid(digits, IdProblem.CheckDigit);

        return new(true, digits, $"{digits[0]}-{digits[1..3]}-{digits[3..8]}-{digits[8]}", IdProblem.None);
    }

    private static int CheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < Weights.Length; i++) sum += (digits[i] - '0') * Weights[i];
        return (sum % 11) switch
        {
            0 => 2,
            1 => 1,
            var r => 11 - r,
        };
    }
}
