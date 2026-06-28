# Company.Orchestrator

.NET 8 Clean Architecture — Workflow Orchestration Engine

## Solution Structure

```
Company.Orchestrator/
├── src/
│   ├── Company.Orchestrator.Domain          # Entities, enums, base classes
│   ├── Company.Orchestrator.Application     # Interfaces, DTOs, service contracts
│   ├── Company.Orchestrator.Infrastructure  # EF Core, repositories, step handlers, WorkflowEngine
│   ├── Company.Orchestrator.Api             # ASP.NET Core Web API (Swagger UI at /)
│   └── Company.Orchestrator.Worker          # Background job poller
```

## Quick Start

### 1. Prerequisites
- .NET 8 SDK
- SQL Server (local or Docker)

### 2. Configure connection string
Edit `appsettings.Development.json` in both `Api` and `Worker` projects:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=OrchestratorDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### 3. Run Migrations
```bash
# From solution root
dotnet ef migrations add InitialCreate \
  --project src/Company.Orchestrator.Infrastructure \
  --startup-project src/Company.Orchestrator.Api

dotnet ef database update \
  --project src/Company.Orchestrator.Infrastructure \
  --startup-project src/Company.Orchestrator.Api
```
> Migrations also auto-apply on startup in development.

### 4. Run the API
```bash
dotnet run --project src/Company.Orchestrator.Api
# Swagger UI → http://localhost:5000
```

### 5. Run the Worker
```bash
dotnet run --project src/Company.Orchestrator.Worker
```

---

## Usage Example

### Create a process definition
```http
POST /api/process-definitions
{
  "name": "OnboardingFlow",
  "description": "New user onboarding",
  "category": "HR"
}
```

### Create a version with a workflow definition
```http
POST /api/process-definitions/{definitionId}/versions
{
  "jsonDefinition": {
    "name": "OnboardingFlow",
    "steps": [
      {
        "id": "send-welcome",
        "name": "Send Welcome Email",
        "type": "Email",
        "config": {
          "to": "{{email}}",
          "subject": "Welcome!",
          "body": "Hello {{name}}, welcome aboard."
        },
        "nextStepId": "call-api"
      },
      {
        "id": "call-api",
        "name": "Notify HR System",
        "type": "ApiCall",
        "config": {
          "url": "https://hrapi.internal/onboard",
          "method": "POST",
          "body": "{\"userId\": \"{{userId}}\"}"
        },
        "nextStepId": "delay"
      },
      {
        "id": "delay",
        "name": "Wait 2 seconds",
        "type": "Delay",
        "config": { "seconds": 2 }
      }
    ]
  },
  "changeNotes": "Initial version"
}
```
> Note: `jsonDefinition` must be a JSON string. Serialize the object before sending.

### Publish the version
```http
POST /api/process-definitions/{definitionId}/versions/{versionId}/publish
```

### Start a process instance
```http
POST /api/process-instances/start
{
  "processDefinitionId": "...",
  "correlationId": "user-123",
  "inputData": "{\"email\": \"user@example.com\", \"name\": \"Alice\", \"userId\": \"u-42\"}",
  "triggeredBy": "Manual"
}
```
A `Job` is queued immediately. The **Worker** picks it up within its polling interval (5s default) and runs the workflow.

---

## Step Handler Types

| Type        | Config Keys                                                             |
|-------------|-------------------------------------------------------------------------|
| `Email`     | `to`, `subject`, `body`                                                 |
| `ApiCall`   | `url`, `method`, `body`, `headers`                                      |
| `Delay`     | `seconds`                                                               |
| `Condition` | `variable`, `operator`, `value`, `trueStepId`, `falseStepId`           |
| `SqlQuery`  | `connectionName`, `query`, `outputVariable`                             |

**Variable interpolation**: use `{{variableName}}` in any config string value.

**Condition operators**: `equals`, `notEquals`, `contains`, `isNull`, `isNotNull`, `greaterThan`, `lessThan`

---

## Adding a Custom Step Handler

1. Implement `IStepHandler` in `Company.Orchestrator.Infrastructure.StepHandlers`
2. Register in `DependencyInjection.cs`:
   ```csharp
   services.AddScoped<IStepHandler, MyCustomStepHandler>();
   ```
That's it. The `WorkflowEngine` discovers handlers by `HandlerType` at runtime.
