# MafInvocationsProxyApp

Azure Functions HTTP-trigger proxy that bridges **API key authentication** to **Microsoft Entra ID** for the Foundry hosted-agent `invocations` endpoint.

## Why this exists

The Foundry Agents service only accepts `Authorization: Bearer <Entra token>` — API keys are not supported. This proxy lets callers use a standard Azure Functions function key while the function itself acquires an Entra token via Managed Identity and forwards the request.

## Caller contract

| Item | Value |
|------|-------|
| Method | `POST` |
| Path | `/api/invocations` |
| Auth | Function key via `x-functions-key` header **or** `?code=<key>` query param |
| Body | Same raw body you would send to the Foundry invocations endpoint |
| Content-Type | Pass through (e.g. `application/json`) |

Additional query parameters (e.g. `agent_session_id`) are forwarded to Foundry. The `code` parameter is stripped before forwarding.

### Example

```bash
curl -X POST \
  "https://<func-app>.azurewebsites.net/api/invocations?agent_session_id=<sid>" \
  -H "x-functions-key: <function-key>" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"Hello"}]}'
```

## Application settings

| Setting | Description |
|---------|-------------|
| `FOUNDRY_INVOCATIONS_ENDPOINT` | Full Foundry invocations URL including `?api-version=v1` |

### Example value

```
https://ai-account-xxxx.services.ai.azure.com/api/projects/<project>/agents/<agent>/endpoint/protocols/invocations?api-version=v1
```

## Local development

1. Copy `local.settings.default.json` to `local.settings.json` and fill in `FOUNDRY_INVOCATIONS_ENDPOINT`.
2. Sign in with Azure CLI (`az login`) — `DefaultAzureCredential` uses your CLI credentials locally.
3. Run with `func start` or F5 in Visual Studio.

## Deploy with Azure Developer CLI (azd)

### Prerequisites

- [Azure Developer CLI](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)
- An existing Foundry AI Services resource and its resource group

### Steps

```bash
# 1. Initialize the environment (first time only)
azd init

# 2. Set required environment variables
azd env set AZURE_AI_ACCOUNT_NAME  <ai-services-resource-name>
azd env set AZURE_AI_RESOURCE_GROUP <resource-group-of-ai-services>

# 3. Set the Foundry invocations endpoint URL
azd env set FOUNDRY_INVOCATIONS_ENDPOINT \
  "https://<account>.services.ai.azure.com/api/projects/<project>/agents/<agent>/endpoint/protocols/invocations?api-version=v1"

# 4. Deploy (provision infrastructure + deploy function code)
azd up
```

`azd up` will:
1. Create a resource group, Storage Account, App Service Plan (Consumption), and Function App
2. Assign `Azure AI Developer` role on the specified AI Services resource to the Function App's managed identity
3. Build and zip-deploy the function code

### Get the function key after deployment

```bash
az functionapp keys list \
  --name $(azd env get-value AZURE_FUNCTION_APP_NAME) \
  --resource-group $(azd env get-value AZURE_RESOURCE_GROUP) \
  --query functionKeys -o json
```

## Manual deployment

Deploy to Azure Functions (Consumption or any plan). Assign the Function App's **system-assigned managed identity** the **Azure AI Developer** role (`64702f94-c441-49e6-a78b-ef80e0188fee`) on the Foundry AI Services resource.

> The minimum required role is `Azure AI Inference Deployment Operator` (`3afb7f49-54cb-416e-8c09-6dc049efa503`). Use `Azure AI Developer` if you also need read access to project resources.

### Role assignment (CLI)

```bash
AI_ACCOUNT_ID=$(az cognitiveservices account show \
  --name <account-name> --resource-group <rg> \
  --query id -o tsv)

FUNC_PRINCIPAL=$(az functionapp identity show \
  --name <func-app-name> --resource-group <rg> \
  --query principalId -o tsv)

az role assignment create \
  --assignee "$FUNC_PRINCIPAL" \
  --role "Azure AI Developer" \
  --scope "$AI_ACCOUNT_ID"
