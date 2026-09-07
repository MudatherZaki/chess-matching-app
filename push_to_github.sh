#!/bin/bash

# Chess Matchmaking App - Push to GitHub Script
# This script sets up git and pushes all code to your GitHub repository

set -e  # Exit on error

echo "════════════════════════════════════════════════════"
echo "  Chess Matchmaking App - GitHub Push Script"
echo "════════════════════════════════════════════════════"
echo ""

# Get repository name from user
read -p "Enter your repository name (e.g., chess-app-backend): " REPO_NAME
read -p "Enter your GitHub username (e.g., MudatherZaki): " GITHUB_USER
read -p "Enter your GitHub personal access token (or press Enter for SSH): " GITHUB_TOKEN

echo ""
echo "Setting up repository: $GITHUB_USER/$REPO_NAME"
echo ""

# Determine if using HTTPS or SSH
if [ -z "$GITHUB_TOKEN" ]; then
    REPO_URL="git@github.com:$GITHUB_USER/$REPO_NAME.git"
    echo "Using SSH authentication"
else
    REPO_URL="https://$GITHUB_USER:$GITHUB_TOKEN@github.com/$GITHUB_USER/$REPO_NAME.git"
    echo "Using HTTPS authentication with personal access token"
fi

echo ""

# Navigate to project directory
PROJECT_DIR="/home/claude"
cd "$PROJECT_DIR"

# Initialize git if not already initialized
if [ ! -d ".git" ]; then
    echo "🔧 Initializing git repository..."
    git init
else
    echo "✓ Git repository already initialized"
fi

# Configure git user (local scope)
echo "🔧 Configuring git user..."
git config user.email "your-email@example.com"
git config user.name "Mudather Zaki"

# Create .gitignore if it doesn't exist
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

# Rider
.idea/
*.sln.iml

# Environment
appsettings.Development.json
.env
.env.local

# OS
.DS_Store
Thumbs.db

# Database
*.db
*.sqlite

# Logs
logs/
*.log

# Node (if using frontend tooling)
node_modules/
npm-debug.log
EOF
    echo "✓ .gitignore created"
fi

# Create .gitattributes for line endings
if [ ! -f ".gitattributes" ]; then
    echo "📝 Creating .gitattributes..."
    cat > .gitattributes << 'EOF'
# Auto detect text files and normalize line endings
* text=auto

# C# files
*.cs text eol=crlf
*.csproj text eol=crlf
*.sln text eol=crlf

# SQL files
*.sql text eol=crlf

# Json
*.json text eol=crlf

# Markdown
*.md text eol=lf
EOF
    echo "✓ .gitattributes created"
fi

# Stage all files
echo ""
echo "📦 Staging files..."
git add .

# Show what will be committed
echo ""
echo "📋 Files to be committed:"
git diff --cached --name-only | head -20
echo ""

# Create initial commit
echo ""
echo "💾 Creating initial commit..."
git commit -m "Initial commit: Chess matchmaking app backend

- Database schema with PostGIS geospatial support
- Entity Framework Core models and DbContext
- Authentication service with JWT
- User profile and location management
- Match proposal and recording system
- Real-time WebSocket notifications (SignalR)
- Complete API specification
- Comprehensive documentation"

# Add remote and push
echo ""
echo "🔗 Adding remote repository..."

# Remove old remote if it exists
git remote remove origin 2>/dev/null || true

git remote add origin "$REPO_URL"

echo ""
echo "🚀 Pushing to GitHub..."

# Push to main branch
if git rev-parse --verify main >/dev/null 2>&1; then
    echo "Found 'main' branch, pushing to main..."
    git push -u origin main
else
    echo "No 'main' branch found, renaming to main..."
    git branch -M main
    git push -u origin main
fi

echo ""
echo "════════════════════════════════════════════════════"
echo "  ✅ Successfully pushed to GitHub!"
echo "════════════════════════════════════════════════════"
echo ""
echo "Repository URL: https://github.com/$GITHUB_USER/$REPO_NAME"
echo ""
echo "Next steps:"
echo "1. Create a README in GitHub with setup instructions"
echo "2. Invite team members as collaborators"
echo "3. Set up CI/CD pipeline (GitHub Actions)"
echo "4. Configure branch protection rules"
echo ""
