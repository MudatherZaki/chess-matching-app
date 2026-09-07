-- Chess Matchmaking App - PostgreSQL Schema with PostGIS
-- Install PostGIS extension first: CREATE EXTENSION postgis;

CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS uuid-ossp;

-- =====================================================
-- USERS & AUTHENTICATION
-- =====================================================

CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email VARCHAR(255) UNIQUE NOT NULL,
    username VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    full_name VARCHAR(255),
    
    -- Profile photo
    photo_url VARCHAR(500),
    bio TEXT,
    
    -- Location (PostGIS Point: longitude, latitude)
    location GEOGRAPHY(POINT, 4326),
    last_location_update TIMESTAMP WITH TIME ZONE,
    
    -- Availability state
    is_available BOOLEAN DEFAULT FALSE,
    availability_expires_at TIMESTAMP WITH TIME ZONE, -- Auto-expire after 4-8 hours
    has_board BOOLEAN DEFAULT FALSE,
    
    -- Chess credentials
    fide_id VARCHAR(20),
    fide_rating INT,
    fide_rating_updated_at TIMESTAMP WITH TIME ZONE,
    
    chesscom_username VARCHAR(100),
    chesscom_url VARCHAR(255),
    chesscom_rating INT,
    
    lichess_username VARCHAR(100),
    lichess_url VARCHAR(255),
    lichess_rating INT,
    
    -- Metadata
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    last_login TIMESTAMP WITH TIME ZONE,
    is_active BOOLEAN DEFAULT TRUE,
    
    -- Device tokens for push notifications
    device_tokens TEXT[] DEFAULT ARRAY[]::TEXT[]
);

CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_users_username ON users(username);
CREATE INDEX idx_users_is_available ON users(is_available) WHERE is_available = TRUE;
-- Spatial index for geolocation queries
CREATE INDEX idx_users_location ON users USING GIST(location);
CREATE INDEX idx_users_availability_expires ON users(availability_expires_at) 
    WHERE is_available = TRUE;

-- =====================================================
-- MATCH PROPOSALS / REQUESTS
-- =====================================================

CREATE TYPE proposal_status AS ENUM ('pending', 'accepted', 'rejected', 'expired', 'cancelled');
CREATE TYPE match_outcome AS ENUM ('not_played', 'player1_won', 'player2_won', 'draw');

CREATE TABLE proposals (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Players
    proposer_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    receiver_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    
    -- Status
    status proposal_status DEFAULT 'pending',
    responded_at TIMESTAMP WITH TIME ZONE,
    expires_at TIMESTAMP WITH TIME ZONE DEFAULT (NOW() + INTERVAL '24 hours'),
    
    -- Optional message
    message TEXT,
    
    -- Metadata
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    
    CONSTRAINT different_users CHECK (proposer_id != receiver_id)
);

CREATE INDEX idx_proposals_receiver_pending ON proposals(receiver_id, status) 
    WHERE status = 'pending';
CREATE INDEX idx_proposals_proposer ON proposals(proposer_id);
CREATE INDEX idx_proposals_status ON proposals(status);
CREATE INDEX idx_proposals_expires ON proposals(expires_at);

-- =====================================================
-- MATCHES / COMPLETED GAMES
-- =====================================================

CREATE TABLE matches (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    
    -- Players (player1 usually the one who proposed)
    player1_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    player2_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    
    proposal_id UUID REFERENCES proposals(id) ON DELETE SET NULL,
    
    -- Match details
    played_at TIMESTAMP WITH TIME ZONE NOT NULL,
    location GEOGRAPHY(POINT, 4326), -- Where they played
    
    outcome match_outcome DEFAULT 'not_played',
    played_with_board BOOLEAN,
    
    -- Optional game data
    time_control VARCHAR(50), -- e.g., "5+3", "10+0"
    pgn TEXT, -- Chess notation if recorded
    notes TEXT,
    
    -- Metadata
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_matches_player1 ON matches(player1_id);
CREATE INDEX idx_matches_player2 ON matches(player2_id);
CREATE INDEX idx_matches_played_at ON matches(played_at);

-- =====================================================
-- RATINGS / ELO HISTORY (Optional, for tracking over time)
-- =====================================================

CREATE TABLE rating_snapshots (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    
    -- Which platform
    platform VARCHAR(50), -- 'fide', 'chesscom', 'lichess'
    rating INT NOT NULL,
    
    captured_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_rating_snapshots_user ON rating_snapshots(user_id, platform);

-- =====================================================
-- BLOCKS / REPORTS
-- =====================================================

CREATE TABLE blocks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    blocker_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    blocked_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    reason TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    
    CONSTRAINT different_users CHECK (blocker_id != blocked_id),
    UNIQUE(blocker_id, blocked_id)
);

CREATE INDEX idx_blocks_blocker ON blocks(blocker_id);
CREATE INDEX idx_blocks_blocked ON blocks(blocked_id);

-- =====================================================
-- USER STATISTICS / PROFILE ENRICHMENT
-- =====================================================

CREATE TABLE user_stats (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL UNIQUE REFERENCES users(id) ON DELETE CASCADE,
    
    total_matches_played INT DEFAULT 0,
    total_wins INT DEFAULT 0,
    total_losses INT DEFAULT 0,
    total_draws INT DEFAULT 0,
    
    avg_opponent_rating INT,
    longest_streak INT,
    
    last_stats_update TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_user_stats_user ON user_stats(user_id);

-- =====================================================
-- REFRESH TOKENS
-- =====================================================

CREATE TABLE refresh_tokens (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    
    token_hash VARCHAR(255) NOT NULL UNIQUE,
    expires_at TIMESTAMP WITH TIME ZONE NOT NULL,
    revoked_at TIMESTAMP WITH TIME ZONE,
    
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX idx_refresh_tokens_user ON refresh_tokens(user_id);
CREATE INDEX idx_refresh_tokens_expires ON refresh_tokens(expires_at);

-- =====================================================
-- FUNCTIONS & TRIGGERS
-- =====================================================

-- Auto-update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER users_updated_at_trigger
    BEFORE UPDATE ON users
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER matches_updated_at_trigger
    BEFORE UPDATE ON matches
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Function to find nearby available users
CREATE OR REPLACE FUNCTION find_nearby_available_users(
    user_id UUID,
    radius_km INT DEFAULT 10
)
RETURNS TABLE (
    id UUID,
    username VARCHAR,
    full_name VARCHAR,
    photo_url VARCHAR,
    fide_rating INT,
    chesscom_rating INT,
    lichess_rating INT,
    has_board BOOLEAN,
    distance_km NUMERIC
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        u.id,
        u.username,
        u.full_name,
        u.photo_url,
        u.fide_rating,
        u.chesscom_rating,
        u.lichess_rating,
        u.has_board,
        ROUND(ST_DistanceSphere(
            (SELECT location FROM users WHERE id = user_id),
            u.location
        ) / 1000.0, 2) as distance_km
    FROM users u
    WHERE 
        u.id != user_id
        AND u.is_active = TRUE
        AND u.is_available = TRUE
        AND u.availability_expires_at > NOW()
        AND NOT EXISTS (
            SELECT 1 FROM blocks 
            WHERE (blocker_id = user_id AND blocked_id = u.id)
            OR (blocker_id = u.id AND blocked_id = user_id)
        )
        AND ST_DWithin(
            (SELECT location FROM users WHERE id = user_id),
            u.location,
            radius_km * 1000.0
        )
    ORDER BY distance_km ASC;
END;
$$ LANGUAGE plpgsql;

-- Function to auto-expire availability
CREATE OR REPLACE FUNCTION expire_old_availability()
RETURNS TABLE (expired_count INT) AS $$
DECLARE
    count INT;
BEGIN
    UPDATE users
    SET is_available = FALSE
    WHERE is_available = TRUE
    AND availability_expires_at < NOW();
    
    GET DIAGNOSTICS count = ROW_COUNT;
    RETURN QUERY SELECT count;
END;
$$ LANGUAGE plpgsql;
