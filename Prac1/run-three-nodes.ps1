$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot 'Prac1\Prac1.csproj'
$taskData = Join-Path $projectRoot 'data'
New-Item -ItemType Directory -Force -Path $taskData | Out-Null

foreach ($port in 5101, 5102, 5103) {
    $chainFile = Join-Path $taskData "chain-$port.json"
    Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', $projectFile, '--', '--port', $port, '--data', $chainFile) -WorkingDirectory $projectRoot -WindowStyle Hidden
}

Start-Sleep -Seconds 2
$nodes = 5101, 5102, 5103 | ForEach-Object { "http://127.0.0.1:$_" }
foreach ($node in $nodes) {
    foreach ($peer in $nodes | Where-Object { $_ -ne $node }) {
        Invoke-RestMethod -Method Post -Uri "$node/peers" -ContentType 'application/json' -Body (@{ url = $peer } | ConvertTo-Json) | Out-Null
    }
}

Write-Host 'Nodes are running on ports 5101, 5102, and 5103.'
