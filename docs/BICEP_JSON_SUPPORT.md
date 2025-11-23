# Bicep and JSON File Support

## Overview

The Memory Code Agent now supports indexing and chunking of:
- **Azure Bicep files** (`.bicep`)
- **JSON files** (`.json`) with intelligent exclusions

## Bicep File Support

### What's Indexed

The Bicep parser extracts and chunks:

1. **Resources** - Azure resource definitions
   ```bicep
   resource storageAccount 'Microsoft.Storage/storageAccounts@2021-02-01' = {
     name: 'mystorageaccount'
     location: resourceGroup().location
   }
   ```

2. **Modules** - Module references
   ```bicep
   module appService './modules/appService.bicep' = {
     name: 'appServiceDeployment'
   }
   ```

3. **Parameters** - Input parameters
   ```bicep
   param location string = 'eastus'
   param vmSize string
   ```

4. **Variables** - Bicep variables
   ```bicep
   var uniqueName = '${prefix}-${uniqueString(resourceGroup().id)}'
   ```

5. **Outputs** - Output values
   ```bicep
   output storageAccountId string = storageAccount.id
   ```

### Metadata Stored

Each Bicep element includes metadata:
- `bicep_type`: resource, module, parameter, variable, or output
- `resource_type`: Azure resource type (for resources)
- `module_path`: Path to module file (for modules)
- `param_type`: Parameter data type
- `has_default`: Whether parameter has default value

### Relationships

- **DEFINES**: File → Resource
- **USES**: File → Module

## JSON File Support

### What's Indexed

JSON files are intelligently chunked based on their structure:

1. **Objects** - Top-level and nested objects
2. **Arrays** - Array elements (first 100 items)
3. **Properties** - Significant key-value pairs

### Excluded Files

The following configuration files are **automatically excluded**:
- ✗ `appsettings.json`
- ✗ `appsettings.development.json`
- ✗ `appsettings.production.json`
- ✗ `config.json`
- ✗ `package.json`
- ✗ `package-lock.json`
- ✗ `tsconfig.json`
- ✗ `jsconfig.json`
- ✗ `.eslintrc.json`
- ✗ `.prettierrc.json`

### Chunking Strategy

JSON files are chunked hierarchically:

```json
{
  "database": {
    "connectionString": "...",
    "timeout": 30
  },
  "features": [
    {"name": "feature1"},
    {"name": "feature2"}
  ]
}
```

Creates chunks for:
- `database` (object chunk)
- `database.connectionString` (property chunk)
- `database.timeout` (property chunk)
- `features` (array chunk)
- `features[0]` (array item chunk)
- `features[1]` (array item chunk)

### Depth Limiting

- **Object depth**: Maximum 3 levels deep
- **Array items**: First 100 items only
- **Content truncation**: Large values truncated to 2000 characters

### Metadata Stored

Each JSON chunk includes:
- `file_type`: "json"
- `json_path`: Path to element (e.g., "database.connectionString")
- `json_type`: object, array, string, number, boolean, null
- `depth`: Nesting depth level
- `array_length`: Number of array elements (for arrays)
- `array_index`: Index in array (for array items)

## File Deletion Handling

When files are deleted from the filesystem:

✅ **Removed from Qdrant** (vector database)
✅ **Removed from Neo4j** (graph database)

The smart reindex feature automatically detects deleted files and removes all associated:
- Vector embeddings
- Graph nodes
- Relationships

## Supported File Patterns

Full list of indexed file types:

```
*.cs, *.vb, *.cshtml, *.razor, *.py, *.md,
*.css, *.scss, *.less,
*.js, *.jsx, *.ts, *.tsx,
*.csproj, *.vbproj, *.fsproj, *.sln,
*.json, *.yml, *.yaml, *.config,
*.bicep,
Dockerfile, *.dockerfile
```

## Example Queries

### Bicep Queries

```bash
# Find Azure resources
"Show me all storage account configurations"

# Find module usage
"Which Bicep files use the appService module?"

# Find parameters
"What parameters are defined for VM sizing?"
```

### JSON Queries

```bash
# Find configuration
"What database settings are configured?"

# Find feature flags
"Show me all feature configurations"

# Find API endpoints
"List all API endpoint definitions"
```

## Testing

To test the new parsers:

```powershell
# Index a Bicep file
$body = @{
    path = "/workspace/myproject/main.bicep"
    context = "MyProject"
} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/index/file `
    -Method POST -Body $body -ContentType 'application/json'

# Index a JSON file
$body = @{
    path = "/workspace/myproject/config/settings.json"
    context = "MyProject"
} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/index/file `
    -Method POST -Body $body -ContentType 'application/json'

# Query
$body = @{
    query = "Azure storage configurations"
    context = "MyProject"
} | ConvertTo-Json
Invoke-RestMethod -Uri http://localhost:5098/api/query `
    -Method POST -Body $body -ContentType 'application/json'
```

## Implementation Details

### New Files
- `MemoryAgent.Server/CodeAnalysis/BicepParser.cs`
- `MemoryAgent.Server/CodeAnalysis/JsonParser.cs`

### Modified Files
- `MemoryAgent.Server/CodeAnalysis/RoslynParser.cs` - Added routing for .bicep and .json
- `MemoryAgent.Server/Services/IndexingService.cs` - Added *.bicep to file patterns
- `MemoryAgent.Server/Services/ReindexService.cs` - Added *.bicep to file patterns

### File Routing Logic

```csharp
// Config files use ConfigFileParser
".json" when IsConfigFile(fileName) => ConfigFileParser

// Other JSON files use JsonParser (with exclusions)
".json" when !IsConfigFile(fileName) => JsonParser

// Bicep files use BicepParser
".bicep" => BicepParser
```

