#:package Azure.Identity@1.*
#:package Azure.AI.Projects@2.0.1
#:package Azure.AI.Projects.Agents@2.1.0-beta.3

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.Core;
using Azure.Identity;

var endpoint = "https://{xxx}.services.ai.azure.com/api/projects/{yyy}";
var agentName = "{zzz}";
var agentVersion = "1";
string? isolationKey = "my-isolation-key";

Console.WriteLine($"Agent: {agentName}, version: {agentVersion}");

var credential = new DefaultAzureCredential();
var projectClient = new AIProjectClient(new Uri(endpoint), credential);

var sessionResult = projectClient.AgentAdministrationClient.CreateSession(
	agentName,
	new VersionRefIndicator(agentVersion),
	userIsolationKey: isolationKey);
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
		session.AgentSessionId,
		userIsolationKey: isolationKey);
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
