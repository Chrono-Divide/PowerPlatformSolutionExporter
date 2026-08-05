using System.Diagnostics;
using System.Collections.Concurrent;

namespace dataverse_table_backup_restore.Services;

public sealed class PacCliService
{
    private readonly string? _pacExecutablePath;

    public PacCliService(string? pacExecutablePath = null)
    {
        _pacExecutablePath = pacExecutablePath;
    }

    public async Task ExportSolutionAsync(
        string environmentUrl,
        string solutionUniqueName,
        string outputPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report($"Exporting solution '{solutionUniqueName}'...");
        var result = await RunAsync(
            [
                "solution", "export",
                "--environment", environmentUrl,
                "--name", solutionUniqueName,
                "--path", outputPath,
                "--overwrite",
            ],
            progress,
            cancellationToken);

        EnsureSuccess(result, "Solution export");
        if (!File.Exists(outputPath))
        {
            throw new FileNotFoundException("PAC reported success but the solution package was not created.", outputPath);
        }
    }

    public async Task ImportSolutionAsync(
        string environmentUrl,
        string solutionPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Importing the snapshot solution and publishing changes...");
        var result = await RunAsync(
            [
                "solution", "import",
                "--environment", environmentUrl,
                "--path", solutionPath,
                "--publish-changes",
            ],
            progress,
            cancellationToken);

        EnsureSuccess(result, "Solution import");
    }

    public async Task<string> GetCurrentEnvironmentAsync(CancellationToken cancellationToken)
    {
        var result = await RunAsync(["env", "who"], null, cancellationToken);
        EnsureSuccess(result, "PAC environment check");
        return result.StandardOutput;
    }

    private async Task<PacResult> RunAsync(
        IReadOnlyList<string> arguments,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _pacExecutablePath ?? ResolvePacExecutable(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        var output = new ConcurrentQueue<string>();
        var errors = new ConcurrentQueue<string>();
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                return;
            }

            output.Enqueue(eventArgs.Data);
            progress?.Report(eventArgs.Data);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (string.IsNullOrWhiteSpace(eventArgs.Data))
            {
                return;
            }

            errors.Enqueue(eventArgs.Data);
            progress?.Report(eventArgs.Data);
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Unable to start the Power Platform CLI.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException("Power Platform CLI (pac) was not found or could not be started.", exception);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            process.WaitForExit();
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        return new PacResult(process.ExitCode, string.Join(Environment.NewLine, output), string.Join(Environment.NewLine, errors));
    }

    private static string ResolvePacExecutable()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var executableNames = OperatingSystem.IsWindows()
            ? new[] { "pac.exe", "pac.launcher.exe", "pac.cmd", "pac.bat" }
            : new[] { "pac" };

        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var executableName in executableNames)
            {
                var candidate = Path.Combine(directory, executableName);
                if (!File.Exists(candidate))
                {
                    continue;
                }

                if (OperatingSystem.IsWindows() &&
                    Path.GetExtension(candidate) is ".cmd" or ".bat")
                {
                    var launcher = Path.Combine(directory, "pac.launcher.exe");
                    if (File.Exists(launcher))
                    {
                        return launcher;
                    }

                    continue;
                }

                return candidate;
            }
        }

        throw new FileNotFoundException("Power Platform CLI (pac) was not found on PATH.");
    }

    private static void EnsureSuccess(PacResult result, string operation)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        var details = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        throw new InvalidOperationException($"{operation} failed (exit code {result.ExitCode}).{Environment.NewLine}{details}");
    }

    private sealed record PacResult(int ExitCode, string StandardOutput, string StandardError);
}
