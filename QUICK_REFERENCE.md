# Chess Matchmaking Backend - Quick Reference

## 🚀 Setup Commands (Copy & Paste)

### PostgreSQL Setup
```sql
-- Run in psql
CREATE DATABASE chess_app;
\c chess_app
CREATE EXTENSION postgis;
CREATE EXTENSION uuid-ossp;

-- Verify
SELECT PostGIS_Version();
```

### .NET Setup
```bash
# Create project
dotnet new webapi -n ChessApp.Backend
cd ChessApp.Backend

# Restore packages
dotnet restore

# Create & apply migrations
dotnet ef migrations add InitialCreate
dotnet ef database update

# Run server
dotnet run

# Server runs at: https://localhost:5001
# Swagger UI: https://localhost:5001/swagger
```

---

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────┐
│         Mobile App (React Native)           │
│  (Login, Map, Proposals, Match Recording)   │
└──────────────────┬──────────────────────────┘
                   │ HTTP/WebSocket
                   ↓
┌─────────────────────────────────────────────┐
│      ASP.NET Core 8 API (Program.cs)        │
│  ├── Auth endpoints (register, login)       │
│  ├── Profile endpoints (get, update)        │
│  ├── Availability endpoints (set, nearby)   │
│  └── SignalR Hub (real-time updates)        │
└──────────────────┬──────────────────────────┘
                   │
        ┌──────────┼──────────┐
        ↓          ↓          ↓
    Services   Services   Services
    ├─Auth     ├─User     ├─Proposal
    ├─Block    ├─Match    └─...
        ↓          ↓          ↓
    └──────────────┬──────────┘
                   ↓
        ┌──────────────────────┐
        │ Entity Framework     │
        │  DbContext & Models  │
        └──────────┬───────────┘
                   ↓
        ┌──────────────────────┐
        │   PostgreSQL + GIS   │
        │  (Location indexes)  │
        └──────────────────────┘
```

---

## 📂 File Organization

```
Models.cs                    ← Domain models (User, Proposal, Match)
ApplicationDbContext.cs      ← EF Core DbContext config
DTOs.cs                      ← Request/response models
Services/
  ├─ AuthService.cs          ← JWT, password hashing
  ├─ UserService.cs          ← Profiles, locations
  ├─ ProposalService.cs      ← Proposals, matches
  ├─ BlockService.cs         ← Blocking logic
  └─ NotificationHub.cs      ← WebSocket (SignalR)
Controllers.cs               ← API endpoints
Program.cs                   ← Startup config
appsettings.json             ← Settings
ChessApp.Backend.csproj      ← Dependencies
```

---

## 🔌 API Endpoint Map

### User Authentication
```
POST   /api/v1/auth/register       → Create account
POST   /api/v1/auth/login          → Get JWT token
POST   /api/v1/auth/refresh        → Get new JWT
POST   /api/v1/auth/logout         → Revoke token
```

### Profile Management
```
GET    /api/v1/profile/me          → Get your profile
GET    /api/v1/profile/{id}        → Get other's profile
PUT    /api/v1/profile/me          → Update profile
POST   /api/v1/profile/me/photo    → Upload photo
```

### Location & Discovery
```
POST   /api/v1/availability/set    → Turn on + location
GET    /api/v1/availability/nearby → Find players
```

### Proposals (Need Controller)
```
POST   /api/v1/proposals           → Propose game
GET    /api/v1/proposals/incoming  → Incoming proposals
POST   /api/v1/proposals/{id}/accept
POST   /api/v1/proposals/{id}/reject
```

### Matches (Need Controller)
```
POST   /api/v1/matches             → Record result
GET    /api/v1/matches/history     → Your games
```

### Blocks (Need Controller)
```
POST   /api/v1/blocks              → Block user
GET    /api/v1/blocks              → Your blocks
DELETE /api/v1/blocks/{id}         → Unblock
```

---

## 🔐 JWT Token Usage

### Login
```bash
curl -X POST https://localhost:5001/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"pass123"}'

# Response:
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "eyJhbGc...",
  "expiresIn": 3600
}
```

### Use Token in Requests
```bash
curl -X GET https://localhost:5001/api/v1/profile/me \
  -H "Authorization: Bearer eyJhbGc..."
```

### Refresh Token
```bash
curl -X POST https://localhost:5001/api/v1/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"eyJhbGc..."}'
```

---

## 🗺️ Geolocation Query

How nearby users are found (automatic, PostGIS):

```csharp
// User sets availability:
POST /api/v1/availability/set
{
  "isAvailable": true,
  "latitude": 30.0444,
  "longitude": 31.2357,
  "hasBoard": true,
  "expiresInHours": 4
}

// Backend stores as Point (lon, lat) in PostGIS
Point: (31.2357, 30.0444) [SRID 4326]

// To find nearby players within 10km:
GET /api/v1/availability/nearby?radiusKm=10

// Internally:
SELECT * FROM users
WHERE ST_DWithin(
  location,
  current_user_location,
  10000 -- meters
)
ORDER BY ST_Distance(...)
```

---

## 🔄 WebSocket (Real-time)

### Connect from Mobile
```typescript
import * as SignalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl('wss://localhost:5001/hub/notifications?access_token=' + token)
  .withAutomaticReconnect([0, 2000, 10000, 30000]) // Retry delays
  .build();

// Listen for events
connection.on('ProposalReceived', (payload) => {
  console.log('New proposal from:', payload.proposerUsername);
});

connection.on('UserAvailabilityChanged', (payload) => {
  console.log('User now available:', payload.userId);
  updateMapMarker(payload);
});

connection.on('UserCameOnline', (payload) => {
  console.log('User online:', payload.username);
});

// Start connection
await connection.start();
```

### Server Sends Events
```csharp
// Backend broadcasts to all clients
await _hubContext.Clients.All.SendAsync("UserAvailabilityChanged", payload);

// Or to specific user
await NotificationHub.SendProposalNotification(_hubContext, userId, proposal);
```

---

## 💾 Database Key Tables

### users
- `id` (UUID) - Primary key
- `email` (TEXT) - Unique
- `username` (TEXT) - Unique
- `password_hash` (TEXT)
- `location` (geography POINT, 4326) - PostGIS spatial type
- `is_available` (BOOL)
- `has_board` (BOOL)
- Chess credentials (fide_rating, chesscom_username, etc.)

### proposals
- `id` (UUID)
- `proposer_id` (UUID) - FK to users
- `receiver_id` (UUID) - FK to users
- `status` (ENUM: pending, accepted, rejected, expired)
- `message` (TEXT)
- `expires_at` (TIMESTAMP)

### matches
- `id` (UUID)
- `player1_id`, `player2_id` (UUID) - FK to users
- `played_at` (TIMESTAMP)
- `outcome` (ENUM: not_played, player1_won, player2_won, draw)
- `pgn` (TEXT) - Chess notation

### user_stats
- `user_id` (UUID) - FK
- `total_matches_played` (INT)
- `total_wins`, `total_losses`, `total_draws` (INT)
- `win_rate` (calculated)

---

## 🛠️ Common Tasks

### Add a New Field to User
```csharp
// 1. Update Models.cs
public class User {
    public string? NewField { get; set; }
}

// 2. Create migration
dotnet ef migrations add AddNewFieldToUser

// 3. Apply
dotnet ef database update
```

### Add a New Service
```csharp
// 1. Create interface and class in Services folder
public interface INewService { }
public class NewService : INewService { }

// 2. Register in Program.cs
builder.Services.AddScoped<INewService, NewService>();

// 3. Inject in controller
public MyController(INewService newService) { }
```

### Add New Endpoint
```csharp
[HttpPost("path")]
[Authorize]
public async Task<IActionResult> MyEndpoint([FromBody] MyRequest request)
{
    try
    {
        var result = await _service.DoSomethingAsync(request);
        return Ok(result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error");
        return StatusCode(500, new ErrorResponse { ... });
    }
}
```

---

## 🧪 Test Checklist

Before deploying, verify:

- [ ] Can register new user
- [ ] Can login with correct password
- [ ] JWT token works in Authorization header
- [ ] Token refresh works
- [ ] Can upload profile photo
- [ ] Can set availability with location
- [ ] Can find nearby users (test with 2+ users)
- [ ] Can create proposal
- [ ] Can accept/reject proposal
- [ ] Can record match result
- [ ] Stats update correctly
- [ ] WebSocket connection stays alive
- [ ] Real-time notifications work
- [ ] Can block/unblock users

---

## 🐛 Common Errors

### "Npgsql.NpgsqlException: connection refused"
→ PostgreSQL not running. Start it or check connection string.

### "DbUpdateException: No database provider"
→ Run `dotnet ef database update` to apply migrations.

### "JWT: Unable to read/validate"
→ Check SecretKey in appsettings.json (min 32 chars for HS256).

### "Location is null" in geospatial query
→ User must have called `/availability/set` first.

### "WebSocket connection failed"
→ Check token is passed: `?access_token=<jwt>` in connection URL.

---

## 📊 Performance Tips

1. **Spatial Indexes** - Already set up on location column
2. **Composite Indexes** - Set up on (status, created_at) for queries
3. **Pagination** - Add skip/take to large result sets
4. **Caching** - Consider Redis for nearby users (high traffic)
5. **Connection Pooling** - EF Core handles automatically

---

## 🚀 Deployment Checklist

- [ ] Change connection strings in appsettings.Production.json
- [ ] Update JWT secret (long random string)
- [ ] Configure CORS for actual domain (not localhost)
- [ ] Enable HTTPS (done by default in .NET)
- [ ] Set up Azure Storage for photos
- [ ] Configure database backups
- [ ] Set up monitoring/logging
- [ ] Run migrations on production database
- [ ] Test all endpoints in production
- [ ] Set up CI/CD pipeline

---

## 📚 Key Classes to Know

```csharp
// Authentication
AuthService          → JWT generation, password hashing, login/register
RefreshToken         → Stored refresh tokens

// User Management
UserService          → Profile, location, nearby searches
User model           → Full user data
UserStats            → Player statistics

// Matching
ProposalService      → Match proposals
MatchService         → Record results, update stats
Proposal, Match      → Models

// Real-time
NotificationHub      → SignalR WebSocket hub

// Data Access
ApplicationDbContext → EF Core configuration
```

---

## 💡 Design Patterns Used

✅ **Service Pattern** - Business logic in services
✅ **Repository Pattern** - EF Core as repository
✅ **DTO Pattern** - Separate API models from domain
✅ **Dependency Injection** - Built into ASP.NET Core
✅ **Async/Await** - Fully asynchronous throughout
✅ **Exception Handling** - Try-catch with logging

---

**Last Updated:** September 07, 2026

Use this as your day-to-day reference while building! 🚀
