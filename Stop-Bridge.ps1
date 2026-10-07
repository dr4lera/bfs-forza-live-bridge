$ErrorActionPreference='Stop'
'stop' | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'capture.stop')
Write-Host 'Stop signal sent. BFS restores its normal view after the feed becomes stale. Neither game is closed.'
