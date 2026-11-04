using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace DominicanTools.Evals;

public sealed record AgentPrompt(string Id, string Prompt, string? ExpectTool, string[]? MustContain, string[]? MustSay, string[]? MustNotContain);

/// <summary>
/// Le da a Gemini las herramientas del servidor y mide tres cosas: si llama a la herramienta correcta (o a ninguna cuando
/// la pregunta no es de este servidor), si la respuesta final contiene lo que la herramienta devolvió, y si trae números
/// que no salen ni del prompt ni de la salida de las herramientas. Las comprobaciones de texto son heurísticas, no un juez.
/// </summary>
public static partial class AgentEval
{
    private const string System =
        "Eres un asistente. Cuando la pregunta sea sobre un RNC, una cédula, un NCF o el Código de Trabajo dominicano, usa las " +
        "herramientas disponibles y responde con lo que devuelvan. No inventes datos. Si las herramientas no pueden responder algo, dilo.";

    [GeneratedRegex(@"\d[\d-]{2,}\d|\d{3,}")]
    private static partial Regex NumberRx();

    public static async Task<int> RunAsync(McpClient mcp, int runs)
    {
        var key = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) { Console.Error.WriteLine("Falta GEMINI_API_KEY."); return 2; }

        var model = Environment.GetEnvironmentVariable("AGENT_MODEL") ?? "gemini-flash-lite-latest";
        IChatClient chat = new Google.GenAI.Client(apiKey: key).AsIChatClient(model).AsBuilder().UseFunctionInvocation().Build();
        var tools = await mcp.ListToolsAsync();
        var options = new ChatOptions { Temperature = 0f, Tools = [.. tools], MaxOutputTokens = 700 };

        var prompts = JsonSerializer.Deserialize<List<AgentPrompt>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "agent-prompts.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var rows = new List<object>();
        int selected = 0, answered = 0, ungrounded = 0, total = 0, errors = 0;
        for (var run = 1; run <= runs; run++)
            foreach (var p in prompts)
            {
                total++;
                var row = await RunOneAsync(chat, options, p);
                // Cuota por minuto del plan gratis: se espera y se reintenta dos veces antes de darlo por error.
                for (var attempt = 0; attempt < 2 && row.Error?.Contains("quota", StringComparison.OrdinalIgnoreCase) == true; attempt++)
                {
                    Console.WriteLine($"     cuota: espero 65 s y reintento {p.Id}");
                    await Task.Delay(TimeSpan.FromSeconds(65));
                    row = await RunOneAsync(chat, options, p);
                }
                if (row.Error is not null) { errors++; Console.WriteLine($"[{run}] {p.Id,-12} ERROR {row.Error}"); rows.Add(row); continue; }
                if (row.ToolOk) selected++;
                if (row.AnswerOk) answered++;
                if (row.Ungrounded.Count > 0) ungrounded++;
                Console.WriteLine($"[{run}] {p.Id,-12} herramienta={(row.ToolOk ? "ok" : "MAL")} ({string.Join(",", row.Calls)}) respuesta={(row.AnswerOk ? "ok" : "MAL")}" +
                                  (row.Ungrounded.Count > 0 ? $" números sin fuente: {string.Join(",", row.Ungrounded)}" : ""));
                rows.Add(row);
                await Task.Delay(8000); // plan gratis: unas 15 peticiones por minuto y cada prompt usa dos
            }

        var scored = total - errors;
        Console.WriteLine($"\nagent ({model}, {runs} corrida(s), {total} llamadas, {errors} con error):");
        Console.WriteLine($"  elige la herramienta correcta : {selected}/{scored}");
        Console.WriteLine($"  respuesta con lo esperado     : {answered}/{scored}");
        Console.WriteLine($"  con números sin fuente        : {ungrounded}/{scored}");

        Directory.CreateDirectory("results");
        File.WriteAllText($"results/agent-{DateTime.UtcNow:yyyyMMdd-HHmm}.json",
            JsonSerializer.Serialize(new { model, runs, selected, answered, ungrounded, errors, total, rows }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    private sealed record Row(string Id, string[] Calls, bool ToolOk, bool AnswerOk, List<string> Ungrounded, string Answer, string? Error);

    private static async Task<Row> RunOneAsync(IChatClient chat, ChatOptions options, AgentPrompt p)
    {
        try
        {
            var response = await chat.GetResponseAsync([new ChatMessage(ChatRole.System, System), new ChatMessage(ChatRole.User, p.Prompt)], options);
            var calls = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().Select(c => c.Name).ToArray();
            var results = string.Join(" ", response.Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Select(r => r.Result?.ToString() ?? ""));
            var answer = response.Text ?? "";

            var toolOk = p.ExpectTool is null ? calls.Length == 0 : calls.Length > 0 && calls[0] == p.ExpectTool;
            var answerOk = (p.MustContain?.All(s => Has(answer, s)) ?? true)
                           && (p.MustSay?.Any(s => Has(answer, s)) ?? true)
                           && !(p.MustNotContain?.Any(s => Has(answer, s)) ?? false);

            // Un número de la respuesta debe salir del prompt o de lo que devolvieron las herramientas.
            var allowed = Normalize(p.Prompt + " " + results);
            var ungrounded = NumberRx().Matches(answer).Select(m => m.Value).Distinct().Where(n => !allowed.Contains(Normalize(n))).ToList();
            return new Row(p.Id, calls, toolOk, answerOk, ungrounded, answer, null);
        }
        catch (Exception e)
        {
            return new Row(p.Id, [], false, false, [], "", e.Message.Split('\n')[0]);
        }
    }

    private static bool Has(string text, string fragment) => Normalize(text).Contains(Normalize(fragment));

    private static string Normalize(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().ToLowerInvariant();
    }
}
