#!/usr/bin/env python3
"""
Test Python AST Parser manually to debug issues
"""

import ast
import sys
import json

def test_python_parser(file_path):
    """Test parsing a Python file with ast module"""
    
    print(f"🔍 Testing Python AST Parser on: {file_path}")
    print("=" * 60)
    print()
    
    try:
        # Read the file
        with open(file_path, 'r', encoding='utf-8') as f:
            code = f.read()
        
        print(f"✅ File read successfully ({len(code)} chars)")
        print()
        
        # Parse with Python AST
        tree = ast.parse(code, file_path)
        print("✅ AST parsing successful")
        print()
        
        # Extract classes
        classes = [node for node in ast.walk(tree) if isinstance(node, ast.ClassDef)]
        print(f"📦 Classes found: {len(classes)}")
        for cls in classes:
            print(f"   - {cls.name} (line {cls.lineno})")
        print()
        
        # Extract functions
        functions = [node for node in ast.walk(tree) if isinstance(node, ast.FunctionDef)]
        print(f"🔧 Functions found: {len(functions)}")
        for func in functions[:10]:  # First 10
            print(f"   - {func.name} (line {func.lineno})")
        if len(functions) > 10:
            print(f"   ... and {len(functions) - 10} more")
        print()
        
        # Extract imports
        imports = []
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                for alias in node.names:
                    imports.append(alias.name)
            elif isinstance(node, ast.ImportFrom):
                module = node.module or ""
                for alias in node.names:
                    imports.append(f"{module}.{alias.name}")
        
        print(f"📥 Imports found: {len(imports)}")
        for imp in imports[:10]:
            print(f"   - {imp}")
        if len(imports) > 10:
            print(f"   ... and {len(imports) - 10} more")
        print()
        
        # Extract method calls (sample from first function)
        if functions:
            first_func = functions[0]
            calls = [node for node in ast.walk(first_func) if isinstance(node, ast.Call)]
            print(f"📞 Method calls in {first_func.name}(): {len(calls)}")
            for call in calls[:5]:
                if isinstance(call.func, ast.Name):
                    print(f"   - {call.func.id}()")
                elif isinstance(call.func, ast.Attribute):
                    print(f"   - {ast.unparse(call.func)}()")
        print()
        
        print("=" * 60)
        print("✅ PYTHON AST PARSING: SUCCESS!")
        print("=" * 60)
        return True
        
    except SyntaxError as e:
        print(f"❌ SYNTAX ERROR: {e}")
        print(f"   Line {e.lineno}: {e.text}")
        return False
    except Exception as e:
        print(f"❌ ERROR: {type(e).__name__}: {e}")
        import traceback
        traceback.print_exc()
        return False

if __name__ == "__main__":
    if len(sys.argv) > 1:
        file_path = sys.argv[1]
    else:
        # Default test file
        file_path = r"E:\GitHub\AgentTrader\agents\base_agent.py"
    
    if not file_path.endswith('.py'):
        print("❌ Please provide a .py file")
        sys.exit(1)
    
    success = test_python_parser(file_path)
    sys.exit(0 if success else 1)











