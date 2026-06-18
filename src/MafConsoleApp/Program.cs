using System.ComponentModel;

using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;

using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

using ModelContextProtocol.Client;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .Build();

var endpoint = configuration["AZURE_OPENAI_ENDPOINT"]
    ?? throw new InvalidOperationException("Set AZURE_OPENAI_ENDPOINT");
var deploymentName = configuration["AZURE_OPENAI_DEPLOYMENT_NAME"] ?? "gpt-4o-mini";

var aiProjectClient = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());

await SimpleAgentExample(aiProjectClient, deploymentName);
await ToolsAgentExample(aiProjectClient, deploymentName);
await RemoteMCPServerExample(aiProjectClient, deploymentName);
await MultiTurnAgentExample(aiProjectClient, deploymentName);
await MemoryAgentExample(aiProjectClient, deploymentName, endpoint);
await WorkflowExample();



#region Simple

static async Task SimpleAgentExample(AIProjectClient aiProjectClient, string deploymentName)
{
    // https://learn.microsoft.com/ja-jp/agent-framework/get-started/your-first-agent
    Console.WriteLine("*** Simple ***");
    AIAgent agent1 = aiProjectClient
        .AsAIAgent(
            model: deploymentName,
            instructions: "You are a friendly assistant. Keep your answers brief.",
            name: "HelloAgent");

    Console.WriteLine(await agent1.RunAsync("What is the largest city in France?"));
    Console.WriteLine();
}

#endregion

#region Tools

static async Task ToolsAgentExample(AIProjectClient aiProjectClient, string deploymentName)
{
    // https://learn.microsoft.com/ja-jp/agent-framework/get-started/add-tools
    Console.WriteLine("*** Tools ***");
    AIAgent agent2 = aiProjectClient
        .AsAIAgent(
            model: deploymentName,
            instructions: "You are a helpful assistant.",
            tools: [AIFunctionFactory.Create(GetWeather)]);

    Console.WriteLine(await agent2.RunAsync("What is the weather like in Amsterdam?"));
    Console.WriteLine();
}

[Description("Get the weather for a given location.")]
static string GetWeather([Description("The location to get the weather for.")] string location)
    => $"The weather in {location} is cloudy with a high of 15°C.";

#endregion

#region MCP Server

static async Task RemoteMCPServerExample(AIProjectClient aiProjectClient, string deploymentName)
{
    // https://learn.microsoft.com/ja-jp/agent-framework/agents/tools/hosted-mcp-tools?pivots=programming-language-csharp
    // https://learn.microsoft.com/ja-jp/azure/foundry/agents/how-to/tools/model-context-protocol?tabs=hosted-agents&pivots=csharp
    // https://github.com/modelcontextprotocol/csharp-sdk/blob/main/samples/QuickstartClient/Program.cs
    Console.WriteLine("*** Remote MCP Server ***");

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

    AIAgent agent = aiProjectClient
        .AsAIAgent(
            model: deploymentName,
            instructions: "You answer questions by searching the Microsoft Learn content only.",
            name: "MicrosoftLearnAgent",
            tools: agentTools);

    // You can then invoke the agent like any other AIAgent.
    Console.WriteLine(await agent.RunAsync("Please summarize the Azure AI Agent documentation related to MCP Tool calling?"));
}

#endregion

#region Multi-turn

static async Task MultiTurnAgentExample(AIProjectClient aiProjectClient, string deploymentName)
{
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
    Console.WriteLine();
}

#endregion

#region Memory

static async Task MemoryAgentExample(AIProjectClient aiProjectClient, string deploymentName, string endpoint)
{
    // https://learn.microsoft.com/ja-jp/agent-framework/get-started/memory
    // https://learn.microsoft.com/en-us/agent-framework/integrations/chat-history-memory-provider
    Console.WriteLine("*** Memory ***");
    VectorStore vectorStore = new InMemoryVectorStore(new InMemoryVectorStoreOptions()
    {
        EmbeddingGenerator = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
            .GetEmbeddingClient("text-embedding-3-large")
            .AsIEmbeddingGenerator()
    });
    AIAgent agent4 = aiProjectClient
        .AsAIAgent(options: new ChatClientAgentOptions
        {
            Name = "MemoryAgent",
            ChatOptions = new ChatOptions
            {
                ModelId = deploymentName,
                Instructions = "You are a helpful assistant.",
            },
            AIContextProviders = [new ChatHistoryMemoryProvider(
            vectorStore,
            collectionName: "ChatHistory",
            vectorDimensions: 3072,
            session => new ChatHistoryMemoryProvider.State(
                storageScope: new() { UserId = "user-123", SessionId = Guid.NewGuid().ToString() },
                searchScope: new() { UserId = "user-123" }))]
        });

    // Start a session and interact with the agent
    AgentSession session4_1 = await agent4.CreateSessionAsync();
    Console.WriteLine(await agent4.RunAsync("I prefer window seats on flights.", session4_1));

    // Start a new session - the agent can recall the user's preference
    AgentSession session4_2 = await agent4.CreateSessionAsync();
    Console.WriteLine(await agent4.RunAsync("Book me a flight to Seattle.", session4_2));

    Console.WriteLine();
}

#endregion

#region Workflows

static async Task WorkflowExample()
{
    // https://learn.microsoft.com/ja-jp/agent-framework/get-started/workflows
    Console.WriteLine("*** Workflows ***");

    // Step 1: Convert text to uppercase
    Func<string, string> uppercaseFunc = s => s.ToUpperInvariant();
    var uppercase = uppercaseFunc.BindAsExecutor("UppercaseExecutor");

    // Step 2: Reverse the string and yield output
    ReverseTextExecutor reverse = new();

    // Step3: Build the workflow
    WorkflowBuilder builder = new(uppercase);
    builder.AddEdge(uppercase, reverse).WithOutputFrom(reverse);
    var workflow = builder.Build();

    await using Run run = await InProcessExecution.RunAsync(workflow, "Hello, World!");
    foreach (WorkflowEvent evt in run.NewEvents)
    {
        if (evt is ExecutorCompletedEvent executorComplete)
        {
            Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
        }
    }
    Console.WriteLine();
}

class ReverseTextExecutor() : Executor<string, string>("ReverseTextExecutor")
{
    public override ValueTask<string> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(string.Concat(message.Reverse()));
    }
}

#endregion
