using MemoryAgent.Server.Models;
using System.Text.RegularExpressions;

namespace MemoryAgent.Server.CodeAnalysis;

/// <summary>
/// TypeScript Decorator Patterns (30 Comprehensive Patterns)
/// </summary>
public partial class JavaScriptPatternDetector : IPatternDetector
{
    #region TypeScript Decorator Patterns (30 Comprehensive Patterns)

    // ==================== ANGULAR DECORATORS ====================
    
    private List<CodePattern> DetectAngularDecorators(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();

        // @Component
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"@Component\s*\("))
            {
                patterns.Add(CreatePattern(
                    name: "Angular_Component",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "@Component decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use @Component to define Angular components with selector, template, and styles metadata",
                    azureUrl: "https://angular.io/api/core/Component",
                    context: context,
                    language: language
                ));
            }
        }

        // @Injectable
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"@Injectable\s*\("))
            {
                var hasProvidedIn = i + 1 < lines.Length && lines[i + 1].Contains("providedIn");
                
                patterns.Add(CreatePattern(
                    name: "Angular_Injectable",
                    type: PatternType.DependencyInjection,
                    category: PatternCategory.Operational,
                    implementation: "@Injectable decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use @Injectable with providedIn: 'root' for singleton services, enables Angular dependency injection",
                    azureUrl: "https://angular.io/api/core/Injectable",
                    context: context,
                    language: language
                ));
            }
        }

        // @Input, @Output
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@(Input|Output)\s*\(");
            if (match.Success)
            {
                var decoratorType = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"Angular_{decoratorType}",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: $"@{decoratorType} decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @{decoratorType} for component {(decoratorType == "Input" ? "input properties" : "event emitters")}, enables parent-child communication",
                    azureUrl: $"https://angular.io/api/core/{decoratorType}",
                    context: context,
                    language: language
                ));
            }
        }

        // @ViewChild, @ContentChild, @ViewChildren, @ContentChildren
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@(ViewChild|ContentChild|ViewChildren|ContentChildren)\s*\(");
            if (match.Success)
            {
                var queryType = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"Angular_{queryType}",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: $"@{queryType} decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @{queryType} to query DOM elements or components, access in ngAfterViewInit lifecycle",
                    azureUrl: $"https://angular.io/api/core/{queryType}",
                    context: context,
                    language: language
                ));
            }
        }

        // @HostListener, @HostBinding
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@Host(Listener|Binding)\s*\(");
            if (match.Success)
            {
                var hostType = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"Angular_Host{hostType}",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: $"@Host{hostType} decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @Host{hostType} to {(hostType == "Listener" ? "listen to host element events" : "bind to host element properties")}",
                    azureUrl: $"https://angular.io/api/core/Host{hostType}",
                    context: context,
                    language: language
                ));
            }
        }

        // @NgModule
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"@NgModule\s*\("))
            {
                patterns.Add(CreatePattern(
                    name: "Angular_NgModule",
                    type: PatternType.DependencyInjection,
                    category: PatternCategory.Operational,
                    implementation: "@NgModule decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use @NgModule to organize related components, directives, pipes, and services into cohesive blocks",
                    azureUrl: "https://angular.io/api/core/NgModule",
                    context: context,
                    language: language
                ));
            }
        }

        return patterns;
    }

    // ==================== NESTJS DECORATORS ====================
    
    private List<CodePattern> DetectNestJSDecorators(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();

        // @Controller
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@Controller\s*\(\s*['""]([^'""]+)['""]");
            if (match.Success)
            {
                var route = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: "NestJS_Controller",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: $"@Controller('{route}')",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use @Controller to define route prefix and organize related endpoints",
                    azureUrl: "https://docs.nestjs.com/controllers",
                    context: context,
                    language: language
                ));
            }
        }

        // @Get, @Post, @Put, @Delete, @Patch
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@(Get|Post|Put|Delete|Patch)\s*\(");
            if (match.Success)
            {
                var httpMethod = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"NestJS_{httpMethod}",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: $"@{httpMethod} decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @{httpMethod} to define {httpMethod} endpoint handler, specify route and parameters",
                    azureUrl: "https://docs.nestjs.com/controllers#routing",
                    context: context,
                    language: language
                ));
            }
        }

        // @Body, @Param, @Query, @Headers
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@(Body|Param|Query|Headers)\s*\(");
            if (match.Success)
            {
                var paramSource = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"NestJS_{paramSource}",
                    type: PatternType.ApiDesign,
                    category: PatternCategory.Operational,
                    implementation: $"@{paramSource} decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @{paramSource} to extract request {paramSource.ToLower()} with automatic validation",
                    azureUrl: "https://docs.nestjs.com/controllers#request-object",
                    context: context,
                    language: language
                ));
            }
        }

        // @UseGuards, @UseInterceptors, @UsePipes
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@Use(Guards|Interceptors|Pipes)\s*\(");
            if (match.Success)
            {
                var middlewareType = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"NestJS_Use{middlewareType}",
                    type: PatternType.Security,
                    category: PatternCategory.Security,
                    implementation: $"@Use{middlewareType} decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @Use{middlewareType} for cross-cutting concerns ({middlewareType.ToLower()}), apply at controller or method level",
                    azureUrl: $"https://docs.nestjs.com/{middlewareType.ToLower()}",
                    context: context,
                    language: language
                ));
            }
        }

        return patterns;
    }

    // ==================== TYPEORM DECORATORS ====================
    
    private List<CodePattern> DetectTypeORMDecorators(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();

        // @Entity
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"@Entity\s*\("))
            {
                patterns.Add(CreatePattern(
                    name: "TypeORM_Entity",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "@Entity decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use @Entity to map class to database table, specify table name for clarity",
                    azureUrl: "https://typeorm.io/entities",
                    context: context,
                    language: language
                ));
            }
        }

        // @Column, @PrimaryColumn, @PrimaryGeneratedColumn
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@(Column|PrimaryColumn|PrimaryGeneratedColumn)\s*\(");
            if (match.Success)
            {
                var columnType = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"TypeORM_{columnType}",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: $"@{columnType} decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @{columnType} to map property to database column with type and constraints",
                    azureUrl: "https://typeorm.io/entities#column-types",
                    context: context,
                    language: language
                ));
            }
        }

        // @ManyToOne, @OneToMany, @ManyToMany, @OneToOne
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@(ManyToOne|OneToMany|ManyToMany|OneToOne)\s*\(");
            if (match.Success)
            {
                var relationType = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"TypeORM_{relationType}",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: $"@{relationType} decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @{relationType} for {relationType} relationships, define cascade and eager loading strategies",
                    azureUrl: "https://typeorm.io/relations",
                    context: context,
                    language: language
                ));
            }
        }

        // @CreateDateColumn, @UpdateDateColumn
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@(CreateDateColumn|UpdateDateColumn)\s*\(");
            if (match.Success)
            {
                var dateColumnType = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"TypeORM_{dateColumnType}",
                    type: PatternType.StateManagement,
                    category: PatternCategory.Operational,
                    implementation: $"@{dateColumnType} decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @{dateColumnType} for automatic timestamp management (audit trail)",
                    azureUrl: "https://typeorm.io/entities#special-columns",
                    context: context,
                    language: language
                ));
            }
        }

        return patterns;
    }

    // ==================== CLASS-VALIDATOR DECORATORS ====================
    
    private List<CodePattern> DetectValidationDecorators(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();

        // @IsEmail, @IsString, @IsNumber, @IsBoolean
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@Is(Email|String|Number|Boolean|Int|Date|Array|Object|Enum)\s*\(");
            if (match.Success)
            {
                var validationType = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"ClassValidator_Is{validationType}",
                    type: PatternType.Validation,
                    category: PatternCategory.Operational,
                    implementation: $"@Is{validationType} decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @Is{validationType} for type validation with class-validator, combine with NestJS DTOs",
                    azureUrl: "https://github.com/typestack/class-validator",
                    context: context,
                    language: language
                ));
            }
        }

        // @Min, @Max, @MinLength, @MaxLength
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@(Min|Max|MinLength|MaxLength)\s*\((\d+)");
            if (match.Success)
            {
                var constraintType = match.Groups[1].Value;
                var value = match.Groups[2].Value;
                
                patterns.Add(CreatePattern(
                    name: $"ClassValidator_{constraintType}",
                    type: PatternType.Validation,
                    category: PatternCategory.Operational,
                    implementation: $"@{constraintType}({value})",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @{constraintType} to enforce {constraintType.ToLower()} constraints with meaningful error messages",
                    azureUrl: "https://github.com/typestack/class-validator#validation-decorators",
                    context: context,
                    language: language
                ));
            }
        }

        // @IsOptional, @IsNotEmpty
        for (int i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"@(IsOptional|IsNotEmpty|IsDefined|IsEmpty)\s*\(");
            if (match.Success)
            {
                var requiredType = match.Groups[1].Value;
                
                patterns.Add(CreatePattern(
                    name: $"ClassValidator_{requiredType}",
                    type: PatternType.Validation,
                    category: PatternCategory.Operational,
                    implementation: $"@{requiredType} decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: $"Use @{requiredType} to define field requirement rules clearly",
                    azureUrl: "https://github.com/typestack/class-validator",
                    context: context,
                    language: language
                ));
            }
        }

        return patterns;
    }

    // ==================== MOBX DECORATORS ====================
    
    private List<CodePattern> DetectMobXDecorators(string sourceCode, string[] lines, string filePath, string? context, string language)
    {
        var patterns = new List<CodePattern>();

        // @observable
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"@observable"))
            {
                patterns.Add(CreatePattern(
                    name: "MobX_Observable",
                    type: PatternType.StateManagement,
                    category: PatternCategory.StateManagement,
                    implementation: "@observable decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use @observable for reactive state properties in MobX, UI auto-updates on changes",
                    azureUrl: "https://mobx.js.org/observable-state.html",
                    context: context,
                    language: language
                ));
            }
        }

        // @computed
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"@computed"))
            {
                patterns.Add(CreatePattern(
                    name: "MobX_Computed",
                    type: PatternType.StateManagement,
                    category: PatternCategory.Operational,
                    implementation: "@computed decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use @computed for derived values with automatic memoization, keeps computations pure",
                    azureUrl: "https://mobx.js.org/computeds.html",
                    context: context,
                    language: language
                ));
            }
        }

        // @action
        for (int i = 0; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"@action"))
            {
                patterns.Add(CreatePattern(
                    name: "MobX_Action",
                    type: PatternType.StateManagement,
                    category: PatternCategory.Operational,
                    implementation: "@action decorator",
                    filePath: filePath,
                    lineNumber: i + 1,
                    content: GetContext(lines, i, 3),
                    bestPractice: "Use @action for methods that modify observable state, enables transaction batching",
                    azureUrl: "https://mobx.js.org/actions.html",
                    context: context,
                    language: language
                ));
            }
        }

        return patterns;
    }

    
    #endregion
}
