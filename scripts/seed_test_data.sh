#!/usr/bin/env bash
#
# Seeds the API with test users for mobile/manual testing.
#
# Deliberately goes through the real /auth/register and /availability/set
# endpoints instead of inserting rows directly into the database. That
# means it doesn't care what password hashing scheme is in use (bcrypt,
# or whatever it becomes later) - it's always correct, because it's
# exercising the same code path a real client does.
#
# Usage:
#   BASE_URL=https://your-staging-url ./scripts/seed_test_data.sh
#   ./scripts/seed_test_data.sh                # defaults to http://localhost:5000
#
# Requires: curl, python3 (for JSON parsing - no jq dependency)

set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5000}"
API="$BASE_URL/api/v1"
SEED_PASSWORD="TestPass123!"

# username|full name|email|lat|lon|has_board|fide_rating|chesscom_username
# Coordinates are spread around central Cairo so nearby-search radius
# testing (default 10km) has a realistic mix of in-range and out-of-range
# users depending on which "current location" you test from.
USERS=(
  "nadia_k|Nadia Kamal|nadia.k@example.test|30.0444|31.2357|true|1850|nadiak"
  "omar_h|Omar Hassan|omar.h@example.test|30.0500|31.2400|false|2100|omarh_chess"
  "youssef_m|Youssef Mostafa|youssef.m@example.test|30.0330|31.2230|true||"
  "layla_s|Layla Said|layla.s@example.test|30.0600|31.2500|true|1600|"
  "karim_e|Karim ElSayed|karim.e@example.test|30.0250|31.2100|false|1950|karim_e"
  "mona_a|Mona Adel|mona.a@example.test|30.0700|31.2600|true||"
  "tarek_f|Tarek Fahmy|tarek.f@example.test|29.9800|31.1800|false|2250|tarekf99"
  "dina_r|Dina Rashid|dina.r@example.test|30.1000|31.3000|true|1400|"
)

echo "Seeding $BASE_URL with ${#USERS[@]} test users..."
echo "Password for all seeded accounts: $SEED_PASSWORD"
echo ""

RESULTS_FILE=$(mktemp)
echo "username,email,user_id,access_token,available" > "$RESULTS_FILE"

for entry in "${USERS[@]}"; do
  IFS='|' read -r username fullname email lat lon has_board fide_rating chesscom <<< "$entry"

  register_response=$(curl -s -X POST "$API/auth/register" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"$email\",\"username\":\"$username\",\"password\":\"$SEED_PASSWORD\",\"fullName\":\"$fullname\"}")

  user_id=$(echo "$register_response" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('userId',''))" 2>/dev/null || echo "")
  access_token=$(echo "$register_response" | python3 -c "import json,sys; d=json.load(sys.stdin); print(d.get('accessToken',''))" 2>/dev/null || echo "")

  if [ -z "$user_id" ]; then
    echo "  ✗ $username: registration failed - $register_response"
    continue
  fi

  echo "  ✓ $username registered ($user_id)"

  # Fill in optional profile fields
  if [ -n "$fide_rating" ] || [ -n "$chesscom" ]; then
    profile_body="{"
    [ -n "$fide_rating" ] && profile_body="$profile_body\"fideRating\":$fide_rating,"
    [ -n "$chesscom" ] && profile_body="$profile_body\"chessComUsername\":\"$chesscom\","
    profile_body="${profile_body%,}}"

    curl -s -X PUT "$API/profile/me" \
      -H "Content-Type: application/json" \
      -H "Authorization: Bearer $access_token" \
      -d "$profile_body" > /dev/null
  fi

  # Make roughly 2/3 of seeded users immediately available, so a fresh
  # nearby-search actually returns something without manual setup.
  available="false"
  if (( RANDOM % 3 != 0 )); then
    curl -s -X POST "$API/availability/set" \
      -H "Content-Type: application/json" \
      -H "Authorization: Bearer $access_token" \
      -d "{\"isAvailable\":true,\"latitude\":$lat,\"longitude\":$lon,\"hasBoard\":$has_board,\"expiresInHours\":8}" > /dev/null
    available="true"
  fi

  echo "$username,$email,$user_id,$access_token,$available" >> "$RESULTS_FILE"
done

echo ""
echo "Done. Summary written to $RESULTS_FILE"
echo ""
echo "To log in as any seeded user from the mobile app or curl:"
echo "  email: <username>@example.test style addresses above"
echo "  password: $SEED_PASSWORD"
echo ""
echo "Quick login example:"
echo "  curl -X POST $API/auth/login -H 'Content-Type: application/json' \\"
echo "    -d '{\"email\":\"nadia.k@example.test\",\"password\":\"$SEED_PASSWORD\"}'"
