using System.Diagnostics;
using System.Text;
using Ape.AiModule.Application.DTOs.AI;
using Ape.AiModule.Application.Interfaces.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ape.AiModule.Infrastructure.AI;

public sealed class ScriptedAiReportSyncService : IAiReportSyncService
{
    private readonly string _repoRoot;
    private readonly string _reportRoot;
    private readonly string _workbookPath;
    private readonly string _scriptPath;
    private readonly ILogger<ScriptedAiReportSyncService> _logger;

    public ScriptedAiReportSyncService(IHostEnvironment environment, ILogger<ScriptedAiReportSyncService> logger)
    {
        _repoRoot = ResolveRepoRoot(environment.ContentRootPath);
        _reportRoot = Path.Combine(_repoRoot, "Test");
        _workbookPath = Path.Combine(_reportRoot, "AI_Benchmark_Report.xlsx");
        _scriptPath = Path.Combine(_repoRoot, "scripts", "update_ai_report_workbook.py");
        _logger = logger;
    }

    public async Task<AiReportSyncResult> SyncAsync(string trigger, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_reportRoot);

        if (!File.Exists(_scriptPath))
        {
            return new AiReportSyncResult(
                false,
                trigger,
                $"Report sync script was not found at '{_scriptPath}'.",
                _reportRoot,
                _workbookPath,
                DateTimeOffset.UtcNow);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ResolvePythonLauncher(),
            Arguments = $"\"{_scriptPath}\"",
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (string.Equals(startInfo.FileName, "py", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.Arguments = $"-3 \"{_scriptPath}\"";
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            if (process.ExitCode == 0)
            {
                return new AiReportSyncResult(
                    true,
                    trigger,
                    "Report workbook and evidence exports were refreshed successfully.",
                    _reportRoot,
                    _workbookPath,
                    DateTimeOffset.UtcNow,
                    string.IsNullOrWhiteSpace(stdOut) ? null : stdOut.Trim(),
                    string.IsNullOrWhiteSpace(stdErr) ? null : stdErr.Trim());
            }

            _logger.LogWarning(
                "AI report sync failed. Trigger={Trigger}, ExitCode={ExitCode}, StdErr={StdErr}",
                trigger,
                process.ExitCode,
                stdErr);

            return new AiReportSyncResult(
                false,
                trigger,
                $"Report sync script exited with code {process.ExitCode}.",
                _reportRoot,
                _workbookPath,
                DateTimeOffset.UtcNow,
                string.IsNullOrWhiteSpace(stdOut) ? null : stdOut.Trim(),
                string.IsNullOrWhiteSpace(stdErr) ? null : stdErr.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI report sync threw an exception. Trigger={Trigger}", trigger);
            return new AiReportSyncResult(
                false,
                trigger,
                $"Report sync failed: {ex.Message}",
                _reportRoot,
                _workbookPath,
                DateTimeOffset.UtcNow,
                null,
                ex.ToString());
        }
    }

    private static string ResolvePythonLauncher()
    {
        return OperatingSystem.IsWindows() ? "py" : "python3";
    }

    private static string ResolveRepoRoot(string contentRootPath)
    {
        var current = new DirectoryInfo(contentRootPath);
        while (current is not null)
        {
            var scriptPath = Path.Combine(current.FullName, "scripts", "update_ai_report_workbook.py");
            var templatePath = Path.Combine(
                current.FullName,
                "Docs",
                "Prototype_Document",
                "Guides",
                "Templates",
                "AI_Report",
                "AI_Benchmark_Report_Template.xlsx");

            if (File.Exists(scriptPath) && File.Exists(templatePath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not resolve repository root from content root '{contentRootPath}'.");
    }
}
