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

## Deployment

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
