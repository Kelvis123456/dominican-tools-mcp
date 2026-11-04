using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DominicanTools.Evals;

public sealed record GoldenCase(
    string Id,
    string Tool,
    Dictionary<string, JsonElement> Args,
    Dictionary<string, JsonElement>? Expect,
    string[]? ExpectTextContains,
    int[]? ExpectTopContains,
    bool KnownMiss);

/// <summary>
/// Cada caso es una pregunta con respuesta conocida: los vectores de los identificadores los calculé a mano y las
/// preguntas de búsqueda son las 21 que usé para medir el RAG. Un "known miss" es un caso que la búsqueda por
/// palabras todavía no resuelve: se espera que falle, y si un día pasa, el reporte lo avisa como mejora.
/// </summary>
public static class Golden
{
    public static async Task<int> RunAsync(McpClient client)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "golden.json");
        var cases = JsonSerializer.Deserialize<List<GoldenCase>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        int pass = 0, fail = 0, knownMisses = 0, improved = 0;
        var failures = new List<string>();
        foreach (var c in cases)
        {
            var (ok, detail) = await CheckAsync(client, c);
            if (c.KnownMiss)
            {
                if (ok) { improved++; failures.Add($"MEJORA  {c.Id}: ahora pasa (quitar knownMiss)"); }
                else knownMisses++;
            }
            else if (ok) pass++;
            else { fail++; failures.Add($"FALLA   {c.Id}: {detail}"); }
        }

        var scored = cases.Count(c => !c.KnownMiss);
        Console.WriteLine($"golden: {pass}/{scored} casos esperados pasan, {knownMisses} fallos conocidos de la búsqueda por palabras, {improved} mejoras");
        foreach (var f in failures) Console.WriteLine("  " + f);
        return fail == 0 && improved == 0 ? 0 : 1;
    }

    private static async Task<(bool, string)> CheckAsync(McpClient client, GoldenCase c)
    {
        var args = c.Args.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
        CallToolResult result;
        try { result = await client.CallToolAsync(c.Tool, args); }
        catch (Exception e) { return (false, "excepción: " + e.Message); }
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
        if (result.IsError == true) return (false, "error de la herramienta: " + text);

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        if (c.Expect is not null)
            foreach (var (key, expected) in c.Expect)
            {
                if (!root.TryGetProperty(key, out var got)) return (false, $"falta '{key}'");
                if (!JsonEquals(expected, got)) return (false, $"'{key}': esperaba {expected}, salió {got}");
            }

        if (c.ExpectTextContains is not null)
        {
            var body = root.GetProperty("article").GetProperty("text").GetString() ?? "";
            foreach (var frag in c.ExpectTextContains)
                if (!body.Contains(frag, StringComparison.OrdinalIgnoreCase)) return (false, $"el texto no contiene '{frag}'");
        }

        if (c.ExpectTopContains is not null)
        {
            var numbers = root.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("number").GetInt32()).ToList();
            if (!numbers.Intersect(c.ExpectTopContains).Any())
                return (false, $"esperaba alguno de [{string.Join(",", c.ExpectTopContains)}], salió [{string.Join(",", numbers)}]");
        }
        return (true, "");
    }

    private static bool JsonEquals(JsonElement a, JsonElement b) => a.ValueKind == b.ValueKind && a.GetRawText() == b.GetRawText()
        || a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number && a.GetDecimal() == b.GetDecimal();
}
