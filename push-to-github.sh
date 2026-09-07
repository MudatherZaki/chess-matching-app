#!/bin/bash

# Chess Matchmaking App - Push to GitHub
# Run this from your project directory containing all the chess app files

set -e

echo "════════════════════════════════════════════════════"
echo "  Chess Matchmaking App - Push to GitHub"
echo "════════════════════════════════════════════════════"
echo ""

# Get input from user
read -p "Enter your GitHub username (e.g., MudatherZaki): " GITHUB_USER
read -p "Enter your GitHub personal access token: " GITHUB_TOKEN
read -p "Enter repository name (e.g., chess-app-backend): " REPO_NAME

if [ -z "$GITHUB_USER" ] || [ -z "$GITHUB_TOKEN" ] || [ -z "$REPO_NAME" ]; then
    echo "❌ Error: All fields are required"
    exit 1
fi

REPO_URL="https://$GITHUB_USER:$GITHUB_TOKEN@github.com/$GITHUB_USER/$REPO_NAME.git"

echo ""
echo "Repository: https://github.com/$GITHUB_USER/$REPO_NAME"
echo ""

# Initialize git
if [ ! -d ".git" ]; then
    echo "🔧 Initializing git repository..."
    git init
else
    echo "✓ Git repository already initialized"
fi

# Configure git
echo "🔧 Configuring git..."
git config user.email "mudather@example.com" 2>/dev/null || true
git config user.name "Mudather Zaki" 2>/dev/null || true

# Create .gitignore
if [ ! -f ".gitignore" ]; then
    echo "📝 Creating .gitignore..."
    cat > .gitignore << 'EOF'
# .NET
bin/
obj/
*.dll
*.exe
*.pdb
.vs/
.vscode/
*.user
*.suo

# Environment
appsettings.Development.json
.env
.env.local

# Logs
logs/
*.log

# OS
.DS_Store
Thumbs.db

# IDE
.idea/
*.swp
EOF
fi

# Stage files
echo "📦 Staging files..."
git add .

# Show files to be committed
echo ""
echo "📋 Files to be committed:"
git diff --cached --name-only | head -10
echo ""

# Create commit
echo "💾 Creating commit..."
git commit -m "Initial commit: Chess Matchmaking Backend

- .NET 8 ASP.NET Core API
- PostgreSQL with PostGIS geospatial support
- User authentication with JWT tokens
- Geolocation-based player matching
- Real-time WebSocket notifications (SignalR)
- Match proposal and recording system
- Complete API specification and database schema
- Comprehensive documentation" || echo "Nothing to commit"

echo ""
echo "🔗 Adding remote repository..."
git remote remove origin 2>/dev/null || true
git remote add origin "$REPO_URL"

echo ""
echo "🚀 Pushing to GitHub..."

# Rename branch to main and push
git branch -M main 2>/dev/null || true
git push -u origin main

echo ""
echo "════════════════════════════════════════════════════"
echo "  ✅ Successfully pushed to GitHub!"
echo "════════════════════════════════════════════════════"
echo ""
echo "Repository: https://github.com/$GITHUB_USER/$REPO_NAME"
echo ""
