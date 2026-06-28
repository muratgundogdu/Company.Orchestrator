using System.Text.Json;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Models;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure.StepHandlers;

/// <summary>
/// Iterates over file objects in a JSON array (e.g. from folder.list-files).
/// Routes to <c>LoopStepId</c> once per file; after the last file routes to <c>CompletedStepId</c>.
/// </summary>
public sealed class ForEachFileStepHandler : IStepHandler
{
    private readonly ILogger<ForEachFileStepHandler> _logger;

    public string HandlerType => "foreach.file";

    public ForEachFileStepHandler(ILogger<ForEachFileStepHandler> logger)
    {
        _logger = logger;
    }

    public Task<StepResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken = default)
    {
        var stepDef  = context.StepDefinition;
        var config   = stepDef.Config;
        var stepId   = stepDef.Id;

        var collectionVar = GetString(config, "collectionVariable");
        var fileVarName   = GetString(config, "fileVariable",   "currentFile");
        var indexVarName  = GetString(config, "indexVariable",  "currentIndex");

        var loopStepId      = stepDef.LoopStepId;
        var completedStepId = stepDef.CompletedStepId;

        var itemsKey = $"__foreachfile_{stepId}_items__";
        var indexKey = $"__foreachfile_{stepId}_index__";

        List<string> files;
        if (!context.Variables.ContainsKey(itemsKey))
        {
            var collectionRaw = ResolveCollectionVariable(context, collectionVar);
            files = ResolveFiles(collectionRaw);

            context.Variables[itemsKey] = JsonSerializer.Serialize(files);
            context.Variables[indexKey] = 0;

            _logger.LogInformation(
                "ForEachFile {StepId}: collectionVariable='{CollectionVar}', file count={Count}",
                stepId, collectionVar, files.Count);
        }
        else
        {
            var raw = context.Variables[itemsKey]?.ToString() ?? "[]";
            files = JsonSerializer.Deserialize<List<string>>(raw) ?? [];
        }

        var index = Convert.ToInt32(context.Variables.GetValueOrDefault(indexKey, 0));

        ClearFileFieldVariables(context, fileVarName);

        if (index >= files.Count)
        {
            context.Variables.Remove(itemsKey);
            context.Variables.Remove(indexKey);
            context.Variables.Remove(fileVarName);
            context.Variables.Remove(indexVarName);

            _logger.LogInformation(
                "ForEachFile {StepId}: completed — processed {Count} file(s) → completedStepId {CompletedStepId}",
                stepId, files.Count, completedStepId ?? "(none)");

            return Task.FromResult(StepResult.Ok(
                output: new Dictionary<string, object>
                {
                    ["nextStepId"]       = completedStepId ?? "",
                    ["foreachCompleted"] = true,
                    ["foreachItemCount"] = files.Count,
                },
                outputData: $"ForEachFile complete — {files.Count} file(s) processed. → {completedStepId ?? "end"}"));
        }

        var currentFile = files[index];
        var filePath    = TryGetFilePath(currentFile);

        _logger.LogInformation(
            "ForEachFile {StepId}: iteration {Index}/{Total}, file path={FilePath} → loopStepId {LoopStepId}",
            stepId, index, files.Count, filePath, loopStepId ?? "(none)");

        context.Variables[fileVarName]   = currentFile;
        context.Variables[indexVarName]  = index;
        context.Variables[indexKey]      = index + 1;
        ExposeFileFieldVariables(context, fileVarName, currentFile);

        return Task.FromResult(StepResult.Ok(
            output: new Dictionary<string, object>
            {
                ["nextStepId"]       = loopStepId ?? "",
                [fileVarName]        = currentFile,
                [indexVarName]       = index,
                ["foreachCompleted"] = false,
            },
            outputData: $"ForEachFile [{index + 1}/{files.Count}] path='{filePath}' → {loopStepId ?? "end"}"));
    }

    private static string ResolveCollectionVariable(WorkflowContext context, string collectionExpr)
    {
        var expr = collectionExpr.Trim();
        if (string.IsNullOrEmpty(expr))
            return string.Empty;

        if (expr.StartsWith("{{", StringComparison.Ordinal) && expr.EndsWith("}}", StringComparison.Ordinal))
            return context.Interpolate(expr);

        if (context.Variables.TryGetValue(expr, out var raw))
            return VariableToString(raw);

        return context.Interpolate($"{{{{{expr}}}}}");
    }

    private static List<string> ResolveFiles(string resolved)
    {
        if (string.IsNullOrWhiteSpace(resolved))
            return [];

        var trimmed = resolved.Trim();
        if (!trimmed.StartsWith('['))
            return [trimmed];

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [trimmed];

            return doc.RootElement.EnumerateArray()
                .Select(ElementToFileJson)
                .ToList();
        }
        catch
        {
            return [trimmed];
        }
    }

    private static string ElementToFileJson(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            _                    => element.GetRawText()
        };

    private static void ExposeFileFieldVariables(WorkflowContext context, string fileVarName, string fileJson)
    {
        if (string.IsNullOrWhiteSpace(fileJson) || !fileJson.TrimStart().StartsWith('{'))
            return;

        try
        {
            using var doc = JsonDocument.Parse(fileJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var fieldKey = $"{fileVarName}.{prop.Name}";
                context.Variables[fieldKey] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? "",
                    JsonValueKind.Null   => "",
                    _                    => prop.Value.GetRawText()
                };
            }
        }
        catch
        {
            // Non-object file entry — field variables are not exposed.
        }
    }

    private static void ClearFileFieldVariables(WorkflowContext context, string fileVarName)
    {
        var prefix = fileVarName + ".";
        var keys = context.Variables.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var key in keys)
            context.Variables.Remove(key);
    }

    private static string TryGetFilePath(string fileJson)
    {
        if (string.IsNullOrWhiteSpace(fileJson) || !fileJson.TrimStart().StartsWith('{'))
            return fileJson;

        try
        {
            using var doc = JsonDocument.Parse(fileJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return fileJson;

            if (doc.RootElement.TryGetProperty("fullPath", out var fullPath) &&
                fullPath.ValueKind == JsonValueKind.String)
                return fullPath.GetString() ?? fileJson;

            if (doc.RootElement.TryGetProperty("FullPath", out var pascalPath) &&
                pascalPath.ValueKind == JsonValueKind.String)
                return pascalPath.GetString() ?? fileJson;
        }
        catch
        {
            // fall through
        }

        return fileJson;
    }

    private static string GetString(
        Dictionary<string, object> config,
        string key,
        string fallback = "")
    {
        if (!config.TryGetValue(key, out var raw)) return fallback;
        if (raw is JsonElement el) return el.GetString() ?? fallback;
        return raw?.ToString() ?? fallback;
    }

    private static string VariableToString(object? val)
    {
        if (val is JsonElement je)
        {
            return je.ValueKind switch
            {
                JsonValueKind.String  => je.GetString() ?? "",
                JsonValueKind.Number  => je.GetRawText(),
                JsonValueKind.True    => "true",
                JsonValueKind.False   => "false",
                JsonValueKind.Null    => "",
                JsonValueKind.Array   => je.GetRawText(),
                JsonValueKind.Object  => je.GetRawText(),
                _                     => je.GetRawText()
            };
        }

        return val?.ToString() ?? "";
    }
}
