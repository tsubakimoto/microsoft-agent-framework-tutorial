see https://github.com/microsoft-foundry/foundry-samples/tree/main/samples/csharp/hosted-agents/agent-framework/simple-agent.

**Deploy**:  
```
azd ai agent init -m agent.manifest.yaml --deploy-mode code
azd provision
azd deploy
```

**Test**:  
```
azd ai agent invoke "Hello! What can you help me with?"
azd ai agent show maf-hosted-agent-responses-app
azd ai agent monitor --follow
```