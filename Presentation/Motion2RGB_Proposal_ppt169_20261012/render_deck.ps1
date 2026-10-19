$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$presentationPath = Join-Path $projectRoot 'exports/Motion2RGB_Proposal.pptx'
$previewPath = Join-Path $projectRoot 'exports/previews'
New-Item -ItemType Directory -Path $previewPath -Force | Out-Null
$application = New-Object -ComObject PowerPoint.Application
try {
    $deck = $application.Presentations.Open($presentationPath, -1, 0, 0)
    try {
        foreach ($slide in $deck.Slides) {
            $fileName = 'Slide_{0:D2}.png' -f [int]$slide.SlideIndex
            $slide.Export((Join-Path $previewPath $fileName), 'PNG', 1920, 1080)
        }
        $deck.SaveAs((Join-Path $projectRoot 'exports/Motion2RGB_Proposal.pdf'), 32)
        Write-Output ('Rendered with ' + $application.Name + ' at ' + $application.Path)
    } finally { $deck.Close() }
} finally { $application.Quit() }
