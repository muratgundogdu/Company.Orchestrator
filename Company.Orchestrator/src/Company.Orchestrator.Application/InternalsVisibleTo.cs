using System.Runtime.CompilerServices;

// Grants the Infrastructure assembly access to internal members of WorkflowContext
// (MergeVariables, RegisterArtifacts, RegisterStepOutput, StepDefinition.set).
// Step handlers in Infrastructure are NOT granted this access because they live
// in a different assembly and should only use public WorkflowContext members.
[assembly: InternalsVisibleTo("Company.Orchestrator.Infrastructure")]
