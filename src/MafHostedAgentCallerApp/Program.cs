using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Core;
using Azure.Identity;

using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var endpoint = configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set.");
var agentName = configuration["FOUNDRY_AGENT_NAME"]
    ?? throw new InvalidOperationException("FOUNDRY_AGENT_NAME environment variable is not set.");
var agentVersion = "1";
string? isolationKey = "my-isolation-key";

Console.WriteLine($"Agent: {agentName}, version: {agentVersion}");

var credential = new AzureDeveloperCliCredential();
var projectClient = new AIProjectClient(new Uri(endpoint), credential);





var sessionResult = projectClient.AgentAdministrationClient.CreateSession(
    agentName,
    new VersionRefIndicator(agentVersion));
//userIsolationKey: isolationKey);
var session = sessionResult.Value;

Console.WriteLine($"Created session: {session.AgentSessionId}");

try
{
    AccessToken token = credential.GetToken(
        new TokenRequestContext(["https://ai.azure.com/.default"]));

    using var http = new HttpClient();
    http.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token.Token);
    http.DefaultRequestHeaders.Add("Foundry-Features", "HostedAgents=V1Preview");
    if (!string.IsNullOrWhiteSpace(isolationKey))
    {
        http.DefaultRequestHeaders.Add("x-ms-user-isolation-key", isolationKey);
    }

    var responsesEndpoint = new Uri(
        $"{endpoint.TrimEnd('/')}/agents/{Uri.EscapeDataString(agentName)}/endpoint/protocols/openai/responses?api-version=v1");

    var requestBody = new JsonObject
    {
        ["input"] = "Hello, what can you help me with?",
        ["stream"] = false,
        ["agent_session_id"] = session.AgentSessionId,
    };

    using var requestContent = new StringContent(
        requestBody.ToJsonString(),
        Encoding.UTF8,
        "application/json");
    var responseHttp = await http.PostAsync(responsesEndpoint, requestContent);

    var responseJson = await responseHttp.Content.ReadAsStringAsync();
    if (!responseHttp.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"Responses call failed: {(int)responseHttp.StatusCode} {responseHttp.ReasonPhrase}\n{responseJson}");
    }

    using var responseDoc = JsonDocument.Parse(responseJson);
    Console.WriteLine($"Response: {ExtractOutputText(responseDoc.RootElement)}");
}
finally
{
    projectClient.AgentAdministrationClient.DeleteSession(
        agentName,
        session.AgentSessionId);
    //userIsolationKey: isolationKey);
}

static string ExtractOutputText(JsonElement root)
{
    if (root.TryGetProperty("output_text", out var outputText) && outputText.ValueKind == JsonValueKind.String)
    {
        return outputText.GetString() ?? string.Empty;
    }

    if (root.TryGetProperty("output", out var outputItems) && outputItems.ValueKind == JsonValueKind.Array)
    {
        foreach (JsonElement outputItem in outputItems.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out var contentItems) || contentItems.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement contentItem in contentItems.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var textValue) && textValue.ValueKind == JsonValueKind.String)
                {
                    return textValue.GetString() ?? string.Empty;
                }
            }
        }
    }

    return root.ToString();
}





#if false
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Identity;

using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var projectEndpoint = new Uri(configuration["FOUNDRY_PROJECT_ENDPOINT"]
    ?? throw new InvalidOperationException("FOUNDRY_PROJECT_ENDPOINT environment variable is not set."));
var name = configuration["FOUNDRY_AGENT_NAME"]
    ?? throw new InvalidOperationException("FOUNDRY_AGENT_NAME environment variable is not set.");
var version = "1";
var isolationKey = "my-isolation-key";

Console.WriteLine($"[Search] agent: {name}, version: {version}");

// see https://learn.microsoft.com/ja-jp/agent-framework/agents/providers/microsoft-foundry?pivots=programming-language-csharp#foundry-agent-versioned
AIProjectClient aiProjectClient = new(projectEndpoint, new DefaultAzureCredential());
ProjectsAgentVersion? agentVersion = null;

await foreach (var ag in aiProjectClient.AgentAdministrationClient.GetAgentVersionsAsync(name))
{
    if (ag.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Founded.");
        agentVersion = ag;
        break;
    }
}

if (agentVersion is null)
{
    Console.WriteLine("Agent is not found.");
    return;
}

var definition = agentVersion.Definition as DeclarativeAgentDefinition;
var model = definition?.Model ?? "";
var instructions = definition?.Instructions ?? "";


ProjectResponsesClient responsesClient = aiProjectClient.ProjectOpenAIClient
    .GetProjectResponsesClientForAgent(new AgentReference(agentVersion.Name, agentVersion.Version));
await foreach (var update in responsesClient.CreateResponseStreamingAsync("Hello, what can you help me with?"))
{
    Console.WriteLine();
}

//var openAiClient = aiProjectClient.GetProjectOpenAIClient(new ProjectOpenAIClientOptions()
//{
//    AgentName = agentVersion.Name,
//    ApiVersion = agentVersion.Version
//});
//var responsesClient = openAiClient.GetProjectResponsesClientForAgentEndpoint(agentVersion.Name);
//var response = await responsesClient.CreateResponseAsync("Hello, what can you help me with?");
//Console.WriteLine(response.Value);


//ProjectsAgentVersion version = await aiProjectClient.AgentAdministrationClient.GetAgentVersionAsync(agentName, agentVersion);
//FoundryAgent agent = aiProjectClient.AsAIAgent(version);

//Console.WriteLine(await agent.RunAsync("Hello, what can you help me with?"));

//AgentSession session = await agent.CreateSessionAsync();
//Console.WriteLine(await agent.RunAsync("Hello, what can you help me with?", session));
//Console.WriteLine(await agent.RunAsync("What is the captial of Japan?", session));
#endif