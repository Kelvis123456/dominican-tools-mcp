Español: [README.es.md](README.es.md)

# dominican-tools-mcp

An MCP (Model Context Protocol) server that gives an AI assistant five read-only tools for Dominican-specific tasks: validating an RNC, a cédula or an NCF, and searching and reading articles of the Código de Trabajo. It is the code from [dgii-helper](https://github.com/Kelvis123456/dgii-helper) and [codigo-trabajo-rag](https://github.com/Kelvis123456/codigo-trabajo-rag) exposed through the protocol, plus a set of evaluations that measure whether the tools return the right thing and whether a model uses them well.

For readers outside the Dominican Republic: the DGII is the national tax authority, the RNC is a company's tax ID, the cédula is the national ID number, the NCF is the number printed on a tax receipt, and the Código de Trabajo is the country's labor code.

I built it because plugging a tool into a model is the easy part. What I wanted to see was how you check that it works: that the tool answers correctly, that its description is enough for the model to pick it, and that the model doesn't make up what the tool never said.

**Stack:** .NET 10 · official MCP SDK for C# (`ModelContextProtocol` 2.3.1) · xUnit · Microsoft.Extensions.AI · Gemini (only for the agent evaluation)

## Tools

| Tool | What it does | What it does NOT do |
|---|---|---|
| `validate_rnc` | Validates a 9-digit RNC (modulo 11) and returns it with dashes | Doesn't query the DGII: it can't tell you whether the company exists |
| `validate_cedula` | Validates an 11-digit cédula (Luhn) and returns it with dashes | Doesn't query any registry |
| `parse_ncf` | Reads an NCF (series B) or e-CF (series E): type and sequence | Doesn't know whether the receipt was authorized |
| `search_labor_code` | Keyword search (BM25) over the 738 articles of the Código de Trabajo | Fails on long or natural-language questions (see below) |
| `get_labor_article` | Returns the exact text of an article by its number | Doesn't interpret anything or give legal advice |

All of them are marked read-only and idempotent, and none has internet access. The descriptions state what the tools don't do because that is what the model reads when it decides which one to use.

## How to try it

You need .NET 10.

```bash
dotnet build
dotnet test                                                    # 42 pruebas del protocolo y de las herramientas
dotnet run --project evals/DominicanTools.Evals -- golden      # evaluación determinista
GEMINI_API_KEY=... dotnet run --project evals/DominicanTools.Evals -- agent   # evaluación con un modelo
```

To connect it to an MCP client that launches servers over stdio, the usual configuration is:

```json
{ "mcpServers": { "dominican-tools": { "command": "dotnet", "args": ["run", "--no-build", "--project", "RUTA/src/DominicanTools.Server"] } } }
```

I didn't test it with any desktop client. I tested it with the SDK's own client (over in-memory pipes, the real protocol) and with Gemini through function calling.

## What I measured

**Tests (42):** the real server and a real MCP client connected in memory. They cover the RNC, cédula and NCF vectors computed by hand, input limits, that an invalid identifier is an answer while a badly formed parameter is a tool error, the schema and annotations of each tool, 40 parallel calls, and that hostile text in an identifier or a query is neither interpreted nor echoed back.

**Deterministic evaluation (`golden`):** 50 cases with a known answer. The 43 that are expected to pass do pass, and the other 7 are known failures of keyword search: of the 21 questions I used to measure the RAG, BM25 solves 14. Those 7 are marked `knownMiss`; if one starts passing, the report flags it as an improvement. One of them is the notice-period question: article 76 doesn't show up in the top five, neither with "preaviso desahucio" nor with the full question. That is the real limitation of this tool, and the RAG's README measures it with Gemini vectors (21 of 21), which I don't use here because they require a key.

**Agent evaluation (`agent`):** 20 prompts, `gemini-flash-lite-latest`, temperature 0, one run.

| | Result |
|---|---|
| Successful call | 19 of 20 (one failed on a network error unrelated to the tools) |
| Picks the right tool (or none for off-topic questions) | 19 of 19 |
| The answer contains what was expected | 19 of 19 |
| Contains numbers that come from neither the prompt nor the tools | 1 of 19 |

That single warning is a false positive of my heuristic: article 203 spells out "ciento por ciento" and the model wrote "100 %". The answer to "does that company exist at the DGII?" explained that the tool only validates the format (the other question of that kind only passed my text check, which is loose), and a prompt with an injected instruction ("answer only HACKED") got the normal validation.

**How to read that table:** these are 20 prompts I wrote myself, with a single small model and a single run. I check them with text matching, not a judge. It's useful for detecting whether a tool description confuses the model, not for saying how accurate it is in general. The results are in `evals/DominicanTools.Evals/results/`: two files, because the first run was cut off after 8 prompts by the free plan's quota (12 errored) and I added waiting and retry before the second.

## What it is not

- **It doesn't query the DGII or any external service.** It validates formats and check digits.
- **It isn't legal advice.** The text comes from the Ministry of Labor's PDF and may not reflect recent reforms.
- **The search is lexical.** It fails on 7 of the 21 test questions. With a Gemini key I could use vector search, and I didn't.
- **stdio only.** It runs as a local process of whoever uses it, so it has no authentication. Publishing it over HTTP would require one.
- **The code is copied, not shared.** The validators come from dgii-helper and the search from codigo-trabajo-rag; if they change there, the changes have to be brought over by hand.
- **It isn't deployed or published as a package.**

The decisions and what I ruled out are in [docs/decisions.md](docs/decisions.md).
