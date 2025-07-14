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

# --- Create Database Tables ---
Write-Host "Creating database tables..." -ForegroundColor Yellow

$createTablesSQL = @"
-- Create MarketData table
CREATE TABLE IF NOT EXISTS "MarketData" (
    "Id" SERIAL PRIMARY KEY,
    "Symbol" VARCHAR(50) NOT NULL,
    "Timestamp" TIMESTAMP NOT NULL,
    "Open" DECIMAL(18,8) NOT NULL,
    "High" DECIMAL(18,8) NOT NULL,
    "Low" DECIMAL(18,8) NOT NULL,
    "Close" DECIMAL(18,8) NOT NULL,
    "Volume" DECIMAL(18,8) NOT NULL,
    "RSI" REAL NOT NULL,
    "MovingAverage5" REAL NOT NULL,
    "MovingAverage20" REAL NOT NULL,
    "BollingerUpper" REAL NOT NULL,
    "BollingerLower" REAL NOT NULL,
    "MACD" REAL NOT NULL,
    "Signal" REAL NOT NULL,
    "VolumeRatio" REAL NOT NULL,
    "PriceChange24h" DECIMAL(18,8) NOT NULL,
    "VolumeChange24h" DECIMAL(18,8) NOT NULL
);

-- Create unique index on Symbol and Timestamp
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MarketData_Symbol_Timestamp" 
ON "MarketData" ("Symbol", "Timestamp");

-- Create TradeRecords table
CREATE TABLE IF NOT EXISTS "TradeRecords" (
    "Id" SERIAL PRIMARY KEY,
    "Symbol" VARCHAR(50) NOT NULL,
    "Side" VARCHAR(10) NOT NULL,
    "Quantity" DECIMAL(18,8) NOT NULL,
    "Price" DECIMAL(18,8) NOT NULL,
    "ExecutedAt" TIMESTAMP NOT NULL,
    "MLPrediction" TEXT NOT NULL DEFAULT '',
    "PnL" DECIMAL(18,8) NOT NULL,
    "PortfolioValueBefore" DECIMAL(18,8) NOT NULL,
    "PortfolioValueAfter" DECIMAL(18,8) NOT NULL,
    "OrderId" VARCHAR(100) NOT NULL DEFAULT '',
    "IsPaperTrade" BOOLEAN NOT NULL DEFAULT false,
    "ConfidenceScore" REAL NOT NULL
);

-- Create index on ExecutedAt
CREATE INDEX IF NOT EXISTS "IX_TradeRecords_ExecutedAt" 
ON "TradeRecords" ("ExecutedAt");

-- Create ModelPerformances table
CREATE TABLE IF NOT EXISTS "ModelPerformances" (
    "Id" SERIAL PRIMARY KEY,
    "TrainingDate" TIMESTAMP NOT NULL,
    "Accuracy" DOUBLE PRECISION NOT NULL,
    "Precision" DOUBLE PRECISION NOT NULL,
    "Recall" DOUBLE PRECISION NOT NULL,
    "F1Score" DOUBLE PRECISION NOT NULL,
    "TrainingDataCount" INTEGER NOT NULL,
    "ModelVersion" VARCHAR(100) NOT NULL DEFAULT ''
);

-- Create PortfolioSnapshots table
CREATE TABLE IF NOT EXISTS "PortfolioSnapshots" (
    "Id" SERIAL PRIMARY KEY,
    "Timestamp" TIMESTAMP NOT NULL,
    "TotalValue" DECIMAL(18,8) NOT NULL,
    "DailyPnL" DECIMAL(18,8) NOT NULL,
    "TotalPnL" DECIMAL(18,8) NOT NULL,
    "AssetAllocation" TEXT NOT NULL DEFAULT '',
    "RiskScore" DECIMAL(18,8) NOT NULL
);

-- Create index on Timestamp
CREATE INDEX IF NOT EXISTS "IX_PortfolioSnapshots_Timestamp" 
ON "PortfolioSnapshots" ("Timestamp");
"@

# Execute the SQL commands
try {
    $env:PGPASSWORD = $dbPassword
    Write-Host "Executing table creation SQL..."
    
    $createTablesSQL | docker exec -i $containerName psql -U $dbUser -d $dbName
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Database tables created successfully!" -ForegroundColor Green
    } else {
        Write-Host "Warning: Some tables may already exist or there were minor issues during creation." -ForegroundColor Yellow
    }
} catch {
    Write-Host "Error creating tables: $_" -ForegroundColor Red
    Write-Host "The database container is running, but table creation failed." -ForegroundColor Yellow
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

# --- Verify Tables Created ---
Write-Host "Verifying tables were created..."
try {
    $env:PGPASSWORD = $dbPassword
    $tableList = "SELECT tablename FROM pg_tables WHERE schemaname = 'public';" | docker exec -i $containerName psql -U $dbUser -d $dbName -t
    
    if ($tableList -match "MarketData|TradeRecords|ModelPerformances|PortfolioSnapshots") {
        Write-Host "Tables verified successfully!" -ForegroundColor Green
    } else {
        Write-Host "Warning: Could not verify all tables were created." -ForegroundColor Yellow
    }
} catch {
    Write-Host "Could not verify table creation, but database is running." -ForegroundColor Yellow
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

# --- Display Connection String ---
Write-Host "Database setup is complete!" -ForegroundColor Green
Write-Host "---"
Write-Host "Connection String for appsettings.json:"
Write-Host "Host=localhost;Port=$dbPort;Database=$dbName;Username=$dbUser;Password=$dbPassword" -ForegroundColor Cyan
Write-Host "---"
Write-Host "Database Tables Created:"
Write-Host "- MarketData (with unique index on Symbol+Timestamp)"
Write-Host "- TradeRecords (with index on ExecutedAt)"
Write-Host "- ModelPerformances"
Write-Host "- PortfolioSnapshots (with index on Timestamp)"
Write-Host "---"
Write-Host "IMPORTANT: Remember to replace 'your_strong_password' with the password you set in this script." -ForegroundColor Yellow
