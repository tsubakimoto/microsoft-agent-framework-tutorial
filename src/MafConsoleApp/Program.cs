using System.ComponentModel;

using Azure.AI.Projects;
using Azure.Identity;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var endpoint = configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT");
var deploymentName = configuration["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? "gpt-4o-mini";

var aiProjectClient = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());

#region Simple

// https://learn.microsoft.com/ja-jp/agent-framework/get-started/your-first-agent
Console.WriteLine("*** Simple ***");
AIAgent agent1 = aiProjectClient
    .AsAIAgent(
        model: deploymentName,
        instructions: "You are a friendly assistant. Keep your answers brief.",
        name: "HelloAgent");

Console.WriteLine(await agent1.RunAsync("What is the largest city in France?"));
Console.WriteLine();

#endregion

#region Tools

// https://learn.microsoft.com/ja-jp/agent-framework/get-started/add-tools
Console.WriteLine("*** Tools ***");
AIAgent agent2 = aiProjectClient
    .AsAIAgent(
        model: deploymentName,
        instructions: "You are a helpful assistant.",
        tools: [AIFunctionFactory.Create(GetWeather)]);

Console.WriteLine(await agent2.RunAsync("What is the weather like in Amsterdam?"));
Console.WriteLine();

#endregion

#region Multi-turn

// https://learn.microsoft.com/ja-jp/agent-framework/get-started/multi-turn
Console.WriteLine("*** Multi-turn conversation ***");
AIAgent agent3 = aiProjectClient
    .AsAIAgent(
        model: deploymentName,
        instructions: "You are a friendly assistant. Keep your answers brief.",
        name: "ConversationAgent");

AgentSession session3 = await agent3.CreateSessionAsync();
Console.WriteLine(await agent3.RunAsync("My name is Alice and I love hiking.", session3));
Console.WriteLine(await agent3.RunAsync("What do you remember about me?", session3));

#endregion

#region Tools

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";

#endregion