@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Package the current working files, including uncommitted changes.
rem Usage: double-click, or run package_for_ai.bat --no-pause.

pushd "%~dp0" || exit /b 1
set "RESULT=1"

where tar.exe >nul 2>&1
if errorlevel 1 (
    echo ERROR: tar.exe is required. Use Windows 10 or newer.
    goto finish
)

set "STAMP="
for /f %%I in ('powershell.exe -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss-fff"') do set "STAMP=%%I"
if not defined STAMP (
    echo ERROR: Could not generate the archive timestamp.
    goto finish
)
set "ARCHIVE=ashes-of-a-dying-world-ai-%STAMP%.zip"

echo Creating %ARCHIVE% ...
echo Source, assets, addons, tools and docs are included.
echo Caches, temporary folders and other ZIP files are excluded.
echo.

tar.exe -a -c -f "%ARCHIVE%" ^
    --exclude=".git" ^
    --exclude=".godot" ^
    --exclude=".dotnet" ^
    --exclude=".worktrees" ^
    --exclude="__pycache__" ^
    --exclude=".pytest_cache" ^
    --exclude="*.pyc" ^
    --exclude="bin" ^
    --exclude="obj" ^
    --exclude="*.zip" ^
    scripts scenes data assets addons tools docs ^
    AGENTS.md project.godot override.cfg ^
    "Ashes of a Dying World.csproj" ^
    "Ashes of a Dying World.sln" ^
    .editorconfig .gitignore package_for_ai.bat
set "RESULT=%ERRORLEVEL%"

if not "%RESULT%"=="0" (
    echo.
    echo ERROR: Packaging failed. Any partial ZIP should not be used.
    goto finish
)

echo.
echo ZIP ready: "%CD%\%ARCHIVE%"

:finish
popd
if /i not "%~1"=="--no-pause" pause
exit /b %RESULT%
