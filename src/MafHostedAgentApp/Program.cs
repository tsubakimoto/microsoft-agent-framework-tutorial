// https://learn.microsoft.com/ja-jp/agent-framework/hosting/foundry-hosted-agent?pivots=programming-language-csharp
// https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/simple-agent
using Azure.AI.Projects;
using Azure.Identity;

using DotNetEnv;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;
using Microsoft.Extensions.AI;

using ModelContextProtocol.Client;

Env.TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4o";

//AIAgent agent = CreateSimpleAgent(projectEndpoint, deployment);
AIAgent agent = await CreateMSLearnAgentAsync(projectEndpoint, deployment);

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

var app = builder.Build();
app.Run();


static AIAgent CreateSimpleAgent(Uri projectEndpoint, string deployment)
{
    AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
        .AsAIAgent(
            model: deployment,
            instructions: "You are a helpful AI assistant.",
            name: "MafHostedAgentApp");
    return agent;
}

static async Task<AIAgent> CreateMSLearnAgentAsync(Uri projectEndpoint, string deployment)
{
    IClientTransport clientTransport = new HttpClientTransport(new()
    {
        Endpoint = new Uri("https://learn.microsoft.com/api/mcp"),
        Name = "Microsoft Learn MCP Server"
    });
    await using var mcpClient = await McpClient.CreateAsync(clientTransport);

    var mcpTools = await mcpClient.ListToolsAsync();
    List<AITool> agentTools = [.. mcpTools.Cast<AITool>()];
    foreach (var tool in mcpTools)
    {
        Console.WriteLine($"Connected to server with tools: {tool.Name}");
    }

    AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
        .AsAIAgent(
            model: deployment,
            instructions: "You answer questions by searching the Microsoft Learn content only.",
            name: "MicrosoftLearnAgent",
            tools: agentTools);
    return agent;
}
