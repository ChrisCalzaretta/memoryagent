#!/bin/bash
# Smart .NET build script
# - Detects project type (console vs web)
# - Creates appropriate .csproj if missing
# - Auto-detects packages from using statements
# - Supports .NET 8, 9, 10

set -e

WORK_DIR="${1:-.}"
TARGET_FRAMEWORK="${2:-net9.0}"
cd "$WORK_DIR"

echo "🔧 Smart .NET Build - Target: $TARGET_FRAMEWORK"
echo "   Working directory: $(pwd)"

# Validate target framework
case "$TARGET_FRAMEWORK" in
    net8.0|net9.0|net10.0)
        echo "   ✅ Target framework: $TARGET_FRAMEWORK"
        ;;
    *)
        echo "   ⚠️ Unsupported framework: $TARGET_FRAMEWORK (defaulting to net9.0)"
        TARGET_FRAMEWORK="net9.0"
        ;;
esac

# Check if .csproj already exists
if ls *.csproj 1> /dev/null 2>&1; then
    echo "📦 Found existing .csproj - running restore and build"
    dotnet restore --verbosity quiet
    dotnet build --nologo --verbosity quiet
    exit $?
fi

# Check for global.json to override target framework
if [ -f "global.json" ]; then
    SDK_VERSION=$(grep -oP '"version":\s*"\K[^"]+' global.json 2>/dev/null || echo "")
    if [ -n "$SDK_VERSION" ]; then
        echo "📋 Found global.json with SDK version: $SDK_VERSION"
        case "$SDK_VERSION" in
            8.*) TARGET_FRAMEWORK="net8.0" ;;
            9.*) TARGET_FRAMEWORK="net9.0" ;;
            10.*) TARGET_FRAMEWORK="net10.0" ;;
        esac
        echo "   Adjusted target framework to: $TARGET_FRAMEWORK"
    fi
fi

# Check if it's a web project (ASP.NET Core indicators)
IS_WEB=false
if grep -rq "Microsoft.AspNetCore" *.cs 2>/dev/null || \
   grep -rq "WebApplication" *.cs 2>/dev/null || \
   grep -rq "IApplicationBuilder" *.cs 2>/dev/null || \
   grep -rq "app.MapGet" *.cs 2>/dev/null || \
   grep -rq "app.MapControllers" *.cs 2>/dev/null || \
   grep -rq "builder.Services" *.cs 2>/dev/null || \
   grep -rq "IWebHostEnvironment" *.cs 2>/dev/null; then
    IS_WEB=true
    echo "🌐 Detected ASP.NET Core web project"
else
    echo "💻 Detected console application"
fi

# Detect packages needed from using statements
echo "🔍 Analyzing using statements for package references..."

# Package mappings: namespace prefix -> package name
declare -A PACKAGE_MAP=(
    # TEST FRAMEWORKS (CRITICAL - most common missing packages!)
    ["Xunit"]="xunit"
    ["NUnit"]="NUnit"
    ["Microsoft.VisualStudio.TestTools"]="MSTest.TestFramework"
    ["Moq"]="Moq"
    ["FluentAssertions"]="FluentAssertions"
    ["NSubstitute"]="NSubstitute"
    ["Bogus"]="Bogus"
    
    # JSON
    ["Newtonsoft.Json"]="Newtonsoft.Json"
    
    # OpenAPI/Swagger (CRITICAL for Web APIs)
    ["Microsoft.OpenApi"]="Microsoft.OpenApi"
    ["Swashbuckle"]="Swashbuckle.AspNetCore"
    ["NSwag"]="NSwag.AspNetCore"
    
    # Microsoft.Extensions.*
    ["Microsoft.Extensions.DependencyInjection"]="Microsoft.Extensions.DependencyInjection"
    ["Microsoft.Extensions.Logging"]="Microsoft.Extensions.Logging"
    ["Microsoft.Extensions.Configuration"]="Microsoft.Extensions.Configuration"
    ["Microsoft.Extensions.Http"]="Microsoft.Extensions.Http"
    ["Microsoft.Extensions.Hosting"]="Microsoft.Extensions.Hosting"
    ["Microsoft.Extensions.Options"]="Microsoft.Extensions.Options"
    ["Microsoft.Extensions.Caching"]="Microsoft.Extensions.Caching.Memory"
    
    # Data Access
    ["Microsoft.EntityFrameworkCore"]="Microsoft.EntityFrameworkCore"
    ["System.Text.Json"]="System.Text.Json"
    ["Dapper"]="Dapper"
    ["CsvHelper"]="CsvHelper"
    ["StackExchange.Redis"]="StackExchange.Redis"
    ["MongoDB.Driver"]="MongoDB.Driver"
    ["Npgsql"]="Npgsql"
    ["MySql.Data"]="MySql.Data"
    ["Microsoft.Data.SqlClient"]="Microsoft.Data.SqlClient"
    
    # Common Libraries
    ["Polly"]="Polly"
    ["FluentValidation"]="FluentValidation"
    ["MediatR"]="MediatR"
    ["AutoMapper"]="AutoMapper"
    ["Serilog"]="Serilog"
    ["Humanizer"]="Humanizer"
    
    # API/Web
    ["Swashbuckle"]="Swashbuckle.AspNetCore"
    ["NSwag"]="NSwag.AspNetCore"
    ["RestSharp"]="RestSharp"
    ["Refit"]="Refit"
)

# Extract unique using statements from all .cs files
PACKAGES=""
USINGS=$(grep -rh "^using " *.cs 2>/dev/null | sed 's/using //g' | sed 's/;//g' | sort -u)

for NS in $USINGS; do
    for KEY in "${!PACKAGE_MAP[@]}"; do
        if [[ "$NS" == "$KEY"* ]]; then
            PKG="${PACKAGE_MAP[$KEY]}"
            if [[ ! "$PACKAGES" == *"$PKG"* ]]; then
                PACKAGES="$PACKAGES $PKG"
                echo "   📦 $NS → $PKG"
            fi
        fi
    done
done

# Add companion packages for test frameworks (they need multiple packages to work)
if [[ "$PACKAGES" == *"xunit"* ]]; then
    echo "   📦 Adding xUnit companion packages..."
    PACKAGES="$PACKAGES xunit.runner.visualstudio Microsoft.NET.Test.Sdk"
fi
if [[ "$PACKAGES" == *"NUnit"* ]]; then
    echo "   📦 Adding NUnit companion packages..."
    PACKAGES="$PACKAGES NUnit3TestAdapter Microsoft.NET.Test.Sdk"
fi
if [[ "$PACKAGES" == *"MSTest.TestFramework"* ]]; then
    echo "   📦 Adding MSTest companion packages..."
    PACKAGES="$PACKAGES MSTest.TestAdapter Microsoft.NET.Test.Sdk"
fi

# Create appropriate project file
if [ "$IS_WEB" = true ]; then
    echo "🌐 Creating ASP.NET Core web project (TempApp.csproj)"
    SDK_TYPE="Microsoft.NET.Sdk.Web"
else
    echo "💻 Creating console project (TempApp.csproj)"
    SDK_TYPE="Microsoft.NET.Sdk"
fi

cat > TempApp.csproj << CSPROJ
<Project Sdk="$SDK_TYPE">
  <PropertyGroup>
    <TargetFramework>$TARGET_FRAMEWORK</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
CSPROJ

# Only add OutputType for console apps (web apps don't need it)
if [ "$IS_WEB" = false ]; then
    echo "    <OutputType>Exe</OutputType>" >> TempApp.csproj
fi

cat >> TempApp.csproj << CSPROJ
  </PropertyGroup>
  <ItemGroup>
CSPROJ

# Add detected packages (using floating version *)
for PKG in $PACKAGES; do
    echo "    <PackageReference Include=\"$PKG\" Version=\"*\" />" >> TempApp.csproj
done

# Close the csproj
cat >> TempApp.csproj << CSPROJ
  </ItemGroup>
</Project>
CSPROJ

echo ""
echo "📄 Generated TempApp.csproj:"
echo "----------------------------------------"
cat TempApp.csproj
echo "----------------------------------------"
echo ""

# Restore packages
echo "📦 Restoring NuGet packages..."
if ! dotnet restore --verbosity quiet; then
    echo "⚠️ Restore failed, trying with verbose output:"
    dotnet restore
fi

# Build
echo "🔨 Building..."
if dotnet build --nologo --verbosity quiet; then
    echo "✅ Build succeeded!"
else
    echo "❌ Build failed"
    # Show more verbose output on failure
    dotnet build --nologo
    exit 1
fi

