using System.Diagnostics;
using System.Text.Json;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for running Semgrep security scans
/// </summary>
public class SemgrepService : ISemgrepService
{
    private readonly ILogger<SemgrepService> _logger;
    private bool? _isAvailable;

    public SemgrepService(ILogger<SemgrepService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (_isAvailable.HasValue)
            return _isAvailable.Value;

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "semgrep",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            _isAvailable = process.ExitCode == 0;
            
            if (_isAvailable.Value)
            {
                _logger.LogInformation("Semgrep available: {Version}", output.Trim());
            }
            
            return _isAvailable.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semgrep not available");
            _isAvailable = false;
            return false;
        }
    }

    public async Task<SemgrepReport> ScanFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var report = new SemgrepReport
        {
            FilePath = filePath
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!await IsAvailableAsync(cancellationToken))
            {
                report.Success = false;
                report.Errors.Add("Semgrep not available");
                return report;
            }

            if (!File.Exists(filePath))
            {
                report.Success = false;
                report.Errors.Add($"File not found: {filePath}");
                return report;
            }

            _logger.LogInformation("Running Semgrep scan on: {File}", filePath);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "semgrep",
                    Arguments = $"--config=auto --json --quiet {filePath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            stopwatch.Stop();
            report.DurationSeconds = stopwatch.Elapsed.TotalSeconds;

            if (!string.IsNullOrWhiteSpace(errorOutput))
            {
                _logger.LogWarning("Semgrep stderr: {Error}", errorOutput);
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("Semgrep scan complete: no findings for {File}", filePath);
                return report;
            }

            // Parse Semgrep JSON output
            var semgrepOutput = JsonSerializer.Deserialize<SemgrepOutput>(output);
            if (semgrepOutput == null)
            {
                report.Success = false;
                report.Errors.Add("Failed to parse Semgrep output");
                return report;
            }

            // Map to our model
            report.Findings = semgrepOutput.Results.Select(r => new SemgrepFinding
            {
                RuleId = r.CheckId,
                Message = r.Extra.Message,
                FilePath = r.Path,
                StartLine = r.Start.Line,
                EndLine = r.End.Line,
                StartColumn = r.Start.Col,
                EndColumn = r.End.Col,
                Severity = r.Extra.Severity,
                CodeSnippet = r.Extra.Lines,
                Fix = r.Extra.Fix,
                Metadata = ParseMetadata(r.Extra.Metadata)
            }).ToList();

            report.Errors = semgrepOutput.Errors.Select(e => e.Message).ToList();

            _logger.LogInformation("Semgrep scan complete: {Findings} findings in {Duration}s",
                report.Findings.Count, report.DurationSeconds);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Semgrep scan on {File}", filePath);
            report.Success = false;
            report.Errors.Add($"Scan failed: {ex.Message}");
            stopwatch.Stop();
            report.DurationSeconds = stopwatch.Elapsed.TotalSeconds;
            return report;
        }
    }

    public async Task<SemgrepReport> ScanDirectoryAsync(
        string directoryPath,
        bool recursive = true,
        CancellationToken cancellationToken = default)
    {
        var report = new SemgrepReport
        {
            FilePath = directoryPath
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!await IsAvailableAsync(cancellationToken))
            {
                report.Success = false;
                report.Errors.Add("Semgrep not available");
                return report;
            }

            if (!Directory.Exists(directoryPath))
            {
                report.Success = false;
                report.Errors.Add($"Directory not found: {directoryPath}");
                return report;
            }

            _logger.LogInformation("Running Semgrep scan on directory: {Dir} (recursive: {Recursive})",
                directoryPath, recursive);

            var recursiveFlag = recursive ? "" : "--max-depth 0";
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "semgrep",
                    Arguments = $"--config=auto --json --quiet {recursiveFlag} {directoryPath}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            stopwatch.Stop();
            report.DurationSeconds = stopwatch.Elapsed.TotalSeconds;

            if (!string.IsNullOrWhiteSpace(errorOutput))
            {
                _logger.LogWarning("Semgrep stderr: {Error}", errorOutput);
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("Semgrep scan complete: no findings for {Dir}", directoryPath);
                return report;
            }

            // Parse output
            var semgrepOutput = JsonSerializer.Deserialize<SemgrepOutput>(output);
            if (semgrepOutput == null)
            {
                report.Success = false;
                report.Errors.Add("Failed to parse Semgrep output");
                return report;
            }

            // Map to our model
            report.Findings = semgrepOutput.Results.Select(r => new SemgrepFinding
            {
                RuleId = r.CheckId,
                Message = r.Extra.Message,
                FilePath = r.Path,
                StartLine = r.Start.Line,
                EndLine = r.End.Line,
                StartColumn = r.Start.Col,
                EndColumn = r.End.Col,
                Severity = r.Extra.Severity,
                CodeSnippet = r.Extra.Lines,
                Fix = r.Extra.Fix,
                Metadata = ParseMetadata(r.Extra.Metadata)
            }).ToList();

            report.Errors = semgrepOutput.Errors.Select(e => e.Message).ToList();

            _logger.LogInformation("Semgrep directory scan complete: {Findings} findings in {Files} files ({Duration}s)",
                report.Findings.Count,
                semgrepOutput.Paths?.Scanned.Count ?? 0,
                report.DurationSeconds);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Semgrep scan on {Dir}", directoryPath);
            report.Success = false;
            report.Errors.Add($"Scan failed: {ex.Message}");
            stopwatch.Stop();
            report.DurationSeconds = stopwatch.Elapsed.TotalSeconds;
            return report;
        }
    }

    private SemgrepMetadata ParseMetadata(Dictionary<string, JsonElement>? metadata)
    {
        if (metadata == null)
            return new SemgrepMetadata();

        var result = new SemgrepMetadata();

        try
        {
            if (metadata.TryGetValue("cwe", out var cwe))
            {
                if (cwe.ValueKind == JsonValueKind.Array)
                    result.CWE = string.Join(", ", cwe.EnumerateArray().Select(e => e.GetString()));
                else
                    result.CWE = cwe.GetString();
            }

            if (metadata.TryGetValue("owasp", out var owasp))
            {
                if (owasp.ValueKind == JsonValueKind.Array)
                    result.OWASP = string.Join(", ", owasp.EnumerateArray().Select(e => e.GetString()));
                else
                    result.OWASP = owasp.GetString();
            }

            if (metadata.TryGetValue("category", out var category))
                result.Category = category.GetString();

            if (metadata.TryGetValue("confidence", out var confidence))
                result.Confidence = confidence.GetString();

            if (metadata.TryGetValue("impact", out var impact))
                result.Impact = impact.GetString();

            if (metadata.TryGetValue("likelihood", out var likelihood))
                result.Likelihood = likelihood.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing Semgrep metadata");
        }

        return result;
    }
}

