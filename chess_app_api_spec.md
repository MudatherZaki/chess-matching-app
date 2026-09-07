# Chess Matchmaking App - API Specification

**Base URL:** `https://api.chessmatch.local/v1`  
**WebSocket:** `wss://api.chessmatch.local/hub/notifications`

---

## Authentication

All endpoints require Bearer token in `Authorization` header (JWT).

```
Authorization: Bearer <jwt_token>
```

### POST `/auth/register`
Register a new user.

**Request:**
```json
{
  "email": "player@example.com",
  "username": "chessmaster42",
  "password": "SecurePassword123!",
  "fullName": "John Doe"
}
```

**Response (201):**
```json
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "email": "player@example.com",
  "username": "chessmaster42",
  "accessToken": "eyJhbGc...",
  "refreshToken": "eyJhbGc...",
  "expiresIn": 3600
}
```

---

### POST `/auth/login`
Authenticate and get tokens.

**Request:**
```json
{
  "email": "player@example.com",
  "password": "SecurePassword123!"
}
```

**Response (200):**
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "eyJhbGc...",
  "expiresIn": 3600,
  "user": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "username": "chessmaster42",
    "email": "player@example.com"
  }
}
```

---

### POST `/auth/refresh`
Get new access token using refresh token.

**Request:**
```json
{
  "refreshToken": "eyJhbGc..."
}
```

**Response (200):**
```json
{
  "accessToken": "eyJhbGc...",
  "expiresIn": 3600
}
```

---

## Profile Management

### GET `/profile/me`
Get current user's profile.

**Response (200):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "email": "player@example.com",
  "username": "chessmaster42",
  "fullName": "John Doe",
  "photoUrl": "https://storage.azure.com/photos/550e8400...",
  "bio": "Love rapid chess",
  "isAvailable": true,
  "availabilityExpiresAt": "2024-01-15T18:30:00Z",
  "hasBoard": true,
  "fideId": "24500000",
  "fideRating": 2100,
  "chessCom": {
    "username": "chessmaster42",
    "url": "https://chess.com/member/chessmaster42",
    "rating": 2050
  },
  "lichess": {
    "username": "chessmaster42",
    "url": "https://lichess.org/chessmaster42",
    "rating": 2200
  },
  "stats": {
    "totalMatchesPlayed": 15,
    "totalWins": 8,
    "totalLosses": 5,
    "totalDraws": 2
  },
  "createdAt": "2024-01-01T10:00:00Z",
  "lastLogin": "2024-01-15T10:30:00Z"
}
```

---

### PUT `/profile/me`
Update current user's profile.

**Request:**
```json
{
  "fullName": "John Doe Updated",
  "bio": "Love rapid and blitz chess",
  "hasBoard": true,
  "fideId": "24500000",
  "fideRating": 2120,
  "chessComUsername": "chessmaster42",
  "lichessUsername": "chessmaster42"
}
```

**Response (200):** Updated profile object

---

### POST `/profile/me/photo`
Upload profile photo.

**Request:** Multipart form-data
```
Content-Type: multipart/form-data
Field: file (image/jpeg, image/png, max 5MB)
```

**Response (200):**
```json
{
  "photoUrl": "https://storage.azure.com/photos/550e8400-e29b-41d4-a716-446655440000.jpg"
}
```

---

## Availability & Discovery

### POST `/availability/set`
Set user availability with location.

**Request:**
```json
{
  "isAvailable": true,
  "hasBoard": true,
  "latitude": 30.0444,
  "longitude": 31.2357,
  "expiresInHours": 4
}
```

**Response (200):**
```json
{
  "isAvailable": true,
  "expiresAt": "2024-01-15T14:30:00Z",
  "location": {
    "latitude": 30.0444,
    "longitude": 31.2357
  }
}
```

---

### GET `/availability/nearby`
Get nearby available players.

**Query Params:**
- `radiusKm` (int, default: 10) - Search radius
- `hasBoard` (bool, optional) - Filter by board availability
- `limit` (int, default: 50) - Max results

**Response (200):**
```json
{
  "users": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440001",
      "username": "classicalplayer99",
      "fullName": "Ahmed Ali",
      "photoUrl": "https://storage.azure.com/photos/550e8400...",
      "fideRating": 1950,
      "chessComRating": 1980,
      "lichessRating": 2050,
      "hasBoard": true,
      "distanceKm": 2.5,
      "stats": {
        "totalMatches": 23,
        "wins": 12
      }
    }
  ],
  "count": 1
}
```

---

## Proposals / Match Requests

### POST `/proposals`
Create a new match proposal.

**Request:**
```json
{
  "receiverId": "550e8400-e29b-41d4-a716-446655440001",
  "message": "Want to play a quick rapid game?"
}
```

**Response (201):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440010",
  "proposerId": "550e8400-e29b-41d4-a716-446655440000",
  "receiverId": "550e8400-e29b-41d4-a716-446655440001",
  "status": "pending",
  "message": "Want to play a quick rapid game?",
  "expiresAt": "2024-01-16T10:30:00Z",
  "createdAt": "2024-01-15T10:30:00Z"
}
```

---

### GET `/proposals/incoming`
Get pending incoming proposals.

**Query Params:**
- `status` (enum: pending, accepted, rejected - default: pending)
- `limit` (int, default: 20)

**Response (200):**
```json
{
  "proposals": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440010",
      "proposer": {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "username": "chessmaster42",
        "fullName": "John Doe",
        "photoUrl": "https://storage.azure.com/photos/550e8400...",
        "fideRating": 2100,
        "chessComRating": 2050
      },
      "status": "pending",
      "message": "Want to play a quick rapid game?",
      "expiresAt": "2024-01-16T10:30:00Z",
      "createdAt": "2024-01-15T10:30:00Z"
    }
  ],
  "count": 1
}
```

---

### GET `/proposals/outgoing`
Get user's sent proposals.

**Query Params:**
- `status` (enum: pending, accepted, rejected)
- `limit` (int, default: 20)

**Response (200):** Similar structure to incoming

---

### POST `/proposals/{proposalId}/accept`
Accept an incoming proposal.

**Request:**
```json
{
  "meetingLocation": {
    "latitude": 30.0444,
    "longitude": 31.2357,
    "address": "Cairo Chess Club"
  }
}
```

**Response (200):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440010",
  "status": "accepted",
  "respondedAt": "2024-01-15T10:45:00Z",
  "matchId": "550e8400-e29b-41d4-a716-446655440020"
}
```

---

### POST `/proposals/{proposalId}/reject`
Reject an incoming proposal.

**Request:**
```json
{
  "reason": "Not available right now"
}
```

**Response (200):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440010",
  "status": "rejected",
  "respondedAt": "2024-01-15T10:45:00Z"
}
```

---

### DELETE `/proposals/{proposalId}`
Cancel an outgoing proposal (only by proposer before expiration).

**Response (204):** No content

---

## Matches

### GET `/matches/history`
Get user's match history.

**Query Params:**
- `limit` (int, default: 20)
- `offset` (int, default: 0)
- `sortBy` (enum: recent, oldest - default: recent)

**Response (200):**
```json
{
  "matches": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440020",
      "opponent": {
        "id": "550e8400-e29b-41d4-a716-446655440001",
        "username": "classicalplayer99",
        "fullName": "Ahmed Ali",
        "fideRating": 1950
      },
      "playedAt": "2024-01-14T18:30:00Z",
      "outcome": "player1_won",
      "playedWithBoard": true,
      "timeControl": "5+3",
      "notes": "Great game!",
      "createdAt": "2024-01-14T18:30:00Z"
    }
  ],
  "count": 15,
  "total": 15
}
```

---

### POST `/matches`
Record a completed match.

**Request:**
```json
{
  "opponentId": "550e8400-e29b-41d4-a716-446655440001",
  "playedAt": "2024-01-14T18:30:00Z",
  "outcome": "player1_won",
  "playedWithBoard": true,
  "timeControl": "5+3",
  "pgn": "[Event \"OTB Match\"] [White \"Me\"] [Black \"Ahmed\"] ...",
  "notes": "Great game!"
}
```

**Response (201):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440020",
  "matchId": "550e8400-e29b-41d4-a716-446655440020"
}
```

---

## User Blocking

### POST `/blocks`
Block a user.

**Request:**
```json
{
  "blockedUserId": "550e8400-e29b-41d4-a716-446655440001",
  "reason": "Inappropriate behavior"
}
```

**Response (201):**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440030",
  "blockedUserId": "550e8400-e29b-41d4-a716-446655440001"
}
```

---

### GET `/blocks`
Get current user's block list.

**Response (200):**
```json
{
  "blockedUsers": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440001",
      "username": "problematicuser",
      "blockedAt": "2024-01-15T10:00:00Z",
      "reason": "Inappropriate behavior"
    }
  ],
  "count": 1
}
```

---

### DELETE `/blocks/{blockedUserId}`
Unblock a user.

**Response (204):** No content

---

## WebSocket - Real-time Notifications

**Endpoint:** `wss://api.chessmatch.local/hub/notifications`

Connect with authentication header or query parameter: `?access_token=<jwt>`

### Events Received

#### `ProposalReceived`
```json
{
  "type": "ProposalReceived",
  "payload": {
    "proposalId": "550e8400-e29b-41d4-a716-446655440010",
    "proposerId": "550e8400-e29b-41d4-a716-446655440000",
    "proposerUsername": "chessmaster42",
    "proposerPhotoUrl": "https://...",
    "message": "Want to play?"
  }
}
```

#### `ProposalResponded`
```json
{
  "type": "ProposalResponded",
  "payload": {
    "proposalId": "550e8400-e29b-41d4-a716-446655440010",
    "status": "accepted",
    "responderId": "550e8400-e29b-41d4-a716-446655440001"
  }
}
```

#### `UserAvailabilityChanged`
```json
{
  "type": "UserAvailabilityChanged",
  "payload": {
    "userId": "550e8400-e29b-41d4-a716-446655440001",
    "isAvailable": true,
    "hasBoard": true,
    "location": {
      "latitude": 30.0444,
      "longitude": 31.2357
    }
  }
}
```

#### `UserCameOnline`
```json
{
  "type": "UserCameOnline",
  "payload": {
    "userId": "550e8400-e29b-41d4-a716-446655440001",
    "username": "classicalplayer99"
  }
}
```

---

## Error Responses

**400 Bad Request:**
```json
{
  "error": "ValidationError",
  "message": "Email is invalid",
  "details": [
    {
      "field": "email",
      "message": "Invalid email format"
    }
  ]
}
```

**401 Unauthorized:**
```json
{
  "error": "Unauthorized",
  "message": "Invalid or expired token"
}
```

**403 Forbidden:**
```json
{
  "error": "Forbidden",
  "message": "You don't have permission to access this resource"
}
```

**404 Not Found:**
```json
{
  "error": "NotFound",
  "message": "Resource not found"
}
```

**429 Too Many Requests:**
```json
{
  "error": "RateLimitExceeded",
  "message": "Too many requests. Try again in 60 seconds"
}
```

**500 Internal Server Error:**
```json
{
  "error": "InternalServerError",
  "message": "An unexpected error occurred"
}
```

---

## Rate Limiting

- Auth endpoints: 5 requests/minute per IP
- API endpoints: 60 requests/minute per user
- Proposal creation: 10 per hour per user
- Photo uploads: 1 per minute per user

Headers returned:
```
X-RateLimit-Limit: 60
X-RateLimit-Remaining: 55
X-RateLimit-Reset: 1705329000
```
