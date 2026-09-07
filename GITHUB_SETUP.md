# Push Chess Matchmaking App to GitHub

## 🚀 Quick Start (Recommended)

### Option 1: Using the Automated Script (Fastest - 2 minutes)

```bash
# 1. Make script executable
chmod +x /home/claude/push_to_github.sh

# 2. Run the script
bash /home/claude/push_to_github.sh

# 3. Enter your GitHub username and personal access token when prompted
```

That's it! The script handles everything.

---

## 📋 Manual Setup (If Script Doesn't Work)

### Step 1: Create Personal Access Token on GitHub

1. Go to https://github.com/settings/tokens
2. Click "Generate new token" → "Generate new token (classic)"
3. Set these scopes:
   - ✅ `repo` (Full control of private repositories)
   - ✅ `workflow` (Actions)
4. Click "Generate token"
5. **Copy the token immediately** (you won't see it again)

### Step 2: Create New Repository on GitHub

1. Go to https://github.com/new
2. **Repository name:** `chess-app-backend` (or your preference)
3. **Description:** "Geolocation-based OTB chess matchmaking backend"
4. **Visibility:** Public (or Private if preferred)
5. **Initialize repository:** Leave unchecked (we'll push existing code)
6. Click "Create repository"

### Step 3: Push Code from Command Line

```bash
# Navigate to your project directory
cd /path/to/ChessApp.Backend

# Initialize git (if not already done)
git init

# Configure git user
git config user.email "your-email@example.com"
git config user.name "Your Name"

# Create .gitignore
cat > .gitignore << 'EOF'
bin/
obj/
*.dll
*.exe
*.pdb
.vs/
.vscode/
*.user
appsettings.Development.json
.env
.env.local
node_modules/
npm-debug.log
logs/
*.log
*.db
*.sqlite
.DS_Store
Thumbs.db
EOF

# Stage all files
git add .

# Create initial commit
git commit -m "Initial commit: Chess matchmaking backend with .NET 8, PostgreSQL, and PostGIS"

# Add remote repository
git remote add origin https://MudatherZaki:YOUR_TOKEN@github.com/MudatherZaki/chess-app-backend.git

# Push to GitHub
git branch -M main
git push -u origin main
```

**Replace:**
- `YOUR_TOKEN` with your actual GitHub token
- `chess-app-backend` with your repository name
- `MudatherZaki` with your GitHub username

---

## 🔑 SSH Setup (Alternative - More Secure for Future Pushes)

### Step 1: Generate SSH Key

```bash
# Generate SSH key (press Enter 3 times to use defaults)
ssh-keygen -t ed25519 -C "your-email@example.com"

# (Or if ed25519 not available):
# ssh-keygen -t rsa -b 4096 -C "your-email@example.com"

# Copy the public key
cat ~/.ssh/id_ed25519.pub
```

### Step 2: Add SSH Key to GitHub

1. Go to https://github.com/settings/ssh/new
2. Title: "My Computer"
3. Paste the public key you copied
4. Click "Add SSH key"

### Step 3: Push Using SSH

```bash
cd /path/to/ChessApp.Backend

# Configure git
git config user.email "your-email@example.com"
git config user.name "Your Name"

# If not already initialized
git init
git add .
git commit -m "Initial commit: Chess matchmaking backend"

# Add remote using SSH
git remote add origin git@github.com:MudatherZaki/chess-app-backend.git

# Push
git branch -M main
git push -u origin main
```

---

## 📂 What Gets Pushed

The repository will contain:

```
chess-app-backend/
├── Models.cs                 ← Domain models
├── ApplicationDbContext.cs   ← Database context
├── DTOs.cs                   ← API models
├── Services/
│   ├── AuthService.cs
│   ├── UserService.cs
│   ├── ProposalService.cs
│   └── NotificationHub.cs
├── Controllers.cs            ← API endpoints
├── Program.cs               ← Startup
├── appsettings.json         ← Configuration
├── ChessApp.Backend.csproj  ← Project file
├── chess_app_schema.sql     ← Database schema
├── chess_app_api_spec.md    ← API documentation
├── README.md
├── QUICK_REFERENCE.md
├── DELIVERABLES.md
└── .gitignore
```

---

## ✅ Verify Push Was Successful

```bash
# Should show your repository
git remote -v

# Output should be:
# origin  https://github.com/MudatherZaki/chess-app-backend.git (fetch)
# origin  https://github.com/MudatherZaki/chess-app-backend.git (push)

# Check current branch
git branch -a

# Should show:
# * main
#   remotes/origin/main
```

---

## 🔧 Common Issues & Solutions

### "fatal: remote origin already exists"
```bash
# Remove the old remote first
git remote remove origin
git remote add origin https://...
```

### "Permission denied (publickey)"
→ SSH key not set up correctly. Use HTTPS method instead:
```bash
git remote set-url origin https://username:token@github.com/username/repo.git
```

### "fatal: Not a git repository"
→ Run in the correct directory:
```bash
cd /path/to/ChessApp.Backend
git init
```

### "fatal: bad credentials"
→ Token is invalid or expired. Generate a new one at https://github.com/settings/tokens

---

## 📝 Create Repository README.md on GitHub

After pushing, add this to your GitHub repo README (GitHub will show edit option):

```markdown
# Chess Matchmaking App Backend

Geolocation-based matchmaking service for over-the-board (OTB) chess players.

## Tech Stack

- **.NET 8** - ASP.NET Core API
- **PostgreSQL** - Database with PostGIS
- **SignalR** - Real-time WebSocket notifications
- **Entity Framework Core 8** - ORM
- **JWT** - Authentication

## Quick Start

See [README.md](./README.md) for setup instructions.

## Features

✅ User registration & authentication
✅ Profile management with photo uploads
✅ Geolocation-based player search
✅ Match proposals & acceptance
✅ Real-time notifications
✅ User statistics tracking
✅ User blocking system

## API Documentation

See [chess_app_api_spec.md](./chess_app_api_spec.md) for full API reference.

## Database Schema

See [chess_app_schema.sql](./chess_app_schema.sql) for database setup.

## Project Structure

- `Models.cs` - Domain models
- `ApplicationDbContext.cs` - EF Core configuration
- `DTOs.cs` - API data models
- `Services/` - Business logic
- `Controllers.cs` - API endpoints
- `NotificationHub.cs` - Real-time communication

## Development

```bash
dotnet restore
dotnet ef database update
dotnet run
```

Server runs at: `https://localhost:5001`
Swagger UI: `https://localhost:5001/swagger`

## License

MIT

## Author

Mudather Zaki
```

---

## 🚀 After Pushing

### 1. Set Up GitHub Actions (CI/CD)

Create `.github/workflows/dotnet.yml`:

```yaml
name: .NET Build & Test

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  build:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        dotnet-version: [ '8.0.x' ]

    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: ${{ matrix.dotnet-version }}
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Test
      run: dotnet test --no-build --verbosity normal
```

### 2. Add Collaborators

1. Go to your repository
2. Settings → Collaborators
3. Add team members

### 3. Set Up Branch Protection

1. Settings → Branches
2. Add rule for `main` branch
3. Require pull request reviews before merging
4. Require status checks to pass

---

## 📱 For Your Team

Share this with team members:

```markdown
# How to Clone & Setup

## Clone the repository
git clone https://github.com/MudatherZaki/chess-app-backend.git
cd chess-app-backend

## Setup
dotnet restore
dotnet ef database update
dotnet run

## Create a branch for your work
git checkout -b feature/your-feature-name

## Push and create a pull request
git push origin feature/your-feature-name
```

---

## 🎯 Next: GitHub Collaboration Workflow

```
1. Pull latest changes
   git pull origin main

2. Create feature branch
   git checkout -b feature/name

3. Make changes & commit
   git add .
   git commit -m "Add feature description"

4. Push to GitHub
   git push origin feature/name

5. Create Pull Request on GitHub
   (GitHub will show a button to create PR)

6. Get reviewed & merge
```

---

## 💡 Tips

1. **Commit Messages** - Be descriptive:
   ```bash
   git commit -m "Add user blocking functionality"
   git commit -m "Fix geolocation query performance"
   ```

2. **Keep Secrets Safe** - Never commit:
   - Database passwords
   - API keys
   - JWT secrets
   - Use `.gitignore` and environment variables

3. **Pull Before Push**:
   ```bash
   git pull origin main
   git push origin main
   ```

4. **Check Status**:
   ```bash
   git status
   git log --oneline  # See commit history
   ```

---

## 📞 Troubleshooting

If anything goes wrong:

```bash
# See what's going on
git status
git log --oneline

# Check remote
git remote -v

# Undo last commit (if not pushed)
git reset --soft HEAD~1

# Force push (careful!)
git push -f origin main
```

---

**You're all set!** Your code is now on GitHub and ready for collaboration. 🎉
