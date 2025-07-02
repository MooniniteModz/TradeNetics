# PowerShell Script for Setting Up PostgreSQL Docker Container for TradeNetics

# --- Configuration ---
$containerName = "tradenetics-postgres"
$dbUser = "tradenetics_user"
$dbPassword = "your_strong_password" # IMPORTANT: Change this to a secure password
$dbName = "tradenetics_db"
$dbPort = 5432
$volumeName = "tradenetics-postgres-data"

# --- Check if Docker is running ---
$dockerRunning = docker info 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker does not seem to be running. Please start Docker and try again." -ForegroundColor Red
    exit
}

Write-Host "Docker is running. Proceeding with database setup..."

# --- Stop and Remove Existing Container ---
$existingContainer = docker ps -a -f "name=$containerName" --format "{{.ID}}"
if ($existingContainer) {
    Write-Host "Found existing container '$containerName'. Stopping and removing it..." -ForegroundColor Yellow
    docker stop $containerName
    docker rm $containerName
}

# --- Pull PostgreSQL Image ---
Write-Host "Pulling the latest postgres Docker image..."
docker pull postgres

# --- Run PostgreSQL Container ---
Write-Host "Starting new PostgreSQL container '$containerName'..."
docker run --name $containerName `
    -e POSTGRES_USER=$dbUser `
    -e POSTGRES_PASSWORD=$dbPassword `
    -e POSTGRES_DB=$dbName `
    -p "$dbPort`:5432" `
    -v "$volumeName`:/var/lib/postgresql/data" `
    -d postgres

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to start the PostgreSQL container. Please check the Docker logs." -ForegroundColor Red
    exit
}

Write-Host "Container '$containerName' started successfully."

# --- Wait for Database to be Ready ---
Write-Host "Waiting for the database to initialize..."
Start-Sleep -Seconds 15 # Wait for PostgreSQL to initialize

# --- Display Connection String ---
Write-Host "Database setup is complete." -ForegroundColor Green
Write-Host "---"
Write-Host "Connection String for appsettings.json:"
Write-Host "Host=localhost;Port=$dbPort;Database=$dbName;Username=$dbUser;Password=$dbPassword" -ForegroundColor Cyan
Write-Host "---"
Write-Host "IMPORTANT: Remember to replace 'your_strong_password' with the password you set in this script." -ForegroundColor Yellow
