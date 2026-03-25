# How to Run the Project from Terminal (Cursor)

## Prerequisites

Make sure you have:
- .NET 8 SDK installed (`dotnet --version` should show 8.x)
- PostgreSQL running on localhost:5432
- Database `erp_dev` created

## Running the Backend API

### Step 1: Navigate to API Directory
```powershell
cd src/server/Api
```

### Step 2: Build the Project
```powershell
dotnet build
```

### Step 3: Run the API (HTTP - Port 5227)
```powershell
dotnet run --launch-profile http
```

Or run with hot reload (auto-restarts on code changes):
```powershell
dotnet watch run --launch-profile http
```

### Alternative: Run HTTPS (Port 7237)
```powershell
dotnet run --launch-profile https
```

### Step 4: Verify It's Running
- Open browser: `http://localhost:5227/swagger`
- You should see Swagger UI with all API endpoints

### Step 5: Stop the Server
Press `Ctrl+C` in the terminal

## Testing Email Configuration

### Option 1: Using Swagger UI
1. Open `http://localhost:5227/swagger`
2. Find `GET /api/test/email-config` - Click "Try it out" → "Execute"
3. Find `POST /api/test/email` - Click "Try it out", enter email, click "Execute"

### Option 2: Using PowerShell/curl

**Check Email Configuration:**
```powershell
Invoke-RestMethod -Uri "http://localhost:5227/api/test/email-config" -Method Get
```

**Send Test Email:**
```powershell
$body = @{
    toEmail = "your-email@gmail.com"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5227/api/test/email" -Method Post -Body $body -ContentType "application/json"
```

### Option 3: Using curl (if installed)
```bash
# Check config
curl http://localhost:5227/api/test/email-config

# Send test email
curl -X POST http://localhost:5227/api/test/email -H "Content-Type: application/json" -d "{\"toEmail\":\"your-email@gmail.com\"}"
```

## Running Frontend (if needed)

**Run from the repo root** (folder that contains `src/client`).

### In a New Terminal Window:
```powershell
cd src/client
pnpm nx serve applicant-portal
```

Frontend will be available at: `http://localhost:4200`

**Offline admission (office):** sign in at `/AdminLogin`, then open **Online Admission → Offline admission** (`http://localhost:4200/admin/admissions/offline`).  
(The old path `/offline-admission` only exists if you use a separate `apps/` tree at repo root—not the main `src/client` app.)

## Quick Commands Reference

```powershell
# Build backend
cd src/server/Api
dotnet build

# Run backend (HTTP)
dotnet run --launch-profile http

# Run backend with hot reload
dotnet watch run --launch-profile http

# Run frontend
cd src/client



# Check if backend is running
netstat -ano | findstr :5227
```

## Troubleshooting

### Port Already in Use
```powershell
# Find process using port 5227
netstat -ano | findstr :5227

# Kill the process (replace PID with actual process ID)
taskkill /PID <PID> /F
```

### Build Errors
```powershell
# Clean and rebuild
dotnet clean
dotnet build
```

### Database Connection Issues
- Make sure PostgreSQL is running
- Check connection string in `src/server/Api/appsettings.json`
- Verify database `erp_dev` exists



















