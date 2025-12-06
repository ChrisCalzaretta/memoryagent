#!/usr/bin/env node

/**
 * TypeScript/JavaScript AST Parser using TypeScript Compiler API
 * Extracts classes, methods, imports, calls, and relationships
 * Production-quality - NO REGEX!
 */

const ts = require('typescript');
const fs = require('fs');

// Parse command-line arguments
const args = process.argv.slice(2);
if (args.length < 1) {
    console.error(JSON.stringify({ error: "Usage: node ts-parser.js <file_path> [context]" }));
    process.exit(1);
}

const filePath = args[0];
const context = args[1] || 'default';

if (!fs.existsSync(filePath)) {
    console.error(JSON.stringify({ error: `File not found: ${filePath}` }));
    process.exit(1);
}

const code = fs.readFileSync(filePath, 'utf-8');
const filePathNormalized = filePath.replace(/\\/g, '/');

// Determine language from extension
const extension = filePath.toLowerCase().match(/\.(ts|tsx|js|jsx|mjs|cjs)$/)?.[1] || 'js';
const isTypeScript = extension === 'ts' || extension === 'tsx';
const isReact = extension === 'tsx' || extension === 'jsx';

// Parse with TypeScript compiler
const sourceFile = ts.createSourceFile(
    filePath,
    code,
    ts.ScriptTarget.Latest,
    true,
    isReact ? ts.ScriptKind.TSX : (isTypeScript ? ts.ScriptKind.TS : ts.ScriptKind.JS)
);

// Results to return
const result = {
    codeElements: [],
    relationships: [],
    errors: []
};

// Add file-level entry
result.codeElements.push({
    type: 'File',
    name: filePathNormalized.split('/').pop(),
    content: code.length > 5000 ? code.substring(0, 5000) + '...' : code,
    filePath: filePathNormalized,
    context: context,
    lineNumber: 0,
    metadata: {
        file_size: code.length,
        language: isTypeScript ? 'typescript' : 'javascript',
        is_react: isReact
    }
});

// Helper: Extract JSDoc comment
function extractJSDoc(node) {
    const jsdocText = node.jsDoc?.map(doc => doc.comment || '').join('\n') || '';
    const summaryMatch = jsdocText.match(/@description\s+(.*?)(?=@|\n\n|$)/s) || jsdocText.match(/^(.*?)(?=@|\n\n|$)/s);
    return {
        summary: summaryMatch ? summaryMatch[1].trim() : '',
        fullText: jsdocText
    };
}

// Helper: Get node text
function getNodeText(node) {
    return node.getText(sourceFile);
}

// Helper: Get node line number
function getLineNumber(node) {
    const { line } = sourceFile.getLineAndCharacterOfPosition(node.getStart());
    return line + 1;
}

// Helper: Get modifiers as tags
function getModifierTags(node) {
    const tags = [];
    if (node.modifiers) {
        node.modifiers.forEach(mod => {
            tags.push(ts.SyntaxKind[mod.kind].toLowerCase().replace('keyword', ''));
        });
    }
    return tags;
}

// Helper: Extract type from type node
function getTypeName(typeNode) {
    if (!typeNode) return 'any';
    
    if (ts.isTypeReferenceNode(typeNode)) {
        return typeNode.typeName.getText(sourceFile);
    } else if (ts.isArrayTypeNode(typeNode)) {
        return `${getTypeName(typeNode.elementType)}[]`;
    } else if (ts.isUnionTypeNode(typeNode) || ts.isIntersectionTypeNode(typeNode)) {
        return typeNode.types.map(t => getTypeName(t)).join(' | ');
    }
    
    return typeNode.getText(sourceFile);
}

// Extract imports
function extractImports() {
    ts.forEachChild(sourceFile, node => {
        if (ts.isImportDeclaration(node)) {
            const moduleSpecifier = node.moduleSpecifier.text;
            const lineNumber = getLineNumber(node);
            
            // Handle named imports
            if (node.importClause) {
                // Default import: import Foo from 'module'
                if (node.importClause.name) {
                    const importedName = node.importClause.name.text;
                    result.relationships.push({
                        fromName: filePathNormalized.split('/').pop().replace(/\.(ts|tsx|js|jsx|mjs|cjs)$/, ''),
                        toName: `${moduleSpecifier}.${importedName}`,
                        type: 'Imports',
                        context: context,
                        properties: {
                            line_number: lineNumber,
                            import_type: 'default',
                            module: moduleSpecifier
                        }
                    });
                }
                
                // Named imports: import { Foo, Bar } from 'module'
                if (node.importClause.namedBindings) {
                    if (ts.isNamedImports(node.importClause.namedBindings)) {
                        node.importClause.namedBindings.elements.forEach(element => {
                            const importedName = element.name.text;
                            result.relationships.push({
                                fromName: filePathNormalized.split('/').pop().replace(/\.(ts|tsx|js|jsx|mjs|cjs)$/, ''),
                                toName: `${moduleSpecifier}.${importedName}`,
                                type: 'Imports',
                                context: context,
                                properties: {
                                    line_number: lineNumber,
                                    import_type: 'named',
                                    module: moduleSpecifier
                                }
                            });
                        });
                    }
                    // Namespace import: import * as Foo from 'module'
                    else if (ts.isNamespaceImport(node.importClause.namedBindings)) {
                        const importedName = node.importClause.namedBindings.name.text;
                        result.relationships.push({
                            fromName: filePathNormalized.split('/').pop().replace(/\.(ts|tsx|js|jsx|mjs|cjs)$/, ''),
                            toName: moduleSpecifier,
                            type: 'Imports',
                            context: context,
                            properties: {
                                line_number: lineNumber,
                                import_type: 'namespace',
                                module: moduleSpecifier,
                                alias: importedName
                            }
                        });
                    }
                }
            }
        }
    });
}

// Extract classes
function extractClasses() {
    ts.forEachChild(sourceFile, node => {
        if (ts.isClassDeclaration(node) && node.name) {
            const className = node.name.text;
            const lineNumber = getLineNumber(node);
            const jsdoc = extractJSDoc(node);
            
            // Extract signature
            let signature = `class ${className}`;
            if (node.typeParameters) {
                const typeParams = node.typeParameters.map(tp => tp.name.text).join(', ');
                signature += `<${typeParams}>`;
            }
            
            // Extract heritage (extends/implements)
            const dependencies = [];
            if (node.heritageClauses) {
                node.heritageClauses.forEach(clause => {
                    clause.types.forEach(type => {
                        const typeName = type.expression.getText(sourceFile);
                        dependencies.push(typeName);
                        
                        const relType = clause.token === ts.SyntaxKind.ExtendsKeyword ? 'Inherits' : 'Implements';
                        result.relationships.push({
                            fromName: className,
                            toName: typeName,
                            type: relType,
                            context: context,
                            properties: {
                                to_node_type: relType === 'Inherits' ? 'Class' : 'Interface',
                                line_number: lineNumber
                            }
                        });
                    });
                });
            }
            
            // Extract tags
            const tags = ['class', ...getModifierTags(node)];
            if (node.decorators) {
                node.decorators.forEach(dec => {
                    tags.push(`@${dec.expression.getText(sourceFile)}`);
                });
            }
            
            result.codeElements.push({
                type: 'Class',
                name: className,
                content: getNodeText(node),
                filePath: filePathNormalized,
                context: context,
                lineNumber: lineNumber,
                summary: jsdoc.summary,
                signature: signature,
                purpose: jsdoc.summary.substring(0, 200),
                tags: tags,
                dependencies: dependencies,
                metadata: {
                    language: isTypeScript ? 'typescript' : 'javascript',
                    type: 'class',
                    is_react: isReact
                }
            });
            
            // DEFINES relationship
            result.relationships.push({
                fromName: filePathNormalized.split('/').pop(),
                toName: className,
                type: 'Defines',
                context: context,
                properties: {
                    to_node_type: 'Class',
                    line_number: lineNumber
                }
            });
            
            // Extract methods
            node.members.forEach(member => {
                if (ts.isMethodDeclaration(member) && member.name) {
                    extractMethod(member, className);
                } else if (ts.isPropertyDeclaration(member) && member.name) {
                    extractProperty(member, className);
                }
            });
        }
    });
}

// Extract top-level functions
function extractFunctions() {
    ts.forEachChild(sourceFile, node => {
        if (ts.isFunctionDeclaration(node) && node.name) {
            extractMethod(node, null);
        }
    });
}

// Extract method/function
function extractMethod(node, parentClass) {
    const methodName = node.name.getText(sourceFile);
    const fullMethodName = parentClass ? `${parentClass}.${methodName}` : methodName;
    const lineNumber = getLineNumber(node);
    const jsdoc = extractJSDoc(node);
    
    // Extract signature
    const params = node.parameters.map(p => {
        const paramName = p.name.getText(sourceFile);
        const paramType = p.type ? `: ${getTypeName(p.type)}` : '';
        return `${paramName}${paramType}`;
    }).join(', ');
    
    const returnType = node.type ? `: ${getTypeName(node.type)}` : '';
    const isAsync = node.modifiers?.some(m => m.kind === ts.SyntaxKind.AsyncKeyword) || false;
    const asyncModifier = isAsync ? 'async ' : '';
    const signature = `${asyncModifier}function ${methodName}(${params})${returnType}`;
    
    // Extract tags
    const tags = ['function'];
    if (parentClass) tags.push('method');
    if (isAsync) tags.push('async');
    tags.push(...getModifierTags(node));
    
    if (node.decorators) {
        node.decorators.forEach(dec => {
            tags.push(`@${dec.expression.getText(sourceFile)}`);
        });
    }
    
    // Extract dependencies (parameter types, return type)
    const dependencies = [];
    node.parameters.forEach(p => {
        if (p.type && ts.isTypeReferenceNode(p.type)) {
            const typeName = p.type.typeName.getText(sourceFile);
            if (!dependencies.includes(typeName)) {
                dependencies.push(typeName);
            }
        }
    });
    
    if (node.type && ts.isTypeReferenceNode(node.type)) {
        const returnTypeName = node.type.typeName.getText(sourceFile);
        if (!dependencies.includes(returnTypeName)) {
            dependencies.push(returnTypeName);
        }
    }
    
    result.codeElements.push({
        type: 'Method',
        name: fullMethodName,
        content: getNodeText(node),
        filePath: filePathNormalized,
        context: context,
        lineNumber: lineNumber,
        summary: jsdoc.summary,
        signature: signature,
        purpose: jsdoc.summary.substring(0, 200),
        tags: tags,
        dependencies: dependencies,
        metadata: {
            language: isTypeScript ? 'typescript' : 'javascript',
            type: isAsync ? 'async_function' : 'function',
            parent_class: parentClass || 'module'
        }
    });
    
    // DEFINES relationship
    const parentName = parentClass || filePathNormalized.split('/').pop();
    result.relationships.push({
        fromName: parentName,
        toName: fullMethodName,
        type: 'Defines',
        context: context,
        properties: {
            to_node_type: 'Method',
            line_number: lineNumber
        }
    });
    
    // Extract function calls
    if (node.body) {
        extractCallsFromBlock(node.body, fullMethodName);
        extractExceptionHandling(node.body, fullMethodName);
    }
}

// Extract property
function extractProperty(node, parentClass) {
    const propertyName = node.name.getText(sourceFile);
    const fullPropertyName = `${parentClass}.${propertyName}`;
    const lineNumber = getLineNumber(node);
    const jsdoc = extractJSDoc(node);
    
    const tags = ['property', ...getModifierTags(node)];
    const signature = `${propertyName}${node.type ? `: ${getTypeName(node.type)}` : ''}`;
    
    result.codeElements.push({
        type: 'Property',
        name: fullPropertyName,
        content: getNodeText(node),
        filePath: filePathNormalized,
        context: context,
        lineNumber: lineNumber,
        summary: jsdoc.summary,
        signature: signature,
        purpose: jsdoc.summary.substring(0, 200),
        tags: tags,
        metadata: {
            language: isTypeScript ? 'typescript' : 'javascript',
            type: 'property',
            parent_class: parentClass
        }
    });
    
    result.relationships.push({
        fromName: parentClass,
        toName: fullPropertyName,
        type: 'Defines',
        context: context,
        properties: {
            to_node_type: 'Property',
            line_number: lineNumber
        }
    });
}

// Extract function calls from a block
function extractCallsFromBlock(block, callerName) {
    function visit(node) {
        if (ts.isCallExpression(node)) {
            const calledFunc = node.expression.getText(sourceFile);
            
            result.relationships.push({
                fromName: callerName,
                toName: calledFunc,
                type: 'Calls',
                context: context,
                properties: {
                    to_node_type: 'Method'
                }
            });
        }
        
        ts.forEachChild(node, visit);
    }
    
    visit(block);
}

// Extract exception handling (try/catch)
function extractExceptionHandling(block, methodName) {
    function visit(node) {
        if (ts.isTryStatement(node)) {
            // Extract catch clauses
            if (node.catchClause) {
                const catchClause = node.catchClause;
                if (catchClause.variableDeclaration && catchClause.variableDeclaration.type) {
                    const exceptionType = getTypeName(catchClause.variableDeclaration.type);
                    
                    result.relationships.push({
                        fromName: methodName,
                        toName: exceptionType,
                        type: 'Catches',
                        context: context,
                        properties: {
                            to_node_type: 'Class'
                        }
                    });
                }
            }
        }
        
        // Extract throw statements
        if (ts.isThrowStatement(node) && node.expression) {
            if (ts.isNewExpression(node.expression)) {
                const exceptionType = node.expression.expression.getText(sourceFile);
                
                result.relationships.push({
                    fromName: methodName,
                    toName: exceptionType,
                    type: 'Throws',
                    context: context,
                    properties: {
                        to_node_type: 'Class'
                    }
                });
            }
        }
        
        ts.forEachChild(node, visit);
    }
    
    visit(block);
}

// Execute extraction
try {
    extractImports();
    extractClasses();
    extractFunctions();
    
    // Output JSON result to stdout
    console.log(JSON.stringify(result, null, 0)); // No pretty-print for performance
} catch (error) {
    console.error(JSON.stringify({ 
        codeElements: [],
        relationships: [],
        errors: [error.message] 
    }));
    process.exit(1);
}









