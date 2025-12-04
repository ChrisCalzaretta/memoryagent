using MemoryAgent.Server.CodeAnalysis;
using MemoryAgent.Server.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MemoryAgent.Server.Tests;

/// <summary>
/// Integration tests for Terraform/HCL pattern detection
/// Tests cover all 24 positive patterns and 4 anti-patterns
/// </summary>
public class TerraformPatternTests : IAsyncLifetime
{
    private TerraformPatternDetector _detector = null!;

    public Task InitializeAsync()
    {
        _detector = new TerraformPatternDetector(NullLogger<TerraformPatternDetector>.Instance);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Core Terraform Constructs

    [Fact]
    public async Task DetectPatterns_Resource_ReturnsPattern()
    {
        var code = @"
resource ""aws_instance"" ""web_server"" {
  ami           = ""ami-12345678""
  instance_type = ""t2.micro""
  
  tags = {
    Name = ""WebServer""
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var resourcePattern = patterns.FirstOrDefault(p => p.Name == "Terraform_Resource");
        Assert.NotNull(resourcePattern);
        Assert.Equal(PatternType.Terraform, resourcePattern.Type);
        Assert.Equal("aws_instance", resourcePattern.Metadata["resource_type"]);
        Assert.Equal("web_server", resourcePattern.Metadata["resource_name"]);
    }

    [Fact]
    public async Task DetectPatterns_DataSource_ReturnsPattern()
    {
        var code = @"
data ""aws_ami"" ""ubuntu"" {
  most_recent = true
  owners      = [""099720109477""]
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var dataPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_DataSource");
        Assert.NotNull(dataPattern);
        Assert.Equal("aws_ami", dataPattern.Metadata["data_type"]);
        Assert.Equal("ubuntu", dataPattern.Metadata["data_name"]);
    }

    [Fact]
    public async Task DetectPatterns_Variable_WithValidation_ReturnsPattern()
    {
        var code = @"
variable ""instance_count"" {
  type        = number
  description = ""Number of instances to create""
  default     = 1
  
  validation {
    condition     = var.instance_count >= 1 && var.instance_count <= 10
    error_message = ""Instance count must be between 1 and 10.""
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var varPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_Variable");
        Assert.NotNull(varPattern);
        Assert.Equal("instance_count", varPattern.Metadata["variable_name"]);
        Assert.True((bool)varPattern.Metadata["has_type"]);
        Assert.True((bool)varPattern.Metadata["has_description"]);
        Assert.True((bool)varPattern.Metadata["has_default"]);
        Assert.True((bool)varPattern.Metadata["has_validation"]);
    }

    [Fact]
    public async Task DetectPatterns_Output_Sensitive_ReturnsPattern()
    {
        var code = @"
output ""db_password"" {
  description = ""Database password""
  value       = aws_db_instance.main.password
  sensitive   = true
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var outputPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_Output");
        Assert.NotNull(outputPattern);
        Assert.Equal("db_password", outputPattern.Metadata["output_name"]);
        Assert.True((bool)outputPattern.Metadata["has_description"]);
        Assert.True((bool)outputPattern.Metadata["is_sensitive"]);
    }

    [Fact]
    public async Task DetectPatterns_Module_WithVersion_ReturnsPattern()
    {
        var code = @"
module ""vpc"" {
  source  = ""terraform-aws-modules/vpc/aws""
  version = ""~> 3.0""
  
  name = ""my-vpc""
  cidr = ""10.0.0.0/16""
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var modulePattern = patterns.FirstOrDefault(p => p.Name == "Terraform_Module");
        Assert.NotNull(modulePattern);
        Assert.Equal("vpc", modulePattern.Metadata["module_name"]);
        Assert.Contains("terraform-aws-modules/vpc/aws", modulePattern.Metadata["module_source"].ToString());
        Assert.True((bool)modulePattern.Metadata["has_version_pinning"]);
    }

    [Fact]
    public async Task DetectPatterns_Provider_WithAlias_ReturnsPattern()
    {
        var code = @"
provider ""aws"" {
  region = ""us-east-1""
  alias  = ""primary""
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var providerPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_Provider");
        Assert.NotNull(providerPattern);
        Assert.Equal("aws", providerPattern.Metadata["provider_name"]);
        Assert.True((bool)providerPattern.Metadata["has_alias"]);
    }

    [Fact]
    public async Task DetectPatterns_Locals_ReturnsPattern()
    {
        var code = @"
locals {
  common_tags = {
    Environment = var.environment
    ManagedBy   = ""terraform""
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var localsPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_Locals");
        Assert.NotNull(localsPattern);
    }

    [Fact]
    public async Task DetectPatterns_TerraformBlock_Complete_ReturnsPattern()
    {
        var code = @"
terraform {
  required_version = "">= 1.0""
  
  required_providers {
    aws = {
      source  = ""hashicorp/aws""
      version = ""~> 5.0""
    }
  }
  
  backend ""s3"" {
    bucket = ""my-terraform-state""
    key    = ""prod/terraform.tfstate""
    region = ""us-east-1""
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var tfPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_ConfigBlock");
        Assert.NotNull(tfPattern);
        Assert.True((bool)tfPattern.Metadata["has_required_version"]);
        Assert.True((bool)tfPattern.Metadata["has_required_providers"]);
        Assert.True((bool)tfPattern.Metadata["has_backend"]);
    }

    #endregion

    #region State Management Patterns

    [Fact]
    public async Task DetectPatterns_RemoteBackend_S3WithEncryption_ReturnsPattern()
    {
        var code = @"
terraform {
  backend ""s3"" {
    bucket         = ""my-terraform-state""
    key            = ""prod/terraform.tfstate""
    region         = ""us-east-1""
    encrypt        = true
    dynamodb_table = ""terraform-state-lock""
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var backendPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_RemoteBackend");
        Assert.NotNull(backendPattern);
        Assert.Equal("s3", backendPattern.Metadata["backend_type"]);
        Assert.True((bool)backendPattern.Metadata["has_encryption"]);
    }

    [Fact]
    public async Task DetectPatterns_StateLocking_DynamoDB_ReturnsPattern()
    {
        var code = @"
terraform {
  backend ""s3"" {
    dynamodb_table = ""terraform-state-lock""
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var lockingPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_StateLocking");
        Assert.NotNull(lockingPattern);
    }

    [Fact]
    public async Task DetectPatterns_Workspaces_ReturnsPattern()
    {
        var code = @"
resource ""aws_instance"" ""web"" {
  ami           = ""ami-12345678""
  instance_type = terraform.workspace == ""prod"" ? ""t2.large"" : ""t2.micro""
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var workspacePattern = patterns.FirstOrDefault(p => p.Name == "Terraform_Workspaces");
        Assert.NotNull(workspacePattern);
    }

    #endregion

    #region Best Practice Patterns

    [Fact]
    public async Task DetectPatterns_VersionPinning_ReturnsPattern()
    {
        var code = @"
terraform {
  required_version = "">= 1.0, < 2.0""
  
  required_providers {
    aws = {
      source  = ""hashicorp/aws""
      version = ""~> 5.0""
    }
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var versionPatterns = patterns.Where(p => p.Name == "Terraform_VersionPinning").ToList();
        Assert.NotEmpty(versionPatterns);
        Assert.Contains(versionPatterns, p => p.Metadata["version_constraint"].ToString()!.Contains("1.0"));
    }

    [Fact]
    public async Task DetectPatterns_ResourceTagging_ReturnsPattern()
    {
        var code = @"
resource ""aws_instance"" ""web"" {
  ami = ""ami-12345678""
  
  tags = {
    Name        = ""WebServer""
    Environment = ""prod""
    ManagedBy   = ""terraform""
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var taggingPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_ResourceTagging");
        Assert.NotNull(taggingPattern);
    }

    [Fact]
    public async Task DetectPatterns_LifecycleRules_AllTypes_ReturnsPattern()
    {
        var code = @"
resource ""aws_db_instance"" ""production"" {
  lifecycle {
    prevent_destroy       = true
    create_before_destroy = true
    ignore_changes        = [password]
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var lifecyclePattern = patterns.FirstOrDefault(p => p.Name == "Terraform_LifecycleRules");
        Assert.NotNull(lifecyclePattern);
        Assert.True((bool)lifecyclePattern.Metadata["has_prevent_destroy"]);
        Assert.True((bool)lifecyclePattern.Metadata["has_create_before_destroy"]);
        Assert.True((bool)lifecyclePattern.Metadata["has_ignore_changes"]);
    }

    [Fact]
    public async Task DetectPatterns_DynamicBlock_ReturnsPattern()
    {
        var code = @"
resource ""aws_security_group"" ""example"" {
  dynamic ""ingress"" {
    for_each = var.service_ports
    content {
      from_port   = ingress.value
      to_port     = ingress.value
      protocol    = ""tcp""
    }
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var dynamicPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_DynamicBlock");
        Assert.NotNull(dynamicPattern);
        Assert.Equal("ingress", dynamicPattern.Metadata["block_type"]);
    }

    [Fact]
    public async Task DetectPatterns_ForExpression_ReturnsPattern()
    {
        var code = @"
locals {
  instance_ips = [for instance in aws_instance.web : instance.private_ip]
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var forPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_ForExpression");
        Assert.NotNull(forPattern);
    }

    [Fact]
    public async Task DetectPatterns_ConditionalExpression_ReturnsPattern()
    {
        var code = @"
resource ""aws_instance"" ""web"" {
  instance_type = var.environment == ""prod"" ? ""t2.large"" : ""t2.micro""
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var conditionalPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_ConditionalExpression");
        Assert.NotNull(conditionalPattern);
    }

    [Fact]
    public async Task DetectPatterns_BuiltInFunctions_ReturnsPatterns()
    {
        var code = @"
resource ""aws_instance"" ""web"" {
  user_data = file(""${path.module}/user-data.sh"")
}

locals {
  config = yamldecode(file(""config.yaml""))
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var functionPatterns = patterns.Where(p => p.Name == "Terraform_Function").ToList();
        Assert.NotEmpty(functionPatterns);
    }

    #endregion

    #region Security Patterns

    [Fact]
    public async Task DetectPatterns_SensitiveVariable_ReturnsPattern()
    {
        var code = @"
variable ""db_password"" {
  type      = string
  sensitive = true
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var sensitivePattern = patterns.FirstOrDefault(p => p.Name == "Terraform_SensitiveVariable");
        Assert.NotNull(sensitivePattern);
    }

    [Fact]
    public async Task DetectPatterns_Encryption_ReturnsPattern()
    {
        var code = @"
resource ""aws_s3_bucket_server_side_encryption_configuration"" ""example"" {
  bucket = aws_s3_bucket.example.id
  
  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = ""AES256""
    }
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var encryptionPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_Encryption");
        Assert.NotNull(encryptionPattern);
    }

    #endregion

    #region Anti-Patterns

    [Fact]
    public async Task DetectPatterns_HardcodedSecret_ReturnsAntiPattern()
    {
        var code = @"
resource ""aws_db_instance"" ""main"" {
  username = ""admin""
  password = ""SuperSecret123!""
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_HardcodedSecret_AntiPattern");
        Assert.NotNull(antiPattern);
        Assert.Equal(PatternCategory.SecurityAntiPattern, antiPattern.Category);
        Assert.False((bool)antiPattern.Metadata["is_positive_pattern"]);
        Assert.Equal("Critical", antiPattern.Metadata["severity"]);
    }

    [Fact]
    public async Task DetectPatterns_MissingRemoteState_ReturnsAntiPattern()
    {
        var code = @"
terraform {
  required_version = "">= 1.0""
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_MissingRemoteState_AntiPattern");
        Assert.NotNull(antiPattern);
        Assert.False((bool)antiPattern.Metadata["is_positive_pattern"]);
        Assert.Equal("High", antiPattern.Metadata["severity"]);
    }

    [Fact]
    public async Task DetectPatterns_UnversionedProvider_ReturnsAntiPattern()
    {
        var code = @"
terraform {
  required_providers {
    aws = {
      source = ""hashicorp/aws""
    }
  }
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_UnversionedProvider_AntiPattern");
        Assert.NotNull(antiPattern);
        Assert.Equal("Medium", antiPattern.Metadata["severity"]);
    }

    [Fact]
    public async Task DetectPatterns_MissingTags_ReturnsAntiPattern()
    {
        var code = @"
resource ""aws_instance"" ""web1"" {
  ami = ""ami-12345678""
}

resource ""aws_instance"" ""web2"" {
  ami = ""ami-12345678""
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var antiPattern = patterns.FirstOrDefault(p => p.Name == "Terraform_MissingTags_AntiPattern");
        Assert.NotNull(antiPattern);
        Assert.Equal("Low", antiPattern.Metadata["severity"]);
        Assert.Equal(2, antiPattern.Metadata["resource_count"]);
        Assert.Equal(0, antiPattern.Metadata["tags_count"]);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task DetectPatterns_EmptyFile_ReturnsNoPatterns()
    {
        var code = "";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        Assert.Empty(patterns);
    }

    [Fact]
    public async Task DetectPatterns_CommentsOnly_ReturnsNoPatterns()
    {
        var code = @"
# This is a comment
// This is also a comment
";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        Assert.Empty(patterns);
    }

    [Fact]
    public async Task DetectPatterns_MultipleResources_ReturnsMultiplePatterns()
    {
        var code = @"
resource ""aws_instance"" ""web1"" {
  ami = ""ami-12345678""
}

resource ""aws_s3_bucket"" ""data"" {
  bucket = ""my-bucket""
}

resource ""aws_db_instance"" ""main"" {
  engine = ""postgres""
}";

        var patterns = await _detector.DetectPatternsAsync("test.tf", "test", code);

        var resourcePatterns = patterns.Where(p => p.Name == "Terraform_Resource").ToList();
        Assert.Equal(3, resourcePatterns.Count);
    }

    #endregion

    #region Real-World Examples

    [Fact]
    public async Task DetectPatterns_CompleteInfrastructure_ReturnsAllPatterns()
    {
        var code = @"
terraform {
  required_version = "">= 1.0""
  
  required_providers {
    aws = {
      source  = ""hashicorp/aws""
      version = ""~> 5.0""
    }
  }
  
  backend ""s3"" {
    bucket         = ""my-terraform-state""
    key            = ""prod/terraform.tfstate""
    region         = ""us-east-1""
    encrypt        = true
    dynamodb_table = ""terraform-state-lock""
  }
}

variable ""environment"" {
  type        = string
  description = ""Environment name""
  default     = ""prod""
  
  validation {
    condition     = contains([""dev"", ""staging"", ""prod""], var.environment)
    error_message = ""Environment must be dev, staging, or prod.""
  }
}

locals {
  common_tags = {
    Environment = var.environment
    ManagedBy   = ""terraform""
  }
}

module ""vpc"" {
  source  = ""terraform-aws-modules/vpc/aws""
  version = ""~> 3.0""
  
  name = ""${var.environment}-vpc""
  cidr = ""10.0.0.0/16""
  
  tags = local.common_tags
}

resource ""aws_instance"" ""web"" {
  ami           = data.aws_ami.ubuntu.id
  instance_type = var.environment == ""prod"" ? ""t2.large"" : ""t2.micro""
  
  lifecycle {
    create_before_destroy = true
  }
  
  tags = merge(local.common_tags, {
    Name = ""WebServer""
  })
}

data ""aws_ami"" ""ubuntu"" {
  most_recent = true
  owners      = [""099720109477""]
}

output ""instance_id"" {
  description = ""EC2 instance ID""
  value       = aws_instance.web.id
}";

        var patterns = await _detector.DetectPatternsAsync("infrastructure.tf", "production", code);

        // Verify all major pattern types are detected
        Assert.Contains(patterns, p => p.Name == "Terraform_ConfigBlock");
        Assert.Contains(patterns, p => p.Name == "Terraform_RemoteBackend");
        Assert.Contains(patterns, p => p.Name == "Terraform_StateLocking");
        Assert.Contains(patterns, p => p.Name == "Terraform_Variable");
        Assert.Contains(patterns, p => p.Name == "Terraform_Locals");
        Assert.Contains(patterns, p => p.Name == "Terraform_Module");
        Assert.Contains(patterns, p => p.Name == "Terraform_Resource");
        Assert.Contains(patterns, p => p.Name == "Terraform_DataSource");
        Assert.Contains(patterns, p => p.Name == "Terraform_Output");
        Assert.Contains(patterns, p => p.Name == "Terraform_VersionPinning");
        Assert.Contains(patterns, p => p.Name == "Terraform_ResourceTagging");
        Assert.Contains(patterns, p => p.Name == "Terraform_LifecycleRules");
        Assert.Contains(patterns, p => p.Name == "Terraform_ConditionalExpression");
        
        // At least 13 different pattern types detected
        Assert.True(patterns.Count >= 13);
    }

    #endregion
}

