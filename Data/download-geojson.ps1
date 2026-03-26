# Download Natural Earth GeoJSON data for country borders
# Run this script to get precise country borders

$outputFile = "countries.geojson"

Write-Host "Downloading Natural Earth country borders (50m resolution)..." -ForegroundColor Cyan

# Natural Earth 50m countries (good balance between detail and performance)
$url = "https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_50m_admin_0_countries.geojson"

try {
    # Download the file
    Invoke-WebRequest -Uri $url -OutFile $outputFile -UseBasicParsing

    $fileSize = (Get-Item $outputFile).Length / 1MB
    Write-Host "Download complete! File size: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Green
    Write-Host "GeoJSON saved to: $((Get-Item $outputFile).FullName)" -ForegroundColor Green
    Write-Host ""
    Write-Host "The game will now use precise country borders!" -ForegroundColor Yellow
}
catch {
    Write-Host "Error downloading file: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternative: Download manually from:" -ForegroundColor Yellow
    Write-Host "  https://www.naturalearthdata.com/downloads/50m-cultural-vectors/" -ForegroundColor Cyan
    Write-Host "  -> Admin 0 - Countries" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Or use this direct link:" -ForegroundColor Yellow
    Write-Host "  $url" -ForegroundColor Cyan
}
