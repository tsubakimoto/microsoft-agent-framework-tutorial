using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;

using Microsoft.Agents.AI.Foundry;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var projectEndpoint = new Uri(configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));
var agentName = configuration["FOUNDRY_AGENT_NAME"]
    ?? throw new InvalidOperationException("FOUNDRY_AGENT_NAME environment variable is not set.");
var agentVersion = "1";
var isolationKey = "my-isolation-key";

Console.WriteLine($"Agent: {agentName}, version: {agentVersion}");

// see https://learn.microsoft.com/ja-jp/agent-framework/agents/providers/microsoft-foundry?pivots=programming-language-csharp#foundry-agent-versioned
AIProjectClient aiProjectClient = new(projectEndpoint, new DefaultAzureCredential());
ProjectsAgentRecord record = await aiProjectClient.AgentAdministrationClient.GetAgentAsync(agentName);
FoundryAgent agent = aiProjectClient.AsAIAgent(record);

Console.WriteLine(await agent.RunAsync("Hello, what can you help me with?"));

//AgentSession session = await agent.CreateSessionAsync();

//Console.WriteLine(await agent.RunAsync("Hello, what can you help me with?", session));
//Console.WriteLine(await agent.RunAsync("What is the captial of Japan?", session));
