# AI abstraction design

## Purpose

- Support multiple “AI” backends for generating incident summaries and investigation steps without coupling the rest of the app to a specific provider.
- In production, **only Mock** is allowed; no paid APIs or hosted LLMs.

## Providers

- **Mock**: Returns fixed or pattern-based text. No external calls. Used by default and **required** in production.
- **Ollama**: Calls a local Ollama instance (e.g. `http://localhost:11434`). Allowed **only in Development**. If Ollama is unavailable, the system falls back to Mock.

## Configuration

- `AI:Provider`: `"Mock"` or `"Ollama"`. In production this is forced to `Mock` at startup regardless of config.
- `AI:AllowProviderSwitch`: In production, switching provider at runtime is disabled (control endpoints are not registered).

## SwitchableAIService

- The API uses a single `IAIService` implementation that delegates to Mock or Ollama based on `IAIProviderState.Provider`.
- Timing and success/failure of AI calls are logged (no prompts or full responses in production logs).

## Production rules

- Mock is the only provider.
- No AI toggles in the UI (read-only dashboard).
- No PATCH to `/api/settings/ai` in production (endpoint not registered).
