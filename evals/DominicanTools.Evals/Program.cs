using System.IO.Pipelines;
using System.Text.Json;
using DominicanTools.Evals;
using DominicanTools.Server.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// golden  Comprueba la salida de cada herramienta contra respuestas conocidas. Determinista; corre en el CI.
// agent   Le da prompts a un modelo (Gemini) con las herramientas y mide si elige bien y si responde con lo que devolvieron.
//         Necesita GEMINI_API_KEY, gasta cuota y NO es determinista: no corre en el CI.
var command = args.ElementAtOrDefault(0) ?? "golden";

await using var server = await InProcessServer.StartAsync();

return command switch
{
    "golden" => await Golden.RunAsync(server.Client),
    "agent" => await AgentEval.RunAsync(server.Client, runs: int.TryParse(args.ElementAtOrDefault(1), out var n) ? n : 1),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("Comandos: golden | agent [repeticiones]");
    return 2;
}

namespace DominicanTools.Evals
{
    /// <summary>El servidor MCP real y un cliente MCP real unidos por tuberías en memoria.</summary>
    public sealed class InProcessServer : IAsyncDisposable
    {
        private IHost _host = null!;
        public McpClient Client { get; private set; } = null!;

        public static async Task<InProcessServer> StartAsync()
        {
            var s = new InProcessServer();
            var clientToServer = new Pipe();
            var serverToClient = new Pipe();
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddMcpServer()
                .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
                .WithToolsFromAssembly(typeof(IdentifierTools).Assembly);
            s._host = builder.Build();
            await s._host.StartAsync();
            s.Client = await McpClient.CreateAsync(new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()));
            return s;
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
