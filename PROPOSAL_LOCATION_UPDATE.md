# Proposal System Update - Meeting Location & Max Distance

## 🎯 Summary of Changes

The proposal system has been completely redesigned to include **meeting locations** and **distance filtering**. Users now set a meeting location and max distance when creating a proposal, making it easier to find suitable chess partners nearby.

**Commit:** `831073f`

---

## 📍 What Changed

### Before
- Proposer creates proposal without location
- Receiver sets meeting location when accepting
- No distance information shown

### After
- Proposer sets meeting location AND max distance when creating proposal
- Meeting location is stored with the proposal
- Receiver sees distance from their location to meeting location
- Receiver simply accepts (no need to set location again)

---

## 🔄 User Flow

### Creating a Proposal (User A)

```
1. User A opens map
2. Sets their desired meeting location (e.g., Chess Club at 30.0444, 31.2357)
3. Sets max distance filter (e.g., 10 km)
4. Selects opponent (User B)
5. Sends proposal with: message + location + max distance
```

### Receiving a Proposal (User B)

```
1. User B gets notification of new proposal
2. Opens proposal and sees:
   - Proposer's profile
   - Meeting location (coordinates + address)
   - Max distance filter (10 km radius)
   - Distance from User B to meeting location (e.g., 2.5 km)
3. Can accept if they're within max distance and available
4. On accept → Match is created at the meeting location
```

---

## 📊 Data Structure Changes

### Proposal Model
```csharp
public class Proposal
{
    public Point? MeetingLocation { get; set; }    // NEW: Where proposer wants to play
    public int MaxDistanceKm { get; set; } = 10;   // NEW: Max distance to search
    
    // ... existing fields (id, proposer_id, receiver_id, status, etc.)
}
```

### Database Schema
```sql
ALTER TABLE proposals ADD COLUMN 
  meeting_location GEOGRAPHY(POINT, 4326),
  max_distance_km INT DEFAULT 10;

CREATE INDEX idx_proposals_meeting_location 
  ON proposals USING GIST(meeting_location);
```

---

## 🔌 API Changes

### Create Proposal

**Old Endpoint:**
```
POST /api/v1/proposals
{
  "receiverId": "...",
  "message": "..."
}
```

**New Endpoint:**
```
POST /api/v1/proposals
{
  "receiverId": "...",
  "message": "Want to play at Cairo Chess Club?",
  "meetingLatitude": 30.0444,
  "meetingLongitude": 31.2357,
  "maxDistanceKm": 10
}

Response:
{
  "id": "...",
  "status": "pending",
  "meetingLocation": {
    "latitude": 30.0444,
    "longitude": 31.2357
  },
  "maxDistanceKm": 10,
  ...
}
```

### Get Incoming Proposals

**Old Response:**
```json
{
  "proposals": [
    {
      "id": "...",
      "proposer": { "username": "...", ... },
      "message": "...",
      "status": "pending"
    }
  ]
}
```

**New Response:**
```json
{
  "proposals": [
    {
      "id": "...",
      "proposer": { "username": "...", ... },
      "message": "...",
      "meetingLocation": {
        "latitude": 30.0444,
        "longitude": 31.2357
      },
      "maxDistanceKm": 10,
      "distanceFromYouKm": 2.5,  // NEW: Distance from your location
      "status": "pending"
    }
  ]
}
```

### Accept Proposal

**Old Endpoint:**
```
POST /api/v1/proposals/{id}/accept
{
  "meetingLocation": {
    "latitude": 30.0444,
    "longitude": 31.2357
  }
}
```

**New Endpoint:**
```
POST /api/v1/proposals/{id}/accept
{
  // Meeting location already set in proposal - just confirm acceptance
}
```

---

## 💾 Database Migration

### For New Installations
Just run:
```bash
dotnet ef database update
```

### For Existing Databases
Apply migration:
```bash
dotnet ef migrations add AddMeetingLocationToProposals
dotnet ef database update
```

Or use raw SQL (see MIGRATION_GUIDE.md):
```sql
ALTER TABLE proposals ADD COLUMN 
  meeting_location GEOGRAPHY(POINT, 4326),
  max_distance_km INT DEFAULT 10;

CREATE INDEX idx_proposals_meeting_location 
  ON proposals USING GIST(meeting_location);
```

---

## 📱 Mobile Integration Example

### React Native Code

#### Creating a Proposal
```typescript
const createProposal = async (receiverId, meetingLocation, maxDistanceKm) => {
  const response = await fetch('https://api.example.com/api/v1/proposals', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify({
      receiverId,
      message: 'Want to play at Cairo Chess Club?',
      meetingLatitude: meetingLocation.latitude,
      meetingLongitude: meetingLocation.longitude,
      maxDistanceKm: maxDistanceKm || 10
    })
  });
  
  return response.json();
};
```

#### Viewing Incoming Proposals
```typescript
const getIncomingProposals = async () => {
  const response = await fetch(
    'https://api.example.com/api/v1/proposals/incoming?status=pending',
    {
      headers: { 'Authorization': `Bearer ${token}` }
    }
  );
  
  const data = await response.json();
  
  // Now you have:
  data.proposals.forEach(proposal => {
    console.log(`${proposal.proposer.username}`);
    console.log(`Location: ${proposal.meetingLocation.latitude}, ${proposal.meetingLocation.longitude}`);
    console.log(`Distance: ${proposal.distanceFromYouKm} km`);
    console.log(`Max range: ${proposal.maxDistanceKm} km`);
  });
};
```

#### Accepting a Proposal
```typescript
const acceptProposal = async (proposalId) => {
  const response = await fetch(
    `https://api.example.com/api/v1/proposals/${proposalId}/accept`,
    {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({}) // No location needed!
    }
  );
  
  return response.json();
};
```

---

## ✅ Benefits

| Feature | Benefit |
|---------|---------|
| **Meeting Location Set Upfront** | Clear, unambiguous location for both players |
| **Max Distance Filter** | Proposer controls search radius (1-100 km) |
| **Distance Display** | Receiver knows exactly how far they need to travel |
| **Spatial Indexes** | Fast geographic queries using PostGIS |
| **Simplified Accept** | One-click acceptance, no extra steps |
| **Better UX** | All information visible upfront |

---

## 🧪 Testing Checklist

After applying this update:

- [ ] Create a proposal with meeting location
  ```bash
  POST /api/v1/proposals
  { 
    "receiverId": "...",
    "meetingLatitude": 30.0444,
    "meetingLongitude": 31.2357,
    "maxDistanceKm": 10
  }
  ```

- [ ] Get incoming proposals and verify distance shown
  ```bash
  GET /api/v1/proposals/incoming
  ```

- [ ] Accept a proposal (no meeting location in request)
  ```bash
  POST /api/v1/proposals/{id}/accept
  {}
  ```

- [ ] Verify match was created at proposal's meeting location
  ```bash
  GET /api/v1/matches/history
  ```

---

## 📋 Files Modified

1. **Models.cs** - Added MeetingLocation and MaxDistanceKm to Proposal
2. **DTOs.cs** - Updated request/response models
3. **ApplicationDbContext.cs** - Added spatial configuration
4. **ProposalService.cs** - Updated service logic
5. **chess_app_schema.sql** - Updated database schema
6. **chess_app_api_spec.md** - Updated API documentation
7. **MIGRATION_GUIDE.md** - Step-by-step migration instructions

---

## 🔄 Breaking Changes

⚠️ **Note:** This is a breaking change for the mobile app

### Old Mobile Code (Won't Work)
```typescript
// Old way - won't work anymore
await api.post('/proposals', {
  receiverId: userId,
  message: "Want to play?"
  // Missing: meetingLatitude, meetingLongitude, maxDistanceKm
});
```

### New Mobile Code (Required)
```typescript
// New way - required
await api.post('/proposals', {
  receiverId: userId,
  message: "Want to play?",
  meetingLatitude: 30.0444,
  meetingLongitude: 31.2357,
  maxDistanceKm: 10
});
```

---

## 🚀 Next Steps

1. **Apply Database Migration**
   ```bash
   dotnet ef database update
   ```

2. **Update Mobile App**
   - Update proposal creation form to include meeting location picker
   - Display distance in incoming proposals list
   - Simplify accept flow (remove location input)

3. **Test Thoroughly**
   - Test with multiple users at different locations
   - Verify distance calculations are correct
   - Test edge cases (max distance = 1 km, 100 km, etc.)

4. **Deploy to Production**
   - Backup database first
   - Apply migration on production database
   - Deploy new backend code
   - Deploy new mobile app

---

## 💬 Questions?

See:
- **MIGRATION_GUIDE.md** - Detailed migration instructions
- **chess_app_api_spec.md** - Full API documentation
- **QUICK_REFERENCE.md** - Code examples

---

**Status:** ✅ Complete and pushed to GitHub  
**Repository:** https://github.com/MudatherZaki/chess-matching-app  
**Commit:** `831073f`

