using System.Text;
using System.Text.RegularExpressions;
using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Parser for Markdown files with smart chunking by headers and sections
/// </summary>
public class MarkdownParser : ICodeParser
{
    private readonly ILogger<MarkdownParser> _logger;

    public MarkdownParser(ILogger<MarkdownParser> logger)
    {
        _logger = logger;
    }

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension == ".md" || extension == ".markdown";
    }

    public async Task<ParseResult> ParseFileAsync(string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new ParseResult { Errors = { $"File not found: {filePath}" } };
        }

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return await ParseCodeAsync(content, filePath, context, cancellationToken);
    }

    public Task<ParseResult> ParseCodeAsync(string code, string filePath, string? context = null, CancellationToken cancellationToken = default)
    {
        var result = new ParseResult();
        
        try
        {
            var content = code;
            var fileName = Path.GetFileName(filePath);
            
            // Extract front matter (YAML/TOML) if present
            var frontMatter = ExtractFrontMatter(content, out var bodyContent);
            
            // Create file-level node
            var fileNode = new CodeMemory
            {
                Type = CodeMemoryType.File,
                Name = fileName,
                Content = content.Length > 2000 ? content.Substring(0, 2000) + "..." : content,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = 1,
                Metadata = new Dictionary<string, object>
                {
                    ["file_type"] = ".md",
                    ["is_markdown"] = true,
                    ["has_front_matter"] = frontMatter.Any()
                }
            };
            
            if (frontMatter.Any())
            {
                foreach (var kvp in frontMatter)
                {
                    fileNode.Metadata[$"fm_{kvp.Key}"] = kvp.Value;
                }
            }
            
            result.CodeElements.Add(fileNode);

            // Extract title (first H1)
            var titleMatch = Regex.Match(bodyContent, @"^#\s+(.+)$", RegexOptions.Multiline);
            if (titleMatch.Success)
            {
                fileNode.Metadata["title"] = titleMatch.Groups[1].Value.Trim();
            }

            // Chunk by headers (H1, H2, H3)
            var chunks = ChunkByHeaders(bodyContent, filePath, context);
            result.CodeElements.AddRange(chunks);

            // Extract links and create relationships
            ExtractLinks(bodyContent, filePath, result);

            // Extract code blocks
            ExtractCodeBlocks(bodyContent, filePath, context, result);

            _logger.LogInformation(
                "Parsed Markdown file: {FileName} - {ElementCount} elements, {RelationshipCount} relationships",
                fileName, result.CodeElements.Count, result.Relationships.Count);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Error parsing Markdown file: {ex.Message}");
            _logger.LogError(ex, "Error parsing Markdown file: {FilePath}", filePath);
            return Task.FromResult(result);
        }
    }

    private Dictionary<string, string> ExtractFrontMatter(string content, out string bodyContent)
    {
        var frontMatter = new Dictionary<string, string>();
        bodyContent = content;

        // Check for YAML front matter (--- ... ---)
        var yamlMatch = Regex.Match(content, @"^---\r?\n(.*?)\r?\n---\r?\n", RegexOptions.Singleline);
        if (yamlMatch.Success)
        {
            var yaml = yamlMatch.Groups[1].Value;
            bodyContent = content.Substring(yamlMatch.Length);
            
            // Simple YAML parsing (key: value)
            foreach (Match match in Regex.Matches(yaml, @"^(\w+):\s*(.+)$", RegexOptions.Multiline))
            {
                frontMatter[match.Groups[1].Value] = match.Groups[2].Value.Trim();
            }
        }
        // Check for TOML front matter (+++ ... +++)
        else if (content.StartsWith("+++"))
        {
            var tomlMatch = Regex.Match(content, @"^\+\+\+\r?\n(.*?)\r?\n\+\+\+\r?\n", RegexOptions.Singleline);
            if (tomlMatch.Success)
            {
                var toml = tomlMatch.Groups[1].Value;
                bodyContent = content.Substring(tomlMatch.Length);
                
                // Simple TOML parsing (key = value)
                foreach (Match match in Regex.Matches(toml, @"^(\w+)\s*=\s*""?(.+?)""?$", RegexOptions.Multiline))
                {
                    frontMatter[match.Groups[1].Value] = match.Groups[2].Value.Trim();
                }
            }
        }

        return frontMatter;
    }

    private List<CodeMemory> ChunkByHeaders(string content, string filePath, string? context)
    {
        var chunks = new List<CodeMemory>();
        var lines = content.Split('\n');
        
        // Find all headers with their positions
        var headers = new List<(int lineNumber, int level, string title, int startIndex)>();
        int lineNumber = 1;
        int charIndex = 0;

        foreach (var line in lines)
        {
            var headerMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
            if (headerMatch.Success)
            {
                int level = headerMatch.Groups[1].Value.Length;
                string title = headerMatch.Groups[2].Value.Trim();
                headers.Add((lineNumber, level, title, charIndex));
            }
            charIndex += line.Length + 1; // +1 for newline
            lineNumber++;
        }

        // Create chunks for each section
        for (int i = 0; i < headers.Count; i++)
        {
            var (startLine, level, title, startIdx) = headers[i];
            
            // Find the end of this section (next header of same or higher level)
            int endIdx = content.Length;
            int endLine = lines.Length;
            
            for (int j = i + 1; j < headers.Count; j++)
            {
                if (headers[j].level <= level)
                {
                    endIdx = headers[j].startIndex;
                    endLine = headers[j].lineNumber - 1;
                    break;
                }
            }

            var sectionContent = content.Substring(startIdx, endIdx - startIdx).Trim();
            
            if (string.IsNullOrWhiteSpace(sectionContent))
                continue;

            var chunk = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"Section: {title}",
                Content = sectionContent,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = startLine,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "section",
                    ["header_level"] = level,
                    ["section_title"] = title,
                    ["line_count"] = endLine - startLine + 1
                }
            };

            chunks.Add(chunk);
        }

        // If no headers found, create a single chunk
        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(content))
        {
            chunks.Add(new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = "Content",
                Content = content.Length > 2000 ? content.Substring(0, 2000) + "..." : content,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = 1,
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "full_content"
                }
            });
        }

        return chunks;
    }

    private void ExtractLinks(string content, string filePath, ParseResult result)
    {
        // Extract markdown links: [text](url)
        var linkMatches = Regex.Matches(content, @"\[([^\]]+)\]\(([^\)]+)\)");
        
        var linkGroups = new Dictionary<string, List<string>>();

        foreach (Match match in linkMatches)
        {
            var linkText = match.Groups[1].Value;
            var linkUrl = match.Groups[2].Value;

            // Categorize links
            string category;
            if (linkUrl.StartsWith("http://") || linkUrl.StartsWith("https://"))
            {
                category = "external";
            }
            else if (linkUrl.StartsWith("#"))
            {
                category = "internal_anchor";
            }
            else if (linkUrl.EndsWith(".md") || linkUrl.EndsWith(".markdown"))
            {
                category = "markdown_reference";
                
                // Create REFERENCES relationship to other markdown files
                var targetFile = Path.GetFileName(linkUrl);
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = Path.GetFileName(filePath),
                    ToName = targetFile,
                    Type = RelationshipType.Uses, // Using USES for markdown references
                    Properties = new Dictionary<string, object>
                    {
                        ["relationship_subtype"] = "markdown_reference",
                        ["link_text"] = linkText,
                        ["link_url"] = linkUrl
                    }
                });
            }
            else
            {
                category = "relative";
            }

            if (!linkGroups.ContainsKey(category))
            {
                linkGroups[category] = new List<string>();
            }
            linkGroups[category].Add(linkUrl);
        }

        // Add link metadata to file node
        if (result.CodeElements.Any())
        {
            var fileNode = result.CodeElements.First();
            fileNode.Metadata["link_count"] = linkMatches.Count;
            foreach (var group in linkGroups)
            {
                fileNode.Metadata[$"links_{group.Key}"] = group.Value.Count;
            }
        }
    }

    private void ExtractCodeBlocks(string content, string filePath, string? context, ParseResult result)
    {
        // Extract fenced code blocks: ```language ... ```
        var codeBlockMatches = Regex.Matches(content, @"```(\w+)?\r?\n(.*?)```", RegexOptions.Singleline);
        
        int blockIndex = 1;
        foreach (Match match in codeBlockMatches)
        {
            var language = match.Groups[1].Value;
            var code = match.Groups[2].Value.Trim();
            
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var codeBlock = new CodeMemory
            {
                Type = CodeMemoryType.Pattern,
                Name = $"CodeBlock_{blockIndex}_{language}",
                Content = code,
                FilePath = filePath,
                Context = context ?? "default",
                LineNumber = GetLineNumber(content, match.Index),
                Metadata = new Dictionary<string, object>
                {
                    ["chunk_type"] = "code_block",
                    ["language"] = string.IsNullOrEmpty(language) ? "plaintext" : language,
                    ["code_length"] = code.Length
                }
            };

            result.CodeElements.Add(codeBlock);
            
            // Create relationship from file to code block
            if (result.CodeElements.Any())
            {
                result.Relationships.Add(new CodeRelationship
                {
                    FromName = Path.GetFileName(filePath),
                    ToName = codeBlock.Name,
                    Type = RelationshipType.Defines,
                    Properties = new Dictionary<string, object>
                    {
                        ["relationship_subtype"] = "contains_code_block"
                    }
                });
            }

            blockIndex++;
        }

        // Add code block count to file metadata
        if (result.CodeElements.Any() && codeBlockMatches.Count > 0)
        {
            var fileNode = result.CodeElements.First();
            fileNode.Metadata["code_block_count"] = codeBlockMatches.Count;
        }
    }

    private int GetLineNumber(string content, int charIndex)
    {
        return content.Substring(0, Math.Min(charIndex, content.Length)).Count(c => c == '\n') + 1;
    }
}

