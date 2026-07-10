# Automated headless build/debug routine for the Meridian Unity project.
#
#   powershell -File Tools\build.ps1                 # fast: compile-check all scripts
#   powershell -File Tools\build.ps1 -Mode build     # full: build a Windows player
#
# Runs the Unity editor in batchmode (no window, no interaction), captures the log, and
# extracts compile errors / build result so the build can be verified without opening Unity
# or pressing Play. Exit code 0 = success, non-zero = failure (errors printed).
#
# One-time prerequisite: the Unity Personal license must be activated once by signing into
# Unity Hub with a Unity account (free). Batchmode can't activate it and will report a
# licensing error until that's done once; after that it runs fully offline/headless.

param(
    [ValidateSet('compile','build')]
    [string]$Mode = 'compile'
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$editorVersion = (Get-Content (Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt') |
    Where-Object { $_ -match '^m_EditorVersion:' }) -replace 'm_EditorVersion:\s*', ''
$editorVersion = $editorVersion.Trim()

$unityExe = "C:\Program Files\Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"
if (-not (Test-Path $unityExe)) {
    # Fall back to whatever editor is installed if the exact version isn't present.
    $found = Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($found) { $unityExe = $found } else { throw "No Unity editor found. Expected $unityExe" }
}

$method = if ($Mode -eq 'build') { 'HeadlessBuild.BuildWindows' } else { 'HeadlessBuild.CompileCheck' }
$logFile = Join-Path $projectPath 'Tools\last_build.log'
if (Test-Path $logFile) { Remove-Item $logFile -Force }

Write-Host "== Unity: $unityExe"
Write-Host "== Mode:  $Mode  ->  $method"
Write-Host "== Running batchmode (first run imports the project; that can take several minutes)..."

# Paths contain a space ("Geo political"), and Start-Process -ArgumentList does NOT quote
# array elements — so build one string with every path explicitly double-quoted.
$argLine = "-batchmode -nographics -quit " +
           "-projectPath `"$projectPath`" " +
           "-logFile `"$logFile`" " +
           "-executeMethod $method " +
           "-buildTarget Win64"

$proc = Start-Process -FilePath $unityExe -ArgumentList $argLine -PassThru -Wait
$exit = $proc.ExitCode

Write-Host "== Unity exited with code $exit"
Write-Host "== ---- errors / results from log ----"

if (Test-Path $logFile) {
    $lines = Get-Content $logFile
    $interesting = $lines | Where-Object {
        $_ -match 'error CS\d+' -or
        $_ -match ': error' -or
        $_ -match 'Compilation failed' -or
        $_ -match 'Exception' -or
        $_ -match '\[headlessbuild\]' -or
        $_ -match 'Licensing' -or
        $_ -match 'No valid Unity Editor license'
    } | Select-Object -Unique
    if ($interesting) { $interesting | ForEach-Object { Write-Host $_ } }
    else { Write-Host "(no error/result lines matched; see full log at $logFile)" }
} else {
    Write-Host "(no log file produced at $logFile)"
}

exit $exit
