using Microsoft.Agents.AI.DurableTask;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;

namespace MafFunctionApp;

public static class Function1
{
    [Function(nameof(Function1))]
    public static async Task<string> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var message = context.GetInput<string>();

        var agent = context.GetAgent("HelloAgent");
        var session = await agent.CreateSessionAsync();

        var response = await agent.RunAsync<string>(message, session);
        return response.Text;
    }
}