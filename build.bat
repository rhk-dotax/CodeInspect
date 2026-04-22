@echo off
cd /d "%~dp0"

echo ============================================
echo   CodeInspect Build Script
echo ============================================
echo.

set CSC=
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    goto :found
)
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    goto :found
)
if exist "%WINDIR%\Microsoft.NET\Framework64\v3.5\csc.exe" (
    set "CSC=%WINDIR%\Microsoft.NET\Framework64\v3.5\csc.exe"
    goto :found
)
if exist "%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe" (
    set "CSC=%WINDIR%\Microsoft.NET\Framework\v3.5\csc.exe"
    goto :found
)

echo [ERROR] csc.exe not found.
pause
exit /b 1

:found
echo [INFO] Compiler: %CSC%
echo [INFO] Building...
echo.

"%CSC%" @build.rsp

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================
    echo   BUILD SUCCESS - CodeInspect.exe
    echo ============================================
    echo.
    echo   1. GUI Mode : CodeInspect.exe
    echo   2. Hook Mode: CodeInspect.exe --hook [filelist] [repopath]
    echo   3. Git Hook : install_hook.bat
    echo.
) else (
    echo.
    echo [ERROR] Build failed.
)

pause
