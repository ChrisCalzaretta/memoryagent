using System.Text.Json;
using AgentContracts.Responses;

namespace CodingOrchestrator.Server.Services;

/// <summary>
/// File-based job persistence to survive container restarts
/// </summary>
public class JobPersistenceService : IJobPersistenceService
{
    private readonly string _jobsPath;
    private readonly ILogger<JobPersistenceService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JobPersistenceService(IConfiguration configuration, ILogger<JobPersistenceService> logger)
    {
        _jobsPath = configuration["JobPersistence:Path"] ?? "/data/jobs";
        _logger = logger;
        
        // Ensure directory exists
        Directory.CreateDirectory(_jobsPath);
        _logger.LogInformation("💾 Job persistence initialized at {Path}", _jobsPath);
    }

    public async Task SaveJobAsync(PersistedJob job)
    {
        await _lock.WaitAsync();
        try
        {
            job.LastUpdatedAt = DateTime.UtcNow;
            var filePath = GetJobFilePath(job.JobId);
            var json = JsonSerializer.Serialize(job, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogDebug("💾 Saved job {JobId} metadata to disk", job.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save job {JobId} to disk", job.JobId);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveJobFilesAsync(string jobId, List<GeneratedFile> files, int iteration)
    {
        if (files == null || files.Count == 0) return;

        await _lock.WaitAsync();
        try
        {
            var filesDir = GetJobFilesDirectory(jobId);
            Directory.CreateDirectory(filesDir);

            // Save each file
            foreach (var file in files)
            {
                var safePath = SanitizePath(file.Path);
                var fullPath = Path.Combine(filesDir, safePath);
                
                // Create subdirectories if needed
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(fullPath, file.Content);
            }

            // Save a manifest with metadata
            var manifest = new FileManifest
            {
                JobId = jobId,
                Iteration = iteration,
                SavedAt = DateTime.UtcNow,
                Files = files.Select(f => new FileManifestEntry
                {
                    Path = f.Path,
                    ChangeType = f.ChangeType.ToString()
                }).ToList()
            };

            var manifestPath = Path.Combine(filesDir, "_manifest.json");
            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(manifestPath, manifestJson);

            _logger.LogInformation("💾 Saved {Count} code files for job {JobId} (iteration {Iteration})", 
                files.Count, jobId, iteration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save files for job {JobId}", jobId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<GeneratedFile>> LoadJobFilesAsync(string jobId)
    {
        var files = new List<GeneratedFile>();
        
        try
        {
            var filesDir = GetJobFilesDirectory(jobId);
            var manifestPath = Path.Combine(filesDir, "_manifest.json");

            if (!File.Exists(manifestPath))
            {
                return files;
            }

            var manifestJson = await File.ReadAllTextAsync(manifestPath);
            var manifest = JsonSerializer.Deserialize<FileManifest>(manifestJson, JsonOptions);

            if (manifest?.Files == null) return files;

            foreach (var entry in manifest.Files)
            {
                var safePath = SanitizePath(entry.Path);
                var fullPath = Path.Combine(filesDir, safePath);

                if (File.Exists(fullPath))
                {
                    var content = await File.ReadAllTextAsync(fullPath);
                    files.Add(new GeneratedFile
                    {
                        Path = entry.Path,
                        Content = content,
                        ChangeType = Enum.TryParse<FileChangeType>(entry.ChangeType, out var ct) 
                            ? ct : FileChangeType.Created
                    });
                }
            }

            _logger.LogInformation("💾 Loaded {Count} code files for job {JobId}", files.Count, jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load files for job {JobId}", jobId);
        }

        return files;
    }

    public async Task<IEnumerable<PersistedJob>> LoadAllJobsAsync()
    {
        var jobs = new List<PersistedJob>();
        
        try
        {
            if (!Directory.Exists(_jobsPath))
            {
                _logger.LogInformation("Jobs directory does not exist, starting fresh");
                return jobs;
            }

            var files = Directory.GetFiles(_jobsPath, "*.json");
            _logger.LogInformation("Found {Count} persisted job files", files.Length);

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var job = JsonSerializer.Deserialize<PersistedJob>(json, JsonOptions);
                    if (job != null)
                    {
                        // Check if this job has persisted files
                        var filesDir = GetJobFilesDirectory(job.JobId);
                        job.HasPersistedFiles = Directory.Exists(filesDir) && 
                            File.Exists(Path.Combine(filesDir, "_manifest.json"));
                        
                        jobs.Add(job);
                        _logger.LogDebug("Loaded job {JobId} from disk (has files: {HasFiles})", 
                            job.JobId, job.HasPersistedFiles);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load job from {File}, skipping", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load jobs from disk");
        }

        return jobs;
    }

    public async Task DeleteJobAsync(string jobId)
    {
        await _lock.WaitAsync();
        try
        {
            // Delete job metadata
            var filePath = GetJobFilePath(jobId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Delete job files directory
            var filesDir = GetJobFilesDirectory(jobId);
            if (Directory.Exists(filesDir))
            {
                Directory.Delete(filesDir, recursive: true);
            }

            _logger.LogDebug("Deleted job {JobId} and files from disk", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete job {JobId} from disk", jobId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CleanupOldJobsAsync(TimeSpan retentionPeriod)
    {
        try
        {
            var jobs = await LoadAllJobsAsync();
            var cutoff = DateTime.UtcNow - retentionPeriod;
            var deletedCount = 0;

            foreach (var job in jobs)
            {
                // Only cleanup completed/failed/cancelled jobs older than retention
                if (job.Status.Status is TaskState.Complete 
                    or TaskState.Failed 
                    or TaskState.Cancelled)
                {
                    if (job.CompletedAt.HasValue && job.CompletedAt.Value < cutoff)
                    {
                        await DeleteJobAsync(job.JobId);
                        deletedCount++;
                    }
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("🧹 Cleaned up {Count} old jobs and their files", deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old jobs");
        }
    }

    private string GetJobFilePath(string jobId)
    {
        var safeJobId = SanitizeJobId(jobId);
        return Path.Combine(_jobsPath, $"{safeJobId}.json");
    }

    private string GetJobFilesDirectory(string jobId)
    {
        var safeJobId = SanitizeJobId(jobId);
        return Path.Combine(_jobsPath, safeJobId, "files");
    }

    private static string SanitizeJobId(string jobId)
    {
        return string.Join("_", jobId.Split(Path.GetInvalidFileNameChars()));
    }

    private static string SanitizePath(string path)
    {
        // Remove any path traversal attempts and normalize
        var normalized = path.Replace("\\", "/");
        var parts = normalized.Split('/').Where(p => p != ".." && p != ".").ToArray();
        return Path.Combine(parts);
    }

    private class FileManifest
    {
        public string JobId { get; set; } = "";
        public int Iteration { get; set; }
        public DateTime SavedAt { get; set; }
        public List<FileManifestEntry> Files { get; set; } = new();
    }

    private class FileManifestEntry
    {
        public string Path { get; set; } = "";
        public string ChangeType { get; set; } = "Created";
    }
}

