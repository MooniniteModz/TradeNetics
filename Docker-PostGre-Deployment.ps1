# deploy-tradenectics.ps1
# TradeNectics Automated Docker Deployment Script
# Run with: .\deploy-tradenectics.ps1

param(
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath = $PWD,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$Production = $false,
    
    [Parameter(Mandatory=$false)]
    [switch]$CleanInstall = $false,
    
    [Parameter(Mandatory=$false)]
    [string]$BinanceApiKey = "",
    
    [Parameter(Mandatory=$false)]
    [string]$BinanceApiSecret = ""
)

# Script configuration
$ErrorActionPreference = "Stop"
$script:TimeStamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Colors for output
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

function Write-Success($message) { Write-Host "✅ $message" -ForegroundColor Green }
function Write-Info($message) { Write-Host "📋 $message" -ForegroundColor Cyan }
function Write-Warning($message) { Write-Host "⚠️  $message" -ForegroundColor Yellow }
function Write-Error($message) { Write-Host "❌ $message" -ForegroundColor Red }
function Write-Step($message) { Write-Host "`n🔹 $message" -ForegroundColor Blue }

# Banner
function Show-Banner {
    Clear-Host
    Write-Host @"

    ╔╦╗┬─┐┌─┐┌┬┐┌─┐╔╗╔┌─┐┌─┐┌┬┐┬┌─┐┌─┐
     ║ ├┬┘├─┤ ││├┤ ║║║├┤ │   │ ││  └─┐
     ╩ ┴└─┴ ┴─┴┘└─┘╝╚╝└─┘└─┘ ┴ ┴└─┘└─┘
        AUTOMATED DOCKER DEPLOYMENT
    
"@ -ForegroundColor Cyan
    Write-Host "    Version: 1.0.0 | $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Gray
    Write-Host "    ═══════════════════════════════════════════════" -ForegroundColor Gray
    Write-Host ""
}

# Check prerequisites
function Test-Prerequisites {
    Write-Step "Checking prerequisites..."
    
    $missingPrereqs = @()
    
    # Check Docker
    try {
        $dockerVersion = docker --version
        Write-Success "Docker installed: $dockerVersion"
    }
    catch {
        $missingPrereqs += "Docker Desktop"
    }
    
    # Check Docker Compose
    try {
        $composeVersion = docker-compose --version
        Write-Success "Docker Compose installed: $composeVersion"
    }
    catch {
        $missingPrereqs += "Docker Compose"
    }
    
    # Check if Docker is running
    try {
        docker ps | Out-Null
        Write-Success "Docker daemon is running"
    }
    catch {
        Write-Error "Docker daemon is not running!"
        Write-Warning "Please start Docker Desktop and try again."
        exit 1
    }
    
    if ($missingPrereqs.Count -gt 0) {
        Write-Error "Missing prerequisites: $($missingPrereqs -join ', ')"
        Write-Info "Please install missing components and try again."
        
        if ($missingPrereqs -contains "Docker Desktop") {
            Write-Info "Download Docker Desktop from: https://www.docker.com/products/docker-desktop"
        }
        exit 1
    }
}

# Create project structure
function Initialize-ProjectStructure {
    Write-Step "Setting up project structure..."
    
    Set-Location $ProjectPath
    
    # Create directories
    $directories = @("logs", "models", "scripts", "backups")
    foreach ($dir in $directories) {
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Name $dir -Force | Out-Null
            Write-Success "Created directory: $dir"
        }
    }
}

# Create Docker files
function New-DockerFiles {
    Write-Step "Creating Docker configuration files..."
    
    # Dockerfile
    $dockerfileContent = @'
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["TradeNectics.csproj", "./"]
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install postgresql client for health checks
RUN apt-get update && apt-get install -y postgresql-client && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Create logs directory
RUN mkdir -p /app/logs

# Create non-root user
RUN useradd -m -s /bin/bash tradenectics && \
    chown -R tradenectics:tradenectics /app

USER tradenectics

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD dotnet TradeNectics.dll --health || exit 1

ENTRYPOINT ["dotnet", "TradeNectics.dll"]
'@

    # docker-compose.yml
    $dockerComposeContent = @'
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    container_name: tradenectics-db
    restart: unless-stopped
    environment:
      POSTGRES_DB: tradenectics
      POSTGRES_USER: tradenectics
      POSTGRES_PASSWORD: ${DB_PASSWORD:-changeme}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init-db.sql:/docker-entrypoint-initdb.d/init-db.sql
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U tradenectics"]
      interval: 10s
      timeout: 5s
      retries: 5

  tradenectics:
    build: .
    container_name: tradenectics-bot
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      # Database
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=tradenectics;Username=tradenectics;Password=${DB_PASSWORD:-changeme}"
      
      # Binance API (set these in .env file)
      BINANCE_API_KEY: ${BINANCE_API_KEY}
      BINANCE_API_SECRET: ${BINANCE_API_SECRET}
      
      # Trading Configuration
      Trading__PaperTradingMode: ${PAPER_TRADING:-true}
      Trading__MinConfidenceScore: ${MIN_CONFIDENCE:-0.7}
      Trading__MaxPositionSize: ${MAX_POSITION_SIZE:-0.02}
      Trading__StopLossPercent: ${STOP_LOSS_PERCENT:-0.05}
      Trading__MaxDailyLoss: ${MAX_DAILY_LOSS:-0.10}
      
      # Logging
      Logging__LogLevel__Default: Information
      ASPNETCORE_ENVIRONMENT: Production
    volumes:
      - ./logs:/app/logs
      - ./models:/app/models
    networks:
      - tradenectics-network

  pgadmin:
    image: dpage/pgadmin4:latest
    container_name: tradenectics-pgadmin
    restart: unless-stopped
    environment:
      PGADMIN_DEFAULT_EMAIL: ${PGADMIN_EMAIL:-admin@tradenectics.com}
      PGADMIN_DEFAULT_PASSWORD: ${PGADMIN_PASSWORD:-admin}
      PGADMIN_CONFIG_SERVER_MODE: 'False'
    ports:
      - "5050:80"
    volumes:
      - pgadmin_data:/var/lib/pgadmin
    networks:
      - tradenectics-network
    depends_on:
      - postgres

volumes:
  postgres_data:
  pgadmin_data:

networks:
  tradenectics-network:
    driver: bridge
'@

    # .dockerignore
    $dockerIgnoreContent = @'
**/.dockerignore
**/.git
**/.gitignore
**/.vs
**/.vscode
**/bin
**/obj
**/.env
**/logs
**/models/*.zip
.gitattributes
.gitignore
README.md
LICENSE
*.user
*.suo
*.sln.docstates
'@

    # init-db.sql
    $initDbContent = @'
-- Create extensions if needed
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create schema
CREATE SCHEMA IF NOT EXISTS trading;

-- Grant permissions
GRANT ALL ON SCHEMA trading TO tradenectics;

-- Set search path
ALTER DATABASE tradenectics SET search_path TO public, trading;
'@

    # Write files
    Set-Content -Path "Dockerfile" -Value $dockerfileContent -Force
    Set-Content -Path "docker-compose.yml" -Value $dockerComposeContent -Force
    Set-Content -Path ".dockerignore" -Value $dockerIgnoreContent -Force
    Set-Content -Path "init-db.sql" -Value $initDbContent -Force
    
    Write-Success "Docker configuration files created"
}

# Create or update .env file
function Set-EnvironmentFile {
    Write-Step "Configuring environment variables..."
    
    $envPath = Join-Path $ProjectPath ".env"
    
    # Generate secure password
    $dbPassword = [System.Web.Security.Membership]::GeneratePassword(20, 5)
    $pgAdminPassword = [System.Web.Security.Membership]::GeneratePassword(16, 3)
    
    # Determine API key configuration
    if ($BinanceApiKey -and $BinanceApiSecret) {
        $apiKeyValue = $BinanceApiKey
        $apiSecretValue = $BinanceApiSecret
        $paperTrading = "false"
        Write-Info "Using provided Binance API credentials"
    }
    else {
        $apiKeyValue = "your_binance_api_key_here"
        $apiSecretValue = "your_binance_api_secret_here"
        $paperTrading = "true"
        Write-Warning "No API credentials provided - Paper trading mode enabled"
    }
    
    $envContent = @"
# TradeNectics Environment Configuration
# Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

# Database
DB_PASSWORD=$dbPassword

# Binance API
BINANCE_API_KEY=$apiKeyValue
BINANCE_API_SECRET=$apiSecretValue

# Trading Settings
PAPER_TRADING=$paperTrading
MIN_CONFIDENCE=0.7
MAX_POSITION_SIZE=0.02
STOP_LOSS_PERCENT=0.05
MAX_DAILY_LOSS=0.10

# pgAdmin
PGADMIN_EMAIL=admin@tradenectics.local
PGADMIN_PASSWORD=$pgAdminPassword

# Production Mode
PRODUCTION_MODE=$($Production.ToString().ToLower())
"@

    Set-Content -Path $envPath -Value $envContent -Force
    Write-Success ".env file created/updated"
    
    # Save credentials securely
    $credFile = Join-Path $ProjectPath "credentials_$script:TimeStamp.txt"
    $credContent = @"
TradeNectics Deployment Credentials
Generated: $(Get-Date)
================================

Database Password: $dbPassword
pgAdmin Email: admin@tradenectics.local
pgAdmin Password: $pgAdminPassword

Access URLs:
- pgAdmin: http://localhost:5050
- PostgreSQL: localhost:5432

IMPORTANT: Keep this file secure and delete after saving credentials!
"@
    
    Set-Content -Path $credFile -Value $credContent -Force
    Write-Warning "Credentials saved to: $credFile"
}

# Build and start containers
function Start-Deployment {
    Write-Step "Building and starting Docker containers..."
    
    try {
        if ($CleanInstall) {
            Write-Info "Performing clean installation..."
            docker-compose down -v 2>$null
            docker system prune -f
        }
        
        if (-not $SkipBuild) {
            Write-Info "Building Docker images..."
            docker-compose build --no-cache
            
            if ($LASTEXITCODE -ne 0) {
                throw "Docker build failed"
            }
        }
        
        Write-Info "Starting services..."
        if ($Production) {
            docker-compose -f docker-compose.yml up -d
        }
        else {
            docker-compose up -d
        }
        
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to start containers"
        }
        
        Write-Success "All services started successfully"
    }
    catch {
        Write-Error "Deployment failed: $_"
        exit 1
    }
}

# Wait for services to be ready
function Wait-ForServices {
    Write-Step "Waiting for services to be ready..."
    
    $maxAttempts = 30
    $attempt = 0
    
    while ($attempt -lt $maxAttempts) {
        $attempt++
        Write-Progress -Activity "Waiting for services" -Status "Attempt $attempt of $maxAttempts" -PercentComplete (($attempt / $maxAttempts) * 100)
        
        # Check PostgreSQL
        $pgReady = docker exec tradenectics-db pg_isready -U tradenectics 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Success "PostgreSQL is ready"
            break
        }
        
        Start-Sleep -Seconds 2
    }
    
    if ($attempt -eq $maxAttempts) {
        Write-Error "Services failed to start within timeout"
        exit 1
    }
    
    # Additional wait for schema creation
    Start-Sleep -Seconds 5
}

# Health check
function Test-Deployment {
    Write-Step "Running deployment health checks..."
    
    $healthChecks = @()
    
    # Check containers
    $containers = docker ps --format "table {{.Names}}\t{{.Status}}" | Select-Object -Skip 1
    Write-Info "Running containers:"
    $containers | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    
    # Check database connection
    try {
        docker exec tradenectics-db psql -U tradenectics -d tradenectics -c "SELECT 1" | Out-Null
        Write-Success "Database connection: OK"
        $healthChecks += $true
    }
    catch {
        Write-Error "Database connection: FAILED"
        $healthChecks += $false
    }
    
    # Check bot logs
    $logs = docker logs tradenectics-bot --tail 20 2>&1
    if ($logs -match "Trading bot service started") {
        Write-Success "Bot initialization: OK"
        $healthChecks += $true
    }
    else {
        Write-Warning "Bot initialization: In progress..."
        $healthChecks += $true
    }
    
    # Overall health
    if ($healthChecks -notcontains $false) {
        Write-Success "All health checks passed!"
        return $true
    }
    else {
        Write-Error "Some health checks failed"
        return $false
    }
}

# Show deployment summary
function Show-Summary {
    Write-Host "`n" -NoNewline
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    Write-Host "                    DEPLOYMENT COMPLETE!                        " -ForegroundColor Green
    Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Green
    
    Write-Host "`n📊 Access Points:" -ForegroundColor Cyan
    Write-Host "   • pgAdmin:    " -NoNewline -ForegroundColor Gray
    Write-Host "http://localhost:5050" -ForegroundColor Yellow
    Write-Host "   • PostgreSQL: " -NoNewline -ForegroundColor Gray
    Write-Host "localhost:5432" -ForegroundColor Yellow
    Write-Host "   • Logs:       " -NoNewline -ForegroundColor Gray
    Write-Host "docker logs -f tradenectics-bot" -ForegroundColor Yellow
    
    Write-Host "`n🔧 Useful Commands:" -ForegroundColor Cyan
    Write-Host "   • View logs:    " -NoNewline -ForegroundColor Gray
    Write-Host "docker-compose logs -f tradenectics" -ForegroundColor White
    Write-Host "   • Stop bot:     " -NoNewline -ForegroundColor Gray
    Write-Host "docker-compose down" -ForegroundColor White
    Write-Host "   • Restart bot:  " -NoNewline -ForegroundColor Gray
    Write-Host "docker-compose restart tradenectics" -ForegroundColor White
    Write-Host "   • Backup DB:    " -NoNewline -ForegroundColor Gray
    Write-Host "docker exec tradenectics-db pg_dump -U tradenectics tradenectics > backup.sql" -ForegroundColor White
    
    if (Test-Path "credentials_$script:TimeStamp.txt") {
        Write-Host "`n🔐 Credentials:" -ForegroundColor Red
        Write-Host "   Saved to: " -NoNewline -ForegroundColor Gray
        Write-Host "credentials_$script:TimeStamp.txt" -ForegroundColor Yellow
        Write-Host "   ⚠️  Please save these credentials and delete the file!" -ForegroundColor Red
    }
    
    Write-Host "`n✅ TradeNectics is now running!" -ForegroundColor Green
    
    if ((Get-Content .env | Select-String "PAPER_TRADING=true")) {
        Write-Host "📝 Running in PAPER TRADING mode (no real trades)" -ForegroundColor Yellow
    }
    else {
        Write-Host "💰 Running in LIVE TRADING mode" -ForegroundColor Red
    }
}

# Create helper scripts
function New-HelperScripts {
    Write-Step "Creating helper scripts..."
    
    # start.ps1
    $startScript = @'
# Quick start script
docker-compose up -d
docker-compose logs -f tradenectics
'@
    
    # stop.ps1
    $stopScript = @'
# Stop all services
docker-compose down
Write-Host "TradeNectics stopped" -ForegroundColor Green
'@
    
    # backup.ps1
    $backupScript = @'
# Backup database
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = "backups/backup_$timestamp.sql"
New-Item -ItemType Directory -Name "backups" -Force | Out-Null
docker exec tradenectics-db pg_dump -U tradenectics tradenectics > $backupFile
Write-Host "Backup created: $backupFile" -ForegroundColor Green
'@
    
    # logs.ps1
    $logsScript = @'
# View real-time logs
docker-compose logs -f tradenectics
'@
    
    Set-Content -Path "scripts/start.ps1" -Value $startScript -Force
    Set-Content -Path "scripts/stop.ps1" -Value $stopScript -Force
    Set-Content -Path "scripts/backup.ps1" -Value $backupScript -Force
    Set-Content -Path "scripts/logs.ps1" -Value $logsScript -Force
    
    Write-Success "Helper scripts created in ./scripts/"
}

# Main execution
function Main {
    Show-Banner
    
    try {
        # Run all deployment steps
        Test-Prerequisites
        Initialize-ProjectStructure
        New-DockerFiles
        Set-EnvironmentFile
        New-HelperScripts
        Start-Deployment
        Wait-ForServices
        
        # Health check
        Start-Sleep -Seconds 3
        $healthy = Test-Deployment
        
        # Show summary
        Show-Summary
        
        if (-not $healthy) {
            Write-Warning "`nSome services may still be initializing. Check logs for details."
        }
        
        # Open pgAdmin in browser
        if (-not $Production) {
            Write-Host "`nOpening pgAdmin in browser..." -ForegroundColor Cyan
            Start-Sleep -Seconds 2
            Start-Process "http://localhost:5050"
        }
    }
    catch {
        Write-Error "Deployment failed: $_"
        Write-Info "Check logs: docker-compose logs"
        exit 1
    }
}

# Add required assembly for password generation
Add-Type -AssemblyName System.Web

# Execute main function
Main