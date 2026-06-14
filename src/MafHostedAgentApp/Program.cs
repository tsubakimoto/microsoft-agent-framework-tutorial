// https://learn.microsoft.com/ja-jp/agent-framework/hosting/foundry-hosted-agent?pivots=programming-language-csharp
// https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/simple-agent
using Azure.AI.AgentServer.Invocations;
using Azure.AI.Projects;
using Azure.Identity;

using DotNetEnv;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry.Hosting;

Env.TraversePath().Load();

var projectEndpoint = new Uri(Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));
var deployment = Environment.GetEnvironmentVariable("AZURE_AI_MODEL_DEPLOYMENT_NAME") ?? "gpt-4o";

AIAgent agent = new AIProjectClient(projectEndpoint, new DefaultAzureCredential())
    .AsAIAgent(
        model: deployment,
        instructions: "You are a helpful AI assistant.",
        name: "MafHostedAgentApp");

var builder = AgentHost.CreateBuilder(args);

// responses
builder.Services.AddFoundryResponses(agent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());

// invocations
// see https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/invocations-echo-agent
builder.Services.AddSingleton<EchoAIAgent>();
builder.Services.AddInvocationsServer();
builder.Services.AddScoped<InvocationHandler, EchoInvocationHandler>();
builder.RegisterProtocol("invocations", endpoints => endpoints.MapInvocationsServer());

var app = builder.Build();
app.Run();