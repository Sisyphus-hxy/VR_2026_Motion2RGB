param([string]$SkillDirectory = '', [switch]$SkipRender)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$toolRoot = Join-Path (Split-Path $projectRoot) 'Tooling'
if (-not $SkillDirectory) {
    $SkillDirectory = Join-Path $toolRoot 'ppt-master/skills/ppt-master'
}
if (-not (Test-Path (Join-Path $SkillDirectory 'SKILL.md'))) {
    throw 'Download official ppt-master v6.7.0 first, or specify -SkillDirectory.'
}
$localDependencies = Join-Path $toolRoot 'deps'
if (Test-Path $localDependencies) { $env:PYTHONPATH = $localDependencies }
function Run-Checked([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Command failed: $Executable" }
}
Run-Checked 'python' @((Join-Path $SkillDirectory 'scripts/attribution_guard.py'))
Run-Checked 'node' @((Join-Path $projectRoot 'build_slides.mjs'), 'all')
Run-Checked 'python' @((Join-Path $SkillDirectory 'scripts/svg_quality_checker.py'), $projectRoot, '--canonical-authoring', '--stage', 'final', '--json')
Run-Checked 'python' @((Join-Path $SkillDirectory 'scripts/total_md_split.py'), $projectRoot)
$launcher = Join-Path $projectRoot 'run_skill_windows.py'
Run-Checked 'python' @($launcher, (Join-Path $SkillDirectory 'scripts/finalize_svg.py'), $projectRoot)
Run-Checked 'python' @($launcher, (Join-Path $SkillDirectory 'scripts/svg_to_pptx.py'), $projectRoot, '-o', (Join-Path $projectRoot 'exports/Motion2RGB_base.pptx'), '--no-animations')
Run-Checked 'python' @((Join-Path $projectRoot 'embed_demo.py'))
if (-not $SkipRender) { & (Join-Path $projectRoot 'render_deck.ps1') }
Run-Checked 'python' @((Join-Path $projectRoot 'verify_delivery.py'))
