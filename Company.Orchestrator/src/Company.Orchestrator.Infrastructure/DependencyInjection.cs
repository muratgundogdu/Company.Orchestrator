using Company.Orchestrator.Application.Artifacts;
using Company.Orchestrator.Application.Capabilities;
using Company.Orchestrator.Application.Capabilities.Browser;
using Company.Orchestrator.Application.Capabilities.Excel;
using Company.Orchestrator.Application.Capabilities.File;
using Company.Orchestrator.Application.Capabilities.Folder;
using Company.Orchestrator.Application.Capabilities.Mail;
using Company.Orchestrator.Application.Common.Interfaces;
using Company.Orchestrator.Application.Services;
using Company.Orchestrator.Infrastructure.Artifacts;
using Company.Orchestrator.Application.Triggers;
using Company.Orchestrator.Infrastructure.BrowserPicker;
using Company.Orchestrator.Infrastructure.Expressions;
using Company.Orchestrator.Infrastructure.Capabilities.Browser;
using Company.Orchestrator.Infrastructure.Capabilities.Excel;
using Company.Orchestrator.Infrastructure.Capabilities.File;
using Company.Orchestrator.Infrastructure.Capabilities.Folder;
using Company.Orchestrator.Infrastructure.Capabilities.Mail;
using Company.Orchestrator.Infrastructure.Capabilities.Registry;
using Company.Orchestrator.Infrastructure.Persistence;
using Company.Orchestrator.Infrastructure.Repositories;
using Company.Orchestrator.Infrastructure.Security;
using Company.Orchestrator.Infrastructure.Services;
using Company.Orchestrator.Infrastructure.StepHandlers;
using Company.Orchestrator.Infrastructure.Triggers;
using Company.Orchestrator.Infrastructure.WorkflowEngine;
using Company.Orchestrator.Infrastructure.Workers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Company.Orchestrator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ---- Persistence ----
        services.AddDbContext<OrchestratorDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(OrchestratorDbContext).Assembly.FullName)));

        // ---- Unit of Work & Repositories ----
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IProcessDefinitionRepository, ProcessDefinitionRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IProcessInstanceRepository, ProcessInstanceRepository>();
        services.AddScoped<IArtifactRepository, ArtifactRepository>();
        services.AddScoped<ITriggerRepository, TriggerRepository>();
        services.AddScoped<ITriggerEventRepository, TriggerEventRepository>();
        services.AddScoped<ICredentialRepository, CredentialRepository>();

        // ---- Credential Vault ----
        var dataProtectionKeysPath = DataProtectionKeyPath.Resolve(configuration);
        Directory.CreateDirectory(dataProtectionKeysPath);
        services.AddDataProtection()
            .SetApplicationName(DataProtectionKeyPath.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddScoped<ICredentialResolver, CredentialResolver>();

        // ---- Application Services ----
        services.AddScoped<IProcessDefinitionService, ProcessDefinitionService>();
        services.AddScoped<IProcessInstanceService, ProcessInstanceService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<ITriggerService, TriggerService>();
        services.AddScoped<ICredentialService, CredentialService>();
        services.AddScoped<IWorkerService, WorkerService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IWorkerHeartbeatWriter, WorkerHeartbeatWriter>();
        services.AddSingleton<IWorkerIdentityProvider, WorkerIdentityProvider>();

        // ---- Artifact Store (content layer, singleton for shared file handles) ----
        services.AddSingleton<IArtifactStore, LocalFileArtifactStore>();

        // ---- Capability Registry (singleton — one registry, all capabilities registered at startup) ----
        services.AddSingleton<ICapabilityRegistry>(sp =>
        {
            var registry = new CapabilityRegistry();

            // File capability (fully functional)
            var fileImpl = new FileCapabilityImpl(
                sp.GetRequiredService<IArtifactStore>(),
                sp.GetRequiredService<ILogger<FileCapabilityImpl>>());
            registry.Register<IFileCapability>(fileImpl);

            // Mail capability (SMTP backed, or simulation if Host not configured)
            var mailImpl = new MailCapabilityImpl(
                configuration,
                sp.GetRequiredService<IArtifactStore>(),
                sp.GetRequiredService<ILogger<MailCapabilityImpl>>());
            registry.Register<IMailCapability>(mailImpl);

            // Browser capability — Playwright/Chromium. Requires one-time setup:
            //   dotnet tool install --global Microsoft.Playwright.CLI
            //   playwright install chromium
            var browserImpl = new BrowserCapabilityImpl(
                sp.GetRequiredService<IArtifactStore>(),
                sp.GetRequiredService<ILogger<BrowserCapabilityImpl>>());
            registry.Register<IBrowserCapability>(browserImpl);

            // SharedFolder capability — direct file system access, UNC paths, future impersonation
            var sharedFolderImpl = new SharedFolderCapabilityImpl(
                sp.GetRequiredService<IArtifactStore>(),
                sp.GetRequiredService<ILogger<SharedFolderCapabilityImpl>>());
            registry.Register<ISharedFolderCapability>(sharedFolderImpl);

            // Excel capability (ClosedXML — full .xlsx read/write + CSV fallback)
            var excelImpl = new ExcelCapabilityImpl(
                sp.GetRequiredService<IArtifactStore>(),
                sp.GetRequiredService<ILogger<ExcelCapabilityImpl>>());
            registry.Register<IExcelCapability>(excelImpl);

            return registry;
        });

        // ---- Browser picker (dev-only selector picking) ----
        services.AddSingleton<IBrowserPickerService, BrowserPickerService>();
        services.AddSingleton<IExpressionEvaluator, ExpressionEvaluator>();

        // ---- Step Handlers ----
        // Legacy handlers (backward compatible; use old "Email", "ApiCall" type names)
        services.AddScoped<IStepHandler, EmailStepHandler>();
        services.AddScoped<IStepHandler, ApiCallStepHandler>();
        services.AddScoped<IStepHandler, DelayStepHandler>();
        services.AddScoped<IStepHandler, ConditionStepHandler>();
        services.AddScoped<IStepHandler, SqlQueryStepHandler>();

        // Designer condition branching (condition.if)
        services.AddScoped<IStepHandler, ConditionIfStepHandler>();
        services.AddScoped<IStepHandler, SetVariableStepHandler>();

        // Designer foreach loop (foreach.loop, foreach.row, foreach.file)
        services.AddScoped<IStepHandler, ForEachLoopStepHandler>();
        services.AddScoped<IStepHandler, ForEachRowStepHandler>();
        services.AddScoped<IStepHandler, ForEachFileStepHandler>();
        services.AddScoped<IStepHandler, DataTableAggregateStepHandler>();
        services.AddScoped<IStepHandler, DataTableJoinStepHandler>();
        services.AddScoped<IStepHandler, HttpRequestStepHandler>();
        services.AddScoped<IStepHandler, JsonParseStepHandler>();
        services.AddScoped<IStepHandler, SqlQueryDesignerStepHandler>();
        services.AddScoped<IStepHandler, SqlExecuteStepHandler>();
        services.AddScoped<IStepHandler, SqlStoredProcedureStepHandler>();

        // Capability-based handlers (use "file.read", "file.write", "mail.send", etc.)
        services.AddScoped<IStepHandler, FileReadStepHandler>();
        services.AddScoped<IStepHandler, FileWriteStepHandler>();
        services.AddScoped<IStepHandler, MailSendStepHandler>();
        services.AddScoped<IStepHandler, MailReceiveStepHandler>();
        services.AddScoped<IStepHandler, MailReadAttachmentsStepHandler>();
        services.AddScoped<IStepHandler, MailGetBodyStepHandler>();
        services.AddScoped<IStepHandler, MailExtractValueStepHandler>();
        services.AddScoped<IStepHandler, MailExtractTableStepHandler>();
        services.AddScoped<IStepHandler, MailReplyStepHandler>();
        services.AddScoped<IStepHandler, MailForwardStepHandler>();
        services.AddScoped<IStepHandler, MailMoveStepHandler>();
        services.AddScoped<IStepHandler, MailMarkReadStepHandler>();
        services.AddScoped<IStepHandler, MailMarkReadLegacyStepHandler>();
        services.AddScoped<IStepHandler, MailDeleteStepHandler>();
        services.AddScoped<IStepHandler, ExcelReadStepHandler>();
        services.AddScoped<IStepHandler, ExcelReadRangeStepHandler>();
        services.AddScoped<IStepHandler, ExcelWriteDataTableStepHandler>();
        services.AddScoped<IStepHandler, ExcelAppendDataTableStepHandler>();
        services.AddScoped<IStepHandler, ExcelWriteStepHandler>();
        services.AddScoped<IStepHandler, ExcelWriteCellStepHandler>();
        services.AddScoped<IStepHandler, ExcelToCsvStepHandler>();
        services.AddScoped<IStepHandler, ExcelTransformStepHandler>();
        services.AddScoped<IStepHandler, ExcelMergeStepHandler>();
        services.AddScoped<IStepHandler, ExcelSplitStepHandler>();
        services.AddScoped<IStepHandler, BrowserOpenStepHandler>();
        services.AddScoped<IStepHandler, BrowserNavigateStepHandler>();
        services.AddScoped<IStepHandler, BrowserClickStepHandler>();
        services.AddScoped<IStepHandler, BrowserTypeStepHandler>();
        services.AddScoped<IStepHandler, BrowserWaitForSelectorStepHandler>();
        services.AddScoped<IStepHandler, BrowserGetTextStepHandler>();
        services.AddScoped<IStepHandler, BrowserGetAttributeStepHandler>();
        services.AddScoped<IStepHandler, BrowserScreenshotStepHandler>();
        services.AddScoped<IStepHandler, BrowserDownloadStepHandler>();
        services.AddScoped<IStepHandler, BrowserCloseStepHandler>();
        services.AddScoped<IStepHandler, BrowserPressKeyStepHandler>();
        services.AddScoped<IStepHandler, BrowserClearStepHandler>();
        services.AddScoped<IStepHandler, BrowserScrollStepHandler>();
        services.AddScoped<IStepHandler, BrowserHoverStepHandler>();
        services.AddScoped<IStepHandler, BrowserWaitForTextStepHandler>();
        services.AddScoped<IStepHandler, BrowserWaitForUrlStepHandler>();
        services.AddScoped<IStepHandler, BrowserWaitUrlStepHandler>();
        services.AddScoped<IStepHandler, BrowserWaitTextStepHandler>();
        services.AddScoped<IStepHandler, BrowserWaitDownloadStepHandler>();
        services.AddScoped<IStepHandler, BrowserWaitNetworkIdleStepHandler>();
        services.AddScoped<IStepHandler, BrowserElementExistsStepHandler>();
        services.AddScoped<IStepHandler, BrowserUploadFileStepHandler>();
        services.AddScoped<IStepHandler, BrowserSwitchTabStepHandler>();
        services.AddScoped<IStepHandler, BrowserCloseTabStepHandler>();
        services.AddScoped<IStepHandler, BrowserHandleAlertStepHandler>();
        services.AddScoped<IStepHandler, BrowserWaitPopupStepHandler>();
        services.AddScoped<IStepHandler, BrowserClickAndHandleAlertStepHandler>();
        services.AddScoped<IStepHandler, BrowserExtractTableStepHandler>();
        services.AddScoped<IStepHandler, BrowserSelectOptionStepHandler>();
        services.AddScoped<IStepHandler, BrowserSelectStepHandler>();
        services.AddScoped<IStepHandler, BrowserEvaluateStepHandler>();
        services.AddScoped<IStepHandler, BrowserWaitForDownloadStepHandler>();

        // Folder / shared-folder handlers (folder.read-file, folder.write-file, …)
        services.AddScoped<IStepHandler, FolderReadFileStepHandler>();
        services.AddScoped<IStepHandler, FolderWriteFileStepHandler>();
        services.AddScoped<IStepHandler, FolderCopyFileStepHandler>();
        services.AddScoped<IStepHandler, FolderMoveFileStepHandler>();
        services.AddScoped<IStepHandler, FolderListFilesStepHandler>();
        services.AddScoped<IStepHandler, FolderDeleteFileStepHandler>();

        // CSV handlers (csv.read, csv.write)
        services.AddScoped<IStepHandler, CsvReadStepHandler>();
        services.AddScoped<IStepHandler, CsvWriteStepHandler>();

        // JSON file handlers (json.read-file, json.write-file)
        services.AddScoped<IStepHandler, JsonReadFileStepHandler>();
        services.AddScoped<IStepHandler, JsonWriteFileStepHandler>();

        // ZIP handlers (zip.extract, zip.create)
        services.AddScoped<IStepHandler, ZipExtractStepHandler>();
        services.AddScoped<IStepHandler, ZipCreateStepHandler>();

        // PDF handlers (pdf.read-text, pdf.extract-table)
        services.AddScoped<IStepHandler, PdfReadTextStepHandler>();
        services.AddScoped<IStepHandler, PdfExtractTableStepHandler>();

        // Word handlers (word.fill-template)
        services.AddScoped<IStepHandler, WordFillTemplateStepHandler>();

        // ---- Workflow Engine ----
        services.AddScoped<IWorkflowEngine, WorkflowEngine.WorkflowEngine>();

        // ---- HttpClient for HTTP step handlers ----
        services.AddHttpClient("WorkflowApiCall")
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30));

        services.AddHttpClient("WorkflowHttpRequest")
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(300));

        return services;
    }
}
