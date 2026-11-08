using System.ComponentModel;
using DominicanTools.Server.Identifiers;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DominicanTools.Server.Tools;

/// <summary>
/// Validadores de identificadores fiscales dominicanos. Solo comprueban la forma y el dígito verificador:
/// no consultan a la DGII, así que no dicen si la persona o la empresa existen.
/// </summary>
[McpServerToolType]
public sealed class IdentifierTools
{
    public const int MaxInput = 64;

    private const string Scope =
        "Solo comprueba el formato y el dígito verificador; no consulta a la DGII ni dice si el número existe o pertenece a alguien.";

    [McpServerTool(Name = "validate_rnc", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Valida un RNC dominicano de 9 dígitos (dígito verificador módulo 11) y lo devuelve con sus guiones. " + Scope)]
    public static string ValidateRnc([Description("El RNC, con o sin guiones o espacios. Ejemplo: 131246796")] string value) =>
        Respond(Rnc.Validate(Checked(value)));

    [McpServerTool(Name = "validate_cedula", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Valida una cédula dominicana de 11 dígitos (dígito verificador de Luhn) y la devuelve con sus guiones. " + Scope)]
    public static string ValidateCedula([Description("La cédula, con o sin guiones o espacios. Ejemplo: 001-1234567-3")] string value) =>
        Respond(Cedula.Validate(Checked(value)));

    [McpServerTool(Name = "parse_ncf", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Lee un NCF (serie B, 11 caracteres) o un e-CF (serie E, 13 caracteres) y devuelve su tipo de comprobante y su secuencia. "
        + "Solo mira el formato: no sabe si el comprobante fue autorizado ni si la secuencia está dentro de un rango válido.")]
    public static string ParseNcf([Description("El NCF o e-CF. Ejemplo: B0100000001")] string value)
    {
        var r = Ncf.Validate(Checked(value));
        return Json.Serialize(r.Valid
            ? new NcfOut(true, r.Info!.Value.Kind.ToString().ToLowerInvariant(), r.Info.Value.TypeCode, r.Info.Value.TypeName, r.Info.Value.Sequence, null)
            : new NcfOut(false, null, null, null, null, r.Problem));
    }

    /// <summary>Un identificador de más de 64 caracteres no es un identificador: se rechaza antes de procesarlo.</summary>
    private static string Checked(string? value)
    {
        if (value is null) throw new McpException("Falta el valor.");
        if (value.Length > MaxInput) throw new McpException($"El valor no puede pasar de {MaxInput} caracteres.");
        return value;
    }

    private static string Respond(IdResult r) =>
        Json.Serialize(new IdOut(r.Valid, r.Valid ? r.Formatted : null, r.Valid ? null : r.Problem.ToString()));

    private sealed record IdOut(bool Valid, string? Formatted, string? Problem);

    private sealed record NcfOut(bool Valid, string? Kind, string? Type, string? TypeName, long? Sequence, string? Problem);
}
