@echo off
echo ============================================
echo CLEARING CURSOR MCP CACHE
echo ============================================
echo.
echo 1. Close Cursor completely (all windows)
echo 2. Press any key to continue...
pause > nul

echo.
echo Killing any remaining Cursor processes...
taskkill /F /IM Cursor.exe 2>nul

echo.
echo Clearing MCP cache...
rd /s /q "%APPDATA%\Cursor\User\globalStorage\anysphere.cursor-mcp" 2>nul
rd /s /q "%APPDATA%\Cursor\User\workspaceStorage" 2>nul

echo.
echo ============================================
echo CACHE CLEARED!
echo ============================================
echo.
echo Now:
echo 1. Start Cursor
echo 2. Open this workspace
echo 3. Check MCP output (Ctrl+Shift+U)
echo 4. Look for "MCP: memory-code-agent"
echo 5. You should see "8 HIGH-LEVEL tools"
echo.
pause


