# Agent skills

Project-specific workflows for coding agents working on `dotnet-serverless`.
Each skill is a folder with a `SKILL.md`; load the one whose description matches
the task.

| Skill | Use it when |
|-------|-------------|
| [`add-endpoint`](add-endpoint/SKILL.md) | Adding a route, request/response model, validation, OpenAPI metadata, or extracting handler logic into a service |
| [`add-configuration`](add-configuration/SKILL.md) | Adding an options class, appsettings key, environment variable, or reading configuration |
| [`verify-api`](verify-api/SKILL.md) | Building, running, smoke-testing, or debugging the API locally or in Docker |

All skills assume the repository root as the working directory. They point to
`docs/` for background instead of restating the long-form guides, and they
preserve the template's serverless constraints: no durable local state, no
background work in-process, no local files, and no unrequested infrastructure.
