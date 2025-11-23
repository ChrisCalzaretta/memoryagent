using MemoryAgent.Server.Models;
using System.Text.Json;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for JSON files (excludes config files like appsettings.json)
/// </summary>
public class JsonParser
{
    private readonly ILogger<JsonParser> _logger;
    
    // Files to exclude from indexing
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "appsettings.json",
        "appsettings.development.json",
        "appsettings.production.json",
        "config.json",
        "package.json",
        "package-lock.json",
        "tsconfig.json",
        "jsconfig.json",
        ".eslintrc.json",
        ".prettierrc.json"
    };

    public JsonParser(ILogger<JsonParser> logger)
    {
        _logger = logger;
    }

    public async Task<ParseResult> ParseFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        var result = new ParseResult();

        try
        {
            // Check if this file should be excluded
            var fileName = Path.GetFileName(filePath);
            if (ExcludedFiles.Contains(fileName))
            {
                _logger.LogDebug("Skipping excluded JSON file: {FileName}", fileName);
                result.Errors.Add($"Excluded configuration file: {fileName}");
                return result;
            }

            if (!File.Exists(filePath))
            {
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            
            // Try to parse and chunk the JSON
            try
            {
                using var document = JsonDocument.Parse(content);
                var root = document.RootElement;

                // Create chunks based on JSON structure
                if (root.ValueKind == JsonValueKind.Object)
                {
                    ChunkJsonObject(root, filePath, context, result, Path.GetFileNameWithoutExtension(filePath));
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    ChunkJsonArray(root, filePath, context, result, Path.GetFileNameWithoutExtension(filePath));
                }
                else
                {
                    // Simple value - store as single chunk
                    var codeMemory = new CodeMemory
                    {
                        Type = CodeMemoryType.Pattern,
                        Name = Path.GetFileNameWithoutExtension(filePath),
                        Content = content,
                        FilePath = filePath,
                        Context = context ?? "default",
                        LineNumber = 1,
                        Metadata = new Dictionary<string, object>
                        {
                            ["file_type"] = "json",
                            ["json_type"] = root.ValueKind.ToString()
                        }
                    };
                    result.CodeElements.Add(codeMemory);
                }

                _logger.LogDebug("Parsed JSON file {FilePath}: {ChunkCount} chunks", 
                    filePath, result.CodeElements.Count);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Invalid JSON in file: {FilePath}", filePath);
                result.Errors.Add($"Invalid JSON: {jsonEx.Message}");
                
                // Still index as raw text
                var codeMemory = new CodeMemory
                {
                    Type = CodeMemoryType.Pattern,
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    Content = content.Length > 2000 ? content.Substring(0, 2000) + "..." : content,
                    FilePath = filePath,
                    Context = context ?? "default",
                    LineNumber = 1,
                    Metadata = new Dictionary<string, object>
                    {
                        ["file_type"] = "json",
                        ["parse_error"] = true,
                        ["error_message"] = jsonEx.Message
                    }
                };
                result.CodeElements.Add(codeMemory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing JSON file: {FilePath}", filePath);
            result.Errors.Add($"Error parsing file: {ex.Message}");
        }

        return result;
    }

    private void ChunkJsonObject(JsonElement obj, string filePath, string? context, ParseResult result, string basePath, int depth = 0)
    {
        // Limit depth to avoid excessive chunking
        if (depth > 3) return;

        foreach (var property in obj.EnumerateObject())
        {
            var propertyName = property.Name;
            var propertyValue = property.Value;
            var fullPath = depth == 0 ? propertyName : $"{basePath}.{propertyName}";

            // Create chunks for objects and arrays
            if (propertyValue.ValueKind == JsonValueKind.Object)
            {
                var objContent = propertyValue.ToString();
                if (!string.IsNullOrWhiteSpace(objContent) && objContent.Length > 10)
                {
                    var codeMemory = new CodeMemory
                    {
                        Type = CodeMemoryType.Pattern,
                        Name = fullPath,
                        Content = objContent.Length > 2000 ? objContent.Substring(0, 2000) + "..." : objContent,
                        FilePath = filePath,
                        Context = context ?? "default",
                        LineNumber = 1, // JSON doesn't have meaningful line numbers
                        Metadata = new Dictionary<string, object>
                        {
                            ["file_type"] = "json",
                            ["json_path"] = fullPath,
                            ["json_type"] = "object",
                            ["depth"] = depth
                        }
                    };
                    result.CodeElements.Add(codeMemory);
                    
                    // Recurse into nested objects
                    ChunkJsonObject(propertyValue, filePath, context, result, fullPath, depth + 1);
                }
            }
            else if (propertyValue.ValueKind == JsonValueKind.Array)
            {
                var arrContent = propertyValue.ToString();
                if (!string.IsNullOrWhiteSpace(arrContent) && arrContent.Length > 10)
                {
                    var codeMemory = new CodeMemory
                    {
                        Type = CodeMemoryType.Pattern,
                        Name = fullPath,
                        Content = arrContent.Length > 2000 ? arrContent.Substring(0, 2000) + "..." : arrContent,
                        FilePath = filePath,
                        Context = context ?? "default",
                        LineNumber = 1,
                        Metadata = new Dictionary<string, object>
                        {
                            ["file_type"] = "json",
                            ["json_path"] = fullPath,
                            ["json_type"] = "array",
                            ["array_length"] = propertyValue.GetArrayLength(),
                            ["depth"] = depth
                        }
                    };
                    result.CodeElements.Add(codeMemory);
                }
            }
            else
            {
                // For primitive values, only store if meaningful
                var valueStr = propertyValue.ToString();
                if (!string.IsNullOrWhiteSpace(valueStr) && valueStr.Length > 5)
                {
                    var codeMemory = new CodeMemory
                    {
                        Type = CodeMemoryType.Pattern,
                        Name = fullPath,
                        Content = $"{propertyName}: {valueStr}",
                        FilePath = filePath,
                        Context = context ?? "default",
                        LineNumber = 1,
                        Metadata = new Dictionary<string, object>
                        {
                            ["file_type"] = "json",
                            ["json_path"] = fullPath,
                            ["json_type"] = propertyValue.ValueKind.ToString(),
                            ["depth"] = depth
                        }
                    };
                    result.CodeElements.Add(codeMemory);
                }
            }
        }
    }

    private void ChunkJsonArray(JsonElement arr, string filePath, string? context, ParseResult result, string basePath)
    {
        int index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
            {
                var itemContent = item.ToString();
                if (!string.IsNullOrWhiteSpace(itemContent) && itemContent.Length > 10)
                {
                    var codeMemory = new CodeMemory
                    {
                        Type = CodeMemoryType.Pattern,
                        Name = $"{basePath}[{index}]",
                        Content = itemContent.Length > 2000 ? itemContent.Substring(0, 2000) + "..." : itemContent,
                        FilePath = filePath,
                        Context = context ?? "default",
                        LineNumber = 1,
                        Metadata = new Dictionary<string, object>
                        {
                            ["file_type"] = "json",
                            ["json_path"] = $"{basePath}[{index}]",
                            ["json_type"] = item.ValueKind.ToString(),
                            ["array_index"] = index
                        }
                    };
                    result.CodeElements.Add(codeMemory);
                }
            }
            index++;
            
            // Limit array indexing to first 100 items
            if (index >= 100) break;
        }
    }
}

