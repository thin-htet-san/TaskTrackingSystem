# OpenRouter translation provider

The Web API uses `OpenRouterContentTranslationService` when
`Translation:Provider` is `OpenRouter`. The Blazor client calls the Web API
and never receives or stores the OpenRouter API key.

## Local development with user-secrets

Run these commands from the solution directory. Replace `YOUR_KEY` locally;
never commit the value.

```powershell
dotnet user-secrets set "Translation:Provider" "OpenRouter" --project TaskTrackingSystem.WebApi
dotnet user-secrets set "Translation:Endpoint" "https://openrouter.ai/api/v1/chat/completions" --project TaskTrackingSystem.WebApi
dotnet user-secrets set "Translation:Model" "openrouter/free" --project TaskTrackingSystem.WebApi
dotnet user-secrets set "Translation:ApiKey" "YOUR_KEY" --project TaskTrackingSystem.WebApi
```

Restart the Web API after changing secrets.

## Deployment environment variables

Configure these variables on the Web API host only:

```text
Translation__Provider=OpenRouter
Translation__Endpoint=https://openrouter.ai/api/v1/chat/completions
Translation__Model=openrouter/free
Translation__ApiKey=YOUR_KEY
```

The key belongs in `Translation:ApiKey` through user-secrets or the
deployment environment. It must not be placed in the Blazor WebApp
configuration, browser storage, source control, or client-side code.

The provider sends `Authorization: Bearer {ApiKey}`, `HTTP-Referer:
https://huggingface.co`, and `X-Title: TaskTrackingSystem`. It parses
`choices[0].message.content` and returns controlled failures for invalid keys,
rate limits, empty responses, timeouts, and network errors.
