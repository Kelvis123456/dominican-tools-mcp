using System.IO.Pipelines;
using System.Text.Json;
using DominicanTools.Server.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace DominicanTools.Tests;

/// <summary>El servidor de verdad y un cliente MCP de verdad, unidos por tuberías en memoria: se prueba el protocolo, no solo los métodos.</summary>
public sealed class McpHarness : IAsyncLifetime
{
    private IHost _host = null!;
    public McpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var builder = Host.CreateApplicationBuilder();
        builder.Services
            .AddMcpServer()
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream())
            .WithToolsFromAssembly(typeof(IdentifierTools).Assembly);
        _host = builder.Build();
        await _host.StartAsync();

        Client = await McpClient.CreateAsync(new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()));
    }

    public async Task DisposeAsync()
    {
        await Client.DisposeAsync();
        await _host.StopAsync();
        _host.Dispose();
    }

    /// <summary>Llama a una herramienta y devuelve el resultado completo (con IsError) y el texto del primer bloque.</summary>
    public async Task<(CallToolResult Result, string Text)> CallAsync(string tool, Dictionary<string, object?> args)
    {
        var result = await Client.CallToolAsync(tool, args);
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
        return (result, text);
    }

    public async Task<JsonElement> CallJsonAsync(string tool, Dictionary<string, object?> args)
    {
        var (result, text) = await CallAsync(tool, args);
        Assert.NotEqual(true, result.IsError);
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
