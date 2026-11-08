using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using DominicanTools.Server.Labor;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace DominicanTools.Server.Tools;

/// <summary>
/// Consulta del Código de Trabajo (Ley 16-92) tal como lo publica el Ministerio de Trabajo. Devuelve el texto de los
/// artículos; no lo interpreta ni da asesoría legal, y puede no reflejar reformas posteriores a esa publicación.
/// </summary>
[McpServerToolType]
public sealed class LaborCodeTools
{
    public const int MaxQuery = 300;
    private const string Notice = "Texto del Código de Trabajo publicado por el Ministerio de Trabajo. No es asesoría legal y puede no incluir reformas recientes.";

    private static readonly Lazy<(IReadOnlyList<Article> All, Bm25Index Index)> Corpus = new(Load);

    private static (IReadOnlyList<Article>, Bm25Index) Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("articles.json")
            ?? throw new InvalidOperationException("Falta el recurso articles.json.");
        var all = JsonSerializer.Deserialize<List<Article>>(stream)!;
        return (all, new Bm25Index(all));
    }

    [McpServerTool(Name = "search_labor_code", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Busca por palabras en los 738 artículos del Código de Trabajo dominicano y devuelve los más parecidos a la consulta, con su texto completo. "
        + "Es búsqueda léxica (BM25): funciona mejor con las palabras que usaría la ley que con una pregunta larga. "
        + "Si no hay buenos resultados, devuelve una lista vacía en vez de inventar.")]
    public static string Search(
        [Description("Palabras de la consulta, por ejemplo: preaviso desahucio")] string query,
        [Description("Cuántos artículos devolver, de 1 a 10. Por defecto 5.")] int top = 5)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new McpException("La consulta está vacía.");
        if (query.Length > MaxQuery) throw new McpException($"La consulta no puede pasar de {MaxQuery} caracteres.");
        if (top is < 1 or > 10) throw new McpException("top debe estar entre 1 y 10.");

        var hits = Corpus.Value.Index.Search(query, top);
        return Json.Serialize(new
        {
            query,
            results = hits.Select(h => Shape(h.Article)),
            notice = Notice,
        });
    }

    [McpServerTool(Name = "get_labor_article", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Devuelve el texto exacto de un artículo del Código de Trabajo por su número (1 a 738). Úsalo cuando ya sepas qué artículo necesitas.")]
    public static string GetArticle([Description("Número del artículo, de 1 a 738")] int number)
    {
        var article = Corpus.Value.All.FirstOrDefault(a => a.Number == number && number > 0)
            ?? throw new McpException($"No existe el artículo {number}. El Código tiene del 1 al 738.");
        return Json.Serialize(new { article = Shape(article), notice = Notice });
    }

    private static object Shape(Article a) => new { a.Number, a.Label, a.Context, a.Text, a.Notes };
}
