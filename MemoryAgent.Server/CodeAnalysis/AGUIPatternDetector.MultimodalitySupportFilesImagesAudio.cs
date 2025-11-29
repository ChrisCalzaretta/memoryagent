using MemoryAgent.Server.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// Multimodality Support (Files, Images, Audio)
/// </summary>
public partial class AGUIPatternDetector
{
    #region Multimodality Support (Files, Images, Audio)

    private List<CodePattern> DetectMultimodality(SyntaxNode root, string filePath, string? context, string sourceCode)
    {
        var patterns = new List<CodePattern>();

        // Pattern: File/attachment handling
        if (sourceCode.Contains("attachment") || sourceCode.Contains("Attachment") ||
            sourceCode.Contains("file upload") || sourceCode.Contains("FileUpload"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_Multimodal_Files",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "Multimodal file/attachment support",
                filePath: filePath,
                lineNumber: 1,
                content: "// File/attachment handling detected",
                bestPractice: "AG-UI supports typed attachments for rich, multi-modal agent interactions.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.75f,
                metadata: new Dictionary<string, object>
                {
                    ["multimodal_type"] = "Files",
                    ["capabilities"] = new[] { "Document uploads", "File attachments", "Binary data" }
                }
            ));
        }

        // Pattern: Image processing
        if (sourceCode.Contains("image") || sourceCode.Contains("Image") ||
            sourceCode.Contains("ImageData") || sourceCode.Contains("picture"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_Multimodal_Images",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "Multimodal image processing",
                filePath: filePath,
                lineNumber: 1,
                content: "// Image processing detected",
                bestPractice: "AG-UI supports image inputs for visual agent interactions and multimodal AI.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.7f,
                metadata: new Dictionary<string, object>
                {
                    ["multimodal_type"] = "Images",
                    ["capabilities"] = new[] { "Image upload", "Visual analysis", "OCR" }
                }
            ));
        }

        // Pattern: Audio/voice processing
        if (sourceCode.Contains("audio") || sourceCode.Contains("Audio") ||
            sourceCode.Contains("voice") || sourceCode.Contains("transcript") ||
            sourceCode.Contains("Transcript"))
        {
            patterns.Add(CreatePattern(
                name: "AGUI_Multimodal_Audio",
                type: PatternType.AGUI,
                category: PatternCategory.AIAgents,
                implementation: "Multimodal audio/voice support",
                filePath: filePath,
                lineNumber: 1,
                content: "// Audio/voice processing detected",
                bestPractice: "AG-UI supports audio inputs and real-time transcripts for voice-enabled agents.",
                azureUrl: "https://docs.ag-ui.com/",
                context: context,
                confidence: 0.72f,
                metadata: new Dictionary<string, object>
                {
                    ["multimodal_type"] = "Audio",
                    ["capabilities"] = new[] { "Voice input", "Audio transcripts", "Speech-to-text" }
                }
            ));
        }

        return patterns;
    }

    
    #endregion
}
