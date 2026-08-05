using GitHub.Copilot;

using Microsoft.Agents.AI;

using Squad.Agents.AI;

namespace MafSquadAgentApp;

public sealed class SquadAgent : DelegatingAIAgent

{
    public SquadAgent(SquadAgentOptions options)
        : base(BuildInnerAgent(options))    // ← inner AIAgent passed to DelegatingAIAgent
    {
    }
    // DelegatingAIAgent forwards RunAsync, RunStreamingAsync, CreateSessionAsync
    // to whatever inner AIAgent we hand the base ctor. SquadAgent doesn't override
    // any of that — the Copilot-backed agent does the work; we just expose the seam.
    private static AIAgent BuildInnerAgent(SquadAgentOptions options)
    {
        // 1. Copilot SDK client, pointed at the Squad team root
        var client = new CopilotClient(/* CLI path, env, token, etc. */);
        // 2. Tell the CLI to auto-discover the .squad/ folder so it picks up
        //    team agents, skills, instructions, and MCP servers at session start.
        var teamRoot = options.SquadFolderPath;
        var squadConfigDir = Path.Combine(teamRoot, ".squad");

        var sessionConfig = new SessionConfig
        {
            WorkingDirectory = teamRoot,
            ConfigDirectory = squadConfigDir,
            EnableConfigDiscovery = true,
            OnPermissionRequest = PermissionHandler.ApproveAll,
        };

        // 3. Lift the Copilot client into a MAF AIAgent — this is the inner
        //    agent that DelegatingAIAgent will forward every call to.
        return client.AsAIAgent(sessionConfig, name: options.AgentName ?? "Squad");
    }
}