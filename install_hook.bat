@echo off
chcp 65001 >nul 2>&1
echo ============================================
echo   CodeInspect - Git Pre-Commit Hook 설치
echo ============================================
echo.

:: CodeInspect.exe 존재 확인
if not exist "%~dp0CodeInspect.exe" (
    echo [오류] CodeInspect.exe를 찾을 수 없습니다.
    echo 먼저 build.bat를 실행하여 빌드해주세요.
    pause
    exit /b 1
)

:: Git 저장소 경로 입력
set /p "REPO_PATH=Git 저장소 경로를 입력하세요: "

if not exist "%REPO_PATH%" (
    echo [오류] 경로가 존재하지 않습니다: %REPO_PATH%
    pause
    exit /b 1
)

:: .git 디렉토리 확인
if not exist "%REPO_PATH%\.git" (
    echo [오류] Git 저장소가 아닙니다. .git 폴더가 없습니다.
    echo 경로: %REPO_PATH%
    pause
    exit /b 1
)

:: hooks 디렉토리 생성
if not exist "%REPO_PATH%\.git\hooks" mkdir "%REPO_PATH%\.git\hooks"

:: 기존 hook 백업
if exist "%REPO_PATH%\.git\hooks\pre-commit" (
    echo [정보] 기존 pre-commit hook을 백업합니다...
    copy "%REPO_PATH%\.git\hooks\pre-commit" "%REPO_PATH%\.git\hooks\pre-commit.backup" >nul
)

:: EXE 경로 (슬래시 변환)
set "EXE_PATH=%~dp0CodeInspect.exe"
set "EXE_PATH=%EXE_PATH:\=/%"

:: pre-commit hook 생성
(
echo #!/bin/sh
echo # CodeInspect Pre-Commit Hook
echo # 커밋 시 취약점 분석 후 로그 기록 ^(커밋은 항상 진행^)
echo.
echo CODEINSPECT_EXE="%EXE_PATH%"
echo.
echo if [ -f "$CODEINSPECT_EXE" ]; then
echo     STAGED_FILES=$^(git diff --cached --name-only --diff-filter=ACM ^| grep -E '\.\(c^|h^|cpp^|cxx^|cc^|hpp^|hxx^|hh^|java^|cs\^)$'^)
echo.
echo     if [ -n "$STAGED_FILES" ]; then
echo         echo "[CodeInspect] 커밋 파일 취약점 분석 중..."
echo         TMPFILE=$^(mktemp^)
echo         echo "$STAGED_FILES" ^> "$TMPFILE"
echo         "$CODEINSPECT_EXE" --hook "$TMPFILE" "$^(git rev-parse --show-toplevel^)"
echo         RESULT=$?
echo         rm -f "$TMPFILE"
echo.
echo         if [ $RESULT -ne 0 ]; then
echo             echo "[CodeInspect] 취약점이 검출되었습니다. 로그를 확인해주세요."
echo             echo "[CodeInspect] ^(커밋은 정상 진행됩니다^)"
echo         else
echo             echo "[CodeInspect] 취약점이 검출되지 않았습니다."
echo         fi
echo     fi
echo fi
echo.
echo exit 0
) > "%REPO_PATH%\.git\hooks\pre-commit"

echo.
echo ============================================
echo   Hook 설치 완료!
echo ============================================
echo.
echo 위치: %REPO_PATH%\.git\hooks\pre-commit
echo.
echo - 커밋 시 자동으로 C/C++/Java/C# 파일의 취약점을 분석합니다.
echo - 취약점이 검출되더라도 커밋은 정상 진행됩니다.
echo - 분석 로그: %REPO_PATH%\codeinspect_logs\
echo.
pause
