namespace DominicanTools.Server.Identifiers;

/// <summary>
/// Cédula de 11 dígitos: 3 del municipio, 7 de secuencia y un verificador. El verificador es el
/// de Luhn (pesos 1 y 2 de izquierda a derecha sobre los primeros 10 dígitos).
/// </summary>
public static class Cedula
{
    public static IdResult Validate(string? input)
    {
        var digits = Digits.Clean(input);
        if (digits is null) return IdResult.Invalid("", IdProblem.NotDigits);
        if (digits.Length != 11) return IdResult.Invalid(digits, IdProblem.Length);
        // 000 no es un municipio; sin esta regla "00000000000" pasaría Luhn.
        if (digits.StartsWith("000", StringComparison.Ordinal)) return IdResult.Invalid(digits, IdProblem.Zeros);
        if (digits[10] - '0' != CheckDigit(digits)) return IdResult.Invalid(digits, IdProblem.CheckDigit);

        return new(true, digits, $"{digits[..3]}-{digits[3..10]}-{digits[10]}", IdProblem.None);
    }

    private static int CheckDigit(string digits)
    {
        var total = 0;
        for (var i = 0; i < 10; i++)
        {
            var product = (digits[i] - '0') * (i % 2 == 0 ? 1 : 2);
            total += product > 9 ? product - 9 : product;
        }
        return (10 - total % 10) % 10;
    }
}
