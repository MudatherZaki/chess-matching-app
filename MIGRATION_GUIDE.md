# Database Migration Guide - Add Meeting Location to Proposals

## Changes Made

The proposals system has been updated to include:
- **meeting_location** (Geography Point) - Where the proposer wants to play
- **max_distance_km** (Int) - Max distance from meeting location to search for players

## How to Apply Migration

### Option 1: Using EF Core Migrations (Recommended)

If you're starting fresh or have not pushed to production:

```bash
# Create a new migration
dotnet ef migrations add AddMeetingLocationToProposals

# Apply the migration
dotnet ef database update
```

### Option 2: Using Raw SQL

If you need to apply this to an existing database:

```sql
-- Add new columns to proposals table
ALTER TABLE proposals
ADD COLUMN meeting_location GEOGRAPHY(POINT, 4326),
ADD COLUMN max_distance_km INT DEFAULT 10;

-- Create spatial index for meeting location queries
CREATE INDEX idx_proposals_meeting_location ON proposals USING GIST(meeting_location);

-- Update existing proposals with default max distance (optional)
UPDATE proposals SET max_distance_km = 10 WHERE max_distance_km IS NULL;
```

## What Changed in Code

### 1. Proposal Model (`Models.cs`)
```csharp
public class Proposal
{
    // ... existing fields ...
    
    public Point? MeetingLocation { get; set; } // Where proposer wants to play
    public int MaxDistanceKm { get; set; } = 10; // Max distance to search for players
}
```

### 2. Create Proposal Request DTO (`DTOs.cs`)
```csharp
public class CreateProposalRequest
{
    public Guid ReceiverId { get; set; }
    public string? Message { get; set; }
    
    // NEW: Meeting location where proposer wants to play
    public double MeetingLatitude { get; set; }
    public double MeetingLongitude { get; set; }
    
    // NEW: Max distance from meeting location to search for players
    public int MaxDistanceKm { get; set; } = 10;
}
```

### 3. Accept Proposal Request DTO (`DTOs.cs`)
```csharp
public class AcceptProposalRequest
{
    // Meeting location is already set in the proposal when it was created
    // No request body needed anymore
}
```

### 4. Proposal Service (`ProposalService.cs`)

**Creating proposals now requires meeting location:**
```csharp
// Old way (no longer works)
POST /api/v1/proposals
{
  "receiverId": "...",
  "message": "..."
}

// New way (required)
POST /api/v1/proposals
{
  "receiverId": "...",
  "message": "...",
  "meetingLatitude": 30.0444,
  "meetingLongitude": 31.2357,
  "maxDistanceKm": 10
}
```

**Accepting proposals is simplified:**
```csharp
// Old way
POST /api/v1/proposals/{id}/accept
{
  "meetingLocation": {
    "latitude": 30.0444,
    "longitude": 31.2357
  }
}

// New way
POST /api/v1/proposals/{id}/accept
{
  // Meeting location already set in proposal - just send empty JSON
}
```

### 5. Incoming Proposals Response (`DTOs.cs`)

Responses now include distance from user to proposal's meeting location:

```json
{
  "id": "...",
  "proposer": { ... },
  "status": "pending",
  "message": "...",
  "meetingLocation": {
    "latitude": 30.0444,
    "longitude": 31.2357
  },
  "maxDistanceKm": 10,
  "distanceFromYouKm": 2.5,  // NEW: Distance from your location to meeting
  "expiresAt": "...",
  "createdAt": "..."
}
```

## Database Schema Changes

### Before
```sql
CREATE TABLE proposals (
    id UUID PRIMARY KEY,
    proposer_id UUID NOT NULL,
    receiver_id UUID NOT NULL,
    status proposal_status DEFAULT 'pending',
    responded_at TIMESTAMP WITH TIME ZONE,
    expires_at TIMESTAMP WITH TIME ZONE,
    message TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT different_users CHECK (proposer_id != receiver_id)
);
```

### After
```sql
CREATE TABLE proposals (
    id UUID PRIMARY KEY,
    proposer_id UUID NOT NULL,
    receiver_id UUID NOT NULL,
    status proposal_status DEFAULT 'pending',
    responded_at TIMESTAMP WITH TIME ZONE,
    expires_at TIMESTAMP WITH TIME ZONE,
    message TEXT,
    meeting_location GEOGRAPHY(POINT, 4326),  -- NEW
    max_distance_km INT DEFAULT 10,            -- NEW
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    CONSTRAINT different_users CHECK (proposer_id != receiver_id)
);

-- NEW: Spatial index for queries
CREATE INDEX idx_proposals_meeting_location ON proposals USING GIST(meeting_location);
```

## Mobile App Integration

### Creating a Proposal (Mobile)

```typescript
// Old way
const proposal = await api.post('/proposals', {
  receiverId: userId,
  message: "Want to play?"
});

// New way - include meeting location and max distance
const proposal = await api.post('/proposals', {
  receiverId: userId,
  message: "Want to play?",
  meetingLatitude: userLocation.latitude,
  meetingLongitude: userLocation.longitude,
  maxDistanceKm: 10  // Players within 10km of meeting location can accept
});
```

### Accepting a Proposal (Mobile)

```typescript
// Old way
await api.post(`/proposals/${proposalId}/accept`, {
  meetingLocation: {
    latitude: userLocation.latitude,
    longitude: userLocation.longitude
  }
});

// New way - simpler, meeting location already set
await api.post(`/proposals/${proposalId}/accept`, {});
```

### Viewing Incoming Proposals (Mobile)

```typescript
const proposals = await api.get('/proposals/incoming');

// Now includes distance from your location to meeting location
proposals.forEach(proposal => {
  console.log(`${proposal.proposer.username} wants to play`);
  console.log(`Meeting location: ${proposal.meetingLocation}`);
  console.log(`Distance from you: ${proposal.distanceFromYouKm}km`);
  console.log(`Max distance filter: ${proposal.maxDistanceKm}km`);
});
```

## Rollback (If Needed)

If you need to rollback this migration:

```bash
# If using EF Core migrations
dotnet ef migrations remove

# Then reapply with the previous migration
dotnet ef database update PreviousMigrationName
```

Or with raw SQL:

```sql
ALTER TABLE proposals DROP COLUMN IF EXISTS meeting_location;
ALTER TABLE proposals DROP COLUMN IF EXISTS max_distance_km;
DROP INDEX IF EXISTS idx_proposals_meeting_location;
```

## Testing

After applying the migration, test the new flow:

```bash
# 1. Create a proposal with meeting location
curl -X POST https://localhost:5001/api/v1/proposals \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "receiverId": "550e8400-e29b-41d4-a716-446655440001",
    "message": "Want to play at Cairo Chess Club?",
    "meetingLatitude": 30.0444,
    "meetingLongitude": 31.2357,
    "maxDistanceKm": 10
  }'

# 2. Check incoming proposals (should show distance)
curl -X GET https://localhost:5001/api/v1/proposals/incoming \
  -H "Authorization: Bearer $TOKEN"

# 3. Accept proposal (simpler now)
curl -X POST https://localhost:5001/api/v1/proposals/{proposalId}/accept \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{}'
```

## Benefits

✅ **Clearer Intent** - Proposers set exact meeting location upfront  
✅ **Distance Filtering** - Can see how far each proposal is  
✅ **Spatial Queries** - Can filter nearby proposals  
✅ **Better UX** - No need to set location twice  
✅ **Scalable** - Uses PostGIS spatial indexes  

---

**Migration Complete!** Your proposal system now includes location-aware matching. 🚀
