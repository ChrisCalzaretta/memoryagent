# Services Refactoring Plan
## CRITICAL: Rule Violation - Files Must Be Under 800 Lines

### Files Requiring Refactoring

| File | Current Lines | Violation | Target Files | Status |
|------|--------------|-----------|--------------|---------|
| PatternValidationService.cs | 2,913 | 3.6x | 18 validators + 1 orchestrator | 🔄 IN PROGRESS |
| McpService.cs | 2,979 | 3.7x | 8 service modules + 1 orchestrator | ⏸️ PENDING |
| BestPracticeValidationService.cs | 1,706 | 2.1x | 7 catalog modules + 1 orchestrator | ⏸️ PENDING |
| GraphService.cs | 1,373 | 1.7x | 4 repository modules + 1 orchestrator | ⏸️ PENDING |

**TOTAL**: 8,971 lines → ~38 focused files averaging ~300 lines each

---

## Phase 1: PatternValidationService Refactoring

### Architecture
**Pattern**: Strategy Pattern with Dependency Injection

**New Structure**:
```
Services/
├── PatternValidation/
│   ├── IPatternValidator.cs ✅ CREATED
│   ├── CachingPatternValidator.cs ✅ CREATED
│   ├── ResiliencePatternValidator.cs ✅ CREATED
│   ├── ValidationPatternValidator.cs ⏸️ TODO
│   ├── SecurityPatternValidator.cs ⏸️ TODO
│   ├── ErrorHandlingPatternValidator.cs ⏸️ TODO
│   ├── AgentFrameworkPatternValidator.cs ⏸️ TODO
│   ├── AgentLightningPatternValidator.cs ⏸️ TODO
│   ├── SemanticKernelPatternValidator.cs ⏸️ TODO
│   ├── AutoGenPatternValidator.cs ⏸️ TODO
│   ├── PluginArchitecturePatternValidator.cs ⏸️ TODO
│   ├── PublisherSubscriberPatternValidator.cs ⏸️ TODO
│   ├── FlutterPatternValidator.cs ⏸️ TODO
│   ├── DartPatternValidator.cs ⏸️ TODO
│   ├── MicrosoftExtensionsAIPatternValidator.cs ⏸️ TODO
│   ├── TerraformPatternValidator.cs ⏸️ TODO
│   ├── BicepPatternValidator.cs ⏸️ TODO
│   ├── ARMTemplatePatternValidator.cs ⏸️ TODO
│   └── PatternMigrationService.cs ⏸️ TODO
└── PatternValidationService.cs (orchestrator, ~200 lines) ⏸️ TODO - REFACTOR
```

### Implementation Steps

1. ✅ **DONE**: Create IPatternValidator interface
2. ✅ **DONE**: Create CachingPatternValidator
3. ✅ **DONE**: Create ResiliencePatternValidator
4. ⏸️ **TODO**: Create remaining 15 validators
5. ⏸️ **TODO**: Refactor PatternValidationService to use validators
6. ⏸️ **TODO**: Update DI registration in Program.cs
7. ⏸️ **TODO**: Update tests
8. ⏸️ **TODO**: Build & verify

---

## Phase 2: McpService Refactoring

### Current Structure Analysis
- **2,979 lines** handling 40+ MCP tools
- Tools grouped by functionality:
  - Indexing (index_file, index_directory, reindex)
  - Search (smartsearch, search_patterns)
  - Validation (validate_*, find_anti_patterns)
  - Plans & TODOs (create_plan, add_todo, etc.)
  - Transformation (transform_page, extract_component, etc.)
  - Analysis (analyze_code_complexity, dependency_chain, impact_analysis)

### New Structure
```
Services/
├── Mcp/
│   ├── IMcpToolService.cs
│   ├── McpIndexingService.cs (~300 lines)
│   ├── McpSearchService.cs (~300 lines)
│   ├── McpValidationService.cs (~400 lines)
│   ├── McpPlanService.cs (~300 lines)
│   ├── McpTransformationService.cs (~400 lines)
│   ├── McpAnalysisService.cs (~300 lines)
│   ├── McpPatternService.cs (~300 lines)
│   └── McpBestPracticeService.cs (~200 lines)
└── McpService.cs (orchestrator, ~300 lines)
```

---

## Phase 3: BestPracticeValidationService Refactoring

### Current Structure
- **1,706 lines** with 350+ best practices in ONE dictionary
- Categories: Azure, Flutter, Dart, Terraform, Bicep, ARM, Microsoft.Extensions.AI

### New Structure
```
Services/
├── BestPractices/
│   ├── IBestPracticeCatalog.cs
│   ├── AzureBestPractices.cs (~250 lines)
│   ├── FlutterDartBestPractices.cs (~250 lines)
│   ├── TerraformBestPractices.cs (~200 lines)
│   ├── BicepARMBestPractices.cs (~250 lines)
│   ├── AIExtensionsBestPractices.cs (~200 lines)
│   ├── CorePatternsBestPractices.cs (~300 lines)
│   └── BestPracticeCatalogAggregator.cs (~200 lines)
└── BestPracticeValidationService.cs (orchestrator, ~250 lines)
```

---

## Phase 4: GraphService Refactoring

### Current Structure
- **1,373 lines** handling all Neo4j operations
- Mix of pattern, code, and relationship operations

### New Structure
```
Services/
├── Graph/
│   ├── IGraphRepository.cs
│   ├── PatternGraphRepository.cs (~350 lines)
│   ├── CodeGraphRepository.cs (~350 lines)
│   ├── RelationshipGraphRepository.cs (~300 lines)
│   └── GraphQueryBuilder.cs (~200 lines)
└── GraphService.cs (orchestrator, ~300 lines)
```

---

## Testing Strategy

### For Each Refactored File
1. **Unit Tests**: Test each new validator/service individually
2. **Integration Tests**: Test orchestrator with real dependencies
3. **Regression Tests**: Ensure existing functionality unchanged

### Test Files to Update/Create
- PatternValidationServiceTests.cs → Update to test orchestrator
- CachingPatternValidatorTests.cs → NEW
- ResiliencePatternValidatorTests.cs → NEW
- [Continue for all 38 new service files]

### Test Coverage Target
- Maintain >80% code coverage
- All existing tests must pass
- New tests for all new services

---

## Migration Checklist

### Before Starting Each Phase
- [ ] Review current implementation
- [ ] Identify all dependencies
- [ ] Plan interface contracts
- [ ] Review existing tests

### During Refactoring
- [ ] Create new service files
- [ ] Update orchestrator
- [ ] Update DI registration
- [ ] Update tests
- [ ] Build successfully
- [ ] All tests pass

### After Completing Each Phase
- [ ] Code review
- [ ] Performance testing
- [ ] Integration testing
- [ ] Documentation update

---

## Estimated Effort

| Phase | Files to Create | Files to Update | Est. Time |
|-------|----------------|-----------------|-----------|
| Phase 1 | 18 validators | 2 (service + DI) | 4-6 hours |
| Phase 2 | 9 MCP modules | 2 (service + DI) | 3-4 hours |
| Phase 3 | 8 catalog modules | 2 (service + DI) | 2-3 hours |
| Phase 4 | 5 graph repos | 2 (service + DI) | 2-3 hours |
| Testing | 38+ test files | All existing | 4-6 hours |

**TOTAL**: 15-22 hours of focused development work

---

## Success Criteria

✅ All services under 800 lines
✅ Zero breaking changes to public APIs
✅ All existing tests pass
✅ New tests for all new services
✅ Build succeeds with zero warnings
✅ Code coverage maintained or improved
✅ Performance not degraded

---

## Current Progress

- [x] Created refactoring plan
- [x] Created IPatternValidator interface
- [x] Created CachingPatternValidator
- [x] Created ResiliencePatternValidator
- [ ] Create remaining 15 validators
- [ ] Refactor PatternValidationService
- [ ] Complete phases 2-4
- [ ] Update all tests
- [ ] Final verification

**Status**: 🔄 5% Complete (2 of 38 files created)

