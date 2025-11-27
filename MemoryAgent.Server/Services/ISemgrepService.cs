using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services;

/// <summary>
/// Service for running Semgrep security scans
/// </summary>
public interface ISemgrepService
{
    /// <summary>
    /// Scan a single file for security vulnerabilities
    /// </summary>
    Task<SemgrepReport> ScanFileAsync(string filePath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Scan an entire directory for security vulnerabilities
    /// </summary>
    Task<SemgrepReport> ScanDirectoryAsync(string directoryPath, bool recursive = true, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if Semgrep is available
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

