# Chess Matchmaking App - Backend (.NET)

## 📋 Project Overview

A geolocation-based matchmaking service that connects over-the-board (OTB) chess players. This backend provides:

- **User Management** - Registration, authentication (JWT), profiles
- **Geolocation** - Find nearby players using PostGIS
- **Match Proposals** - Users propose games and accept/reject
- **Match Recording** - Track completed games and statistics
- **Real-time Notifications** - SignalR WebSocket connections
- **Azure Integration** - Blob storage for profile photos

---

## 🏗️ Project Structure

```
ChessApp.Backend/
├── Models.cs                 # Domain models (User, Proposal, Match, etc.)
├── ApplicationDbContext.cs   # EF Core DbContext with PostGIS
├── DTOs.cs                   # Data Transfer Objects for API
├── Services/
│   ├── AuthService.cs        # JWT, password hashing, login/register
│   ├── UserService.cs        # Profile, availability, geolocation
│   ├── ProposalService.cs    # Proposals, matches, blocking
│   └── NotificationHub.cs    # SignalR real-time updates
├── Controllers.cs            # API endpoints (Auth, Profile, Availability)
├── Program.cs               # Startup configuration
├── appsettings.json         # Configuration
└── ChessApp.Backend.csproj  # Project file & dependencies
```

---

## 🔧 Prerequisites

1. **.NET 8 SDK** - Download from https://dotnet.microsoft.com/download
2. **PostgreSQL 14+** - https://www.postgresql.org/download/
3. **Azure Storage Account** (optional, for production)

---

## 📦 Installation & Setup

### 1. Create PostgreSQL Database

```bash
# Connect to PostgreSQL
psql -U postgres

# Create database
CREATE DATABASE chess_app;

# Enable PostGIS extension
\c chess_app
CREATE EXTENSION postgis;
CREATE EXTENSION uuid-ossp;

# Verify
SELECT PostGIS_Version();
```

### 2. Clone and Configure

```bash
# Copy files into .NET project
mkdir ChessApp.Backend
cd ChessApp.Backend

# Copy all the generated files into this directory
# (Models.cs, ApplicationDbContext.cs, DTOs.cs, Services, Controllers, etc.)
```

### 3. Update Connection Strings

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=chess_app;Username=postgres;Password=YOUR_PASSWORD;",
    "AzureBlobStorage": "DefaultEndpointsProtocol=https;AccountName=YOUR_ACCOUNT;AccountKey=YOUR_KEY;EndpointSuffix=core.windows.net"
  },
  "Jwt": {
    "SecretKey": "your-secret-key-at-least-32-characters-long",
    "Issuer": "chess-app-api",
    "Audience": "chess-app-mobile"
  },
  "Azure": {
    "StorageAccount": "yourstorageaccount",
    "StorageKey": "yourkey",
    "BlobContainerName": "chess-app-photos"
  }
}
```

### 4. Install Dependencies

```bash
dotnet restore
```

### 5. Create and Run Migrations

```bash
# Create initial migration
dotnet ef migrations add InitialCreate

# Apply migration to database
dotnet ef database update

# Verify tables created
# Connect to chess_app database and run: \dt
```

### 6. Run the Application

```bash
dotnet run
```

The API will be available at: `https://localhost:5001`

Swagger UI: `https://localhost:5001/swagger`

---

## 🚀 Core Features Implemented

### Authentication (`AuthService`)
- ✅ Register new users
- ✅ Login with JWT tokens
- ✅ Refresh tokens
- ✅ Password hashing (SHA-512)
- ✅ Token revocation

### User Management (`UserService`)
- ✅ Get/update profiles
- ✅ Upload profile photos (Azure Blob Storage)
- ✅ Set availability with geolocation
- ✅ Find nearby users (PostGIS spatial queries)
- ✅ Auto-expire availability

### Proposals & Matches (`ProposalService`, `MatchService`)
- ✅ Create match proposals
- ✅ Get incoming/outgoing proposals
- ✅ Accept/reject proposals
- ✅ Record match results
- ✅ Update player statistics
- ✅ Match history

### Blocking (`BlockService`)
- ✅ Block/unblock users
- ✅ Get block list
- ✅ Check if blocked before proposing

### Real-time (`NotificationHub`)
- ✅ WebSocket connections
- ✅ Proposal notifications
- ✅ Availability updates
- ✅ Online status broadcasting

---

## 📡 API Endpoints

### Authentication
```
POST   /api/v1/auth/register      - Register new user
POST   /api/v1/auth/login         - Login
POST   /api/v1/auth/refresh       - Refresh token
POST   /api/v1/auth/logout        - Logout
```

### Profile
```
GET    /api/v1/profile/me         - Get current user profile
GET    /api/v1/profile/{userId}   - Get public profile
PUT    /api/v1/profile/me         - Update profile
POST   /api/v1/profile/me/photo   - Upload photo
```

### Availability
```
POST   /api/v1/availability/set   - Set availability & location
GET    /api/v1/availability/nearby - Get nearby players
```

### Proposals (to be implemented in controllers)
```
POST   /api/v1/proposals          - Create proposal
GET    /api/v1/proposals/incoming - Get incoming proposals
GET    /api/v1/proposals/outgoing - Get outgoing proposals
POST   /api/v1/proposals/{id}/accept - Accept proposal
POST   /api/v1/proposals/{id}/reject - Reject proposal
```

### Matches (to be implemented)
```
POST   /api/v1/matches           - Record match result
GET    /api/v1/matches/history   - Get match history
```

### Blocks (to be implemented)
```
POST   /api/v1/blocks            - Block user
GET    /api/v1/blocks            - Get block list
DELETE /api/v1/blocks/{userId}   - Unblock user
```

---

## 📊 Database Schema

### Tables
- **users** - User accounts, profiles, chess credentials
- **proposals** - Match requests between users
- **matches** - Completed games
- **user_stats** - Player statistics
- **blocks** - User blocks
- **rating_snapshots** - Historical rating tracking
- **refresh_tokens** - JWT refresh tokens

### Key Indexes
- Spatial index on user location for fast geo-queries
- Composite indexes on proposals for quick filtering
- Unique constraints on email, username, blocks

---

## 🔐 Security Features

1. **JWT Authentication** - Signed tokens with expiration
2. **Password Hashing** - SHA-512 with no salt (upgrade to bcrypt in production)
3. **HTTPS Only** - Redirect HTTP to HTTPS
4. **CORS** - Configurable for mobile origins
5. **Rate Limiting** - (To be implemented)
6. **Input Validation** - FluentValidation (template provided)

---

## 📱 Mobile Integration (React Native/Flutter)

### Connection Example

```typescript
// React Native example
import * as SignalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl('https://api.example.com/hub/notifications?access_token=' + token)
  .withAutomaticReconnect()
  .build();

connection.on('ProposalReceived', (payload) => {
  console.log('New proposal:', payload);
});

connection.on('UserAvailabilityChanged', (payload) => {
  updateMapMarker(payload);
});

await connection.start();
```

---

## 🚧 Next Steps

### Phase 2 (Backend)
1. **Implement Proposal/Match Controllers** - Wire up remaining endpoints
2. **Add Validators** - FluentValidation for DTOs
3. **Implement Rate Limiting** - Prevent abuse
4. **Add Caching** - Redis for nearby users queries
5. **Better Password Hashing** - Switch to bcrypt
6. **Logging** - Structured logging with Serilog

### Phase 3 (Frontend - React Native)
1. **Authentication Screens** - Register/Login UI
2. **Map View** - Show nearby players
3. **Profile Management** - Photo upload, chess credentials
4. **Proposal UI** - Send/respond to match requests
5. **Real-time Updates** - SignalR integration
6. **Match Recording** - Post-game form

### Phase 4 (Polish)
1. **Testing** - Unit tests, integration tests
2. **Deployment** - Azure App Service, CI/CD
3. **Monitoring** - Application Insights
4. **Performance** - Load testing, optimization

---

## 🐛 Troubleshooting

### Connection String Error
```
Ensure PostgreSQL is running and connection string matches
psql -U postgres -d chess_app
```

### Migration Fails
```bash
# Reset and re-migrate (development only!)
dotnet ef database drop
dotnet ef database update
```

### JWT Token Invalid
```
Check that SecretKey in appsettings.json is at least 32 characters
Verify token is in Authorization header: "Bearer <token>"
```

### PostGIS Not Found
```bash
# Make sure extension is created
psql -U postgres -d chess_app -c "CREATE EXTENSION postgis;"
```

---

## 📚 Additional Resources

- **EF Core Docs**: https://docs.microsoft.com/en-us/ef/core/
- **PostGIS**: https://postgis.net/documentation/
- **SignalR**: https://docs.microsoft.com/en-us/aspnet/core/signalr/
- **JWT**: https://tools.ietf.org/html/rfc7519
- **Azure Blob Storage**: https://docs.microsoft.com/en-us/azure/storage/blobs/

---

## 📝 Development Notes

### Code Style
- Follow C# naming conventions (PascalCase for classes/methods)
- Use async/await throughout
- Leverage EntityFramework LINQ
- Handle exceptions explicitly

### Database Access
- Always use dependency injection for DbContext
- Use `.Include()` to eager load related entities
- Leverage EntityFramework's spatial queries

### API Response Format
All endpoints return JSON with consistent error handling:
```json
{
  "error": "ValidationError",
  "message": "Validation failed",
  "details": [
    { "field": "email", "message": "Invalid format" }
  ]
}
```

---

## 🤝 Contributing

When adding new features:
1. Create corresponding models in `Models.cs`
2. Add DbSet to `ApplicationDbContext`
3. Create service interface and implementation
4. Add DTOs in `DTOs.cs`
5. Create controller and endpoints
6. Update API spec

Good luck building! 🚀
