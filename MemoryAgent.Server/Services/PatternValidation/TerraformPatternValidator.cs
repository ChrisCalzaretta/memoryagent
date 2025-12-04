using MemoryAgent.Server.Models;

namespace MemoryAgent.Server.Services.PatternValidation;

/// <summary>
/// Validator for Terraform Infrastructure as Code patterns
/// Covers resources, modules, variables, state management, and security
/// </summary>
public class TerraformPatternValidator : IPatternValidator
{
    public IEnumerable<PatternType> SupportedPatternTypes => new[] { PatternType.Terraform };

    public PatternQualityResult Validate(CodePattern pattern)
    {
        var result = new PatternQualityResult
        {
            Pattern = pattern,
            Score = 10,
            SecurityScore = 10
        };

        // Check for anti-patterns in metadata
        if (pattern.Metadata.TryGetValue("is_positive_pattern", out var isPositive) && !(bool)isPositive)
        {
            var severity = pattern.Metadata.TryGetValue("severity", out var sev) ? sev.ToString() : "Medium";
            var severityLevel = severity switch
            {
                "Critical" => IssueSeverity.Critical,
                "High" => IssueSeverity.High,
                "Low" => IssueSeverity.Low,
                _ => IssueSeverity.Medium
            };

            var scoreImpact = severity switch
            {
                "Critical" => 5,
                "High" => 3,
                "Medium" => 2,
                _ => 1
            };

            result.Issues.Add(new ValidationIssue
            {
                Severity = severityLevel,
                Category = IssueCategory.Security,
                Message = $"Terraform Anti-Pattern: {pattern.Implementation}",
                ScoreImpact = scoreImpact,
                FixGuidance = pattern.BestPractice
            });
            result.Score -= scoreImpact;
            
            // Deduct security score for security anti-patterns
            if (pattern.Category == PatternCategory.SecurityAntiPattern)
            {
                result.SecurityScore -= severityLevel == IssueSeverity.Critical ? 5 : 3;
            }
        }

        // Pattern-specific validations
        switch (pattern.Name)
        {
            case "Terraform_Variable":
                if (pattern.Metadata.TryGetValue("has_type", out var hasType) && !(bool)hasType)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Medium,
                        Category = IssueCategory.BestPractice,
                        Message = "Variable without type constraint - can lead to unexpected values",
                        ScoreImpact = 1,
                        FixGuidance = "Add 'type = string' or appropriate type constraint to variable block"
                    });
                    result.Score -= 1;
                }
                
                if (pattern.Metadata.TryGetValue("has_description", out var hasDesc) && !(bool)hasDesc)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Low,
                        Category = IssueCategory.BestPractice,
                        Message = "Variable without description - reduces maintainability",
                        ScoreImpact = 1,
                        FixGuidance = "Add 'description' to document variable purpose"
                    });
                    result.Score -= 1;
                }
                
                if (pattern.Metadata.TryGetValue("has_validation", out var hasVal) && (bool)hasVal)
                {
                    result.Recommendations.Add("Excellent! Variable validation rules prevent invalid inputs");
                }
                break;

            case "Terraform_Module":
                if (pattern.Metadata.TryGetValue("has_version_pinning", out var hasVersion) && !(bool)hasVersion)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.High,
                        Category = IssueCategory.Reliability,
                        Message = "Module without version pinning - can cause unexpected breaking changes",
                        ScoreImpact = 3,
                        FixGuidance = "Add 'version = \"~> 1.0\"' to module block with appropriate version constraint"
                    });
                    result.Score -= 3;
                }
                break;

            case "Terraform_RemoteBackend":
                if (pattern.Metadata.TryGetValue("has_encryption", out var hasEnc) && !(bool)hasEnc)
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = IssueSeverity.Critical,
                        Category = IssueCategory.Security,
                        Message = "Remote backend without encryption - state may contain sensitive data",
                        ScoreImpact = 4,
                        FixGuidance = "Enable encryption: AWS S3 use 'encrypt = true', Azure use storage account encryption"
                    });
                    result.Score -= 4;
                    result.SecurityScore -= 3;
                }
                else
                {
                    result.Recommendations.Add("Excellent! Remote backend with encryption protects sensitive state data");
                }
                break;

            case "Terraform_StateLocking":
                result.Recommendations.Add("Good! State locking prevents concurrent modifications and state corruption");
                break;

            case "Terraform_VersionPinning":
                result.Recommendations.Add("Best practice: Version pinning ensures reproducible deployments");
                break;

            case "Terraform_LifecycleRules":
                if (pattern.Metadata.TryGetValue("has_prevent_destroy", out var hasPrevent) && (bool)hasPrevent)
                {
                    result.Recommendations.Add("Excellent! prevent_destroy lifecycle rule protects critical resources");
                }
                if (pattern.Metadata.TryGetValue("has_create_before_destroy", out var hasCreate) && (bool)hasCreate)
                {
                    result.Recommendations.Add("Good! create_before_destroy ensures zero-downtime replacements");
                }
                break;

            case "Terraform_SensitiveVariable":
                result.Recommendations.Add("Excellent! Marking variables as sensitive prevents accidental exposure in logs");
                break;

            case "Terraform_Encryption":
                result.Recommendations.Add("Good security: Encryption at rest protects sensitive data");
                break;

            case "Terraform_ResourceTagging":
                result.Recommendations.Add("Best practice: Resource tagging enables cost tracking and organization");
                break;

            case "Terraform_DynamicBlock":
                result.Recommendations.Add("Advanced pattern: Dynamic blocks enable flexible, DRY configurations");
                break;

            case "Terraform_Workspaces":
                result.Recommendations.Add("Good! Workspaces enable multi-environment management with same code");
                break;

            // Anti-patterns with auto-fix
            case "Terraform_HardcodedSecret_AntiPattern":
                result.AutoFixCode = @"# Use variable instead of hardcoded secret
variable ""database_password"" {
  description = ""Database password""
  type        = string
  sensitive   = true
}

resource ""aws_db_instance"" ""example"" {
  password = var.database_password  # Reference variable
}

# Pass via environment variable: export TF_VAR_database_password=""secret""
# Or use AWS Secrets Manager data source";
                break;

            case "Terraform_MissingRemoteState_AntiPattern":
                result.AutoFixCode = @"# Add to terraform block:
terraform {
  backend ""s3"" {  # or ""azurerm"", ""gcs"", etc.
    bucket         = ""my-terraform-state""
    key            = ""prod/terraform.tfstate""
    region         = ""us-east-1""
    encrypt        = true  # Enable encryption!
    dynamodb_table = ""terraform-state-lock""  # Enable locking!
  }
}";
                break;

            case "Terraform_UnversionedProvider_AntiPattern":
                result.AutoFixCode = @"# Add to terraform block:
terraform {
  required_providers {
    aws = {
      source  = ""hashicorp/aws""
      version = ""~> 5.0""  # Pin to major version
    }
  }
}";
                break;

            case "Terraform_MissingTags_AntiPattern":
                result.AutoFixCode = @"# Add tags to resources:
resource ""aws_instance"" ""example"" {
  # ... other config ...
  
  tags = {
    Name        = ""my-instance""
    Environment = var.environment
    Owner       = ""team-platform""
    CostCenter  = ""engineering""
    ManagedBy   = ""terraform""
  }
}";
                break;
        }

        result.Score = Math.Max(0, result.Score);
        result.SecurityScore = Math.Max(0, result.SecurityScore);
        result.Summary = $"Terraform Pattern Quality: {result.Grade} ({result.Score}/10) | Security: {result.SecurityScore}/10";

        return result;
    }

    public PatternMigrationPath? GenerateMigrationPath(CodePattern pattern, bool includeCodeExample = true)
    {
        return null;
    }
}

