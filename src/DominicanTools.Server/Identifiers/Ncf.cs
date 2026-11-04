namespace DominicanTools.Server.Identifiers;

public enum NcfKind
{
    /// <summary>Comprobante en papel o impreso: serie B + tipo de 2 dígitos + 8 de secuencia (11 caracteres).</summary>
    Traditional,

    /// <summary>Comprobante fiscal electrónico (e-CF): serie E + tipo de 2 dígitos + 10 de secuencia (13 caracteres).</summary>
    Electronic,
}

public readonly record struct NcfInfo(NcfKind Kind, string TypeCode, string TypeName, long Sequence);

public readonly record struct NcfResult(bool Valid, NcfInfo? Info, string? Problem)
{
    public static NcfResult Fail(string problem) => new(false, null, problem);
}

/// <summary>
/// Forma de un NCF o e-CF. Solo mira el formato: que el número exista o esté dentro de un rango
/// autorizado no se puede saber sin consultar a la DGII.
/// </summary>
public static class Ncf
{
    // Tipos según la documentación pública de la DGII. Verificar contra la norma vigente antes de producción.
    private static readonly Dictionary<string, string> TraditionalTypes = new()
    {
        ["01"] = "Factura de crédito fiscal",
        ["02"] = "Factura de consumo",
        ["03"] = "Nota de débito",
        ["04"] = "Nota de crédito",
        ["11"] = "Comprobante de compras",
        ["12"] = "Registro único de ingresos",
        ["13"] = "Comprobante para gastos menores",
        ["14"] = "Comprobante para regímenes especiales",
        ["15"] = "Comprobante gubernamental",
        ["16"] = "Comprobante para exportaciones",
        ["17"] = "Comprobante para pagos al exterior",
    };

    private static readonly Dictionary<string, string> ElectronicTypes = new()
    {
        ["31"] = "Factura de crédito fiscal electrónica",
        ["32"] = "Factura de consumo electrónica",
        ["33"] = "Nota de débito electrónica",
        ["34"] = "Nota de crédito electrónica",
        ["41"] = "Comprobante electrónico de compras",
        ["43"] = "Comprobante electrónico para gastos menores",
        ["44"] = "Comprobante electrónico para regímenes especiales",
        ["45"] = "Comprobante electrónico gubernamental",
        ["46"] = "Comprobante electrónico para exportaciones",
        ["47"] = "Comprobante electrónico para pagos al exterior",
    };

    public const int TraditionalSequenceDigits = 8;
    public const int ElectronicSequenceDigits = 10;

    public static IReadOnlyDictionary<string, string> Types(NcfKind kind) =>
        kind == NcfKind.Traditional ? TraditionalTypes : ElectronicTypes;

    public static int SequenceDigits(NcfKind kind) =>
        kind == NcfKind.Traditional ? TraditionalSequenceDigits : ElectronicSequenceDigits;

    public static long MaxSequence(NcfKind kind) => kind == NcfKind.Traditional ? 99_999_999L : 9_999_999_999L;

    public static NcfResult Validate(string? input)
    {
        var text = input?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(text)) return NcfResult.Fail("length");

        var kind = text[0] switch
        {
            'B' => NcfKind.Traditional,
            'E' => NcfKind.Electronic,
            _ => (NcfKind?)null,
        };
        if (kind is null) return NcfResult.Fail("series");
        if (text.Length != 3 + SequenceDigits(kind.Value)) return NcfResult.Fail("length");
        if (text.AsSpan(1).ContainsAnyExceptInRange('0', '9')) return NcfResult.Fail("not_digits");

        var typeCode = text.Substring(1, 2);
        if (!Types(kind.Value).TryGetValue(typeCode, out var typeName)) return NcfResult.Fail("type");

        var sequence = long.Parse(text.AsSpan(3), System.Globalization.CultureInfo.InvariantCulture);
        if (sequence == 0) return NcfResult.Fail("sequence");

        return new(true, new NcfInfo(kind.Value, typeCode, typeName, sequence), null);
    }

    public static string Format(NcfKind kind, string typeCode, long sequence) =>
        $"{(kind == NcfKind.Traditional ? 'B' : 'E')}{typeCode}{sequence.ToString().PadLeft(SequenceDigits(kind), '0')}";
}
