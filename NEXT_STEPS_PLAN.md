# Chess Matching App — 1-Week Completion Plan

**Goal:** Feature-complete, tested app running end-to-end (backend deployed + mobile app installable on iOS/Android) by end of next week.

**Honest scope note:** "Complete" here means *feature-complete, deployed to staging, and installable on real devices for testing*. App Store / Google Play review (1–3+ days each, outside our control) realistically won't finish inside this week — plan for **submission-ready**, not **live in stores**, by Friday.

---

## Status (updated as work lands)

- ✅ **BE-1, BE-2, BE-3** — Proposal/Match/Block controllers built and wired up
- ✅ **BE-7** — SignalR broadcasts wired into proposal create/accept/reject
- ✅ **BE-9** — Migration/schema drift checked (found and fixed real inconsistencies between `ApplicationDbContext.cs` and `chess_app_schema.sql`); seed script added at `scripts/seed_test_data.sh`
- ✅ **BE-5** — Password hashing moved from unsalted SHA-512 to bcrypt
- ✅ **BE-4** — FluentValidation validators added for all 8 mutating endpoints, via a hand-written async action filter (not the deprecated `FluentValidation.AspNetCore` package)
- ✅ **CI** — `.github/workflows/backend-ci.yml` added; backend now actually compiles and is verified green on every push (it did not compile at all when this plan was first written - see commit history for what that took)
- ✅ **[.NET 10 migration](https://github.com/MudatherZaki/chess-matching-app/issues/1)** — done ahead of the originally-planned "after this sprint" timing, at the user's request. Green on CI. Caught and avoided a known, currently-open runtime-only bug in `JwtBearer 10.0.1` (dotnet/aspnetcore#64932) that CI's restore+build alone would not have caught. Issue closed.
- ⬜ BE-6 (rate limiting), BE-8 (unit tests) — not started
- ⬜ Everything under Mobile (MO-\*) and Ops (OPS-\*) — not started

---

## How to use this doc

- Each task has an ID, owner track, estimate, dependencies, and acceptance criteria — pick one up, do it, check it off.
- Tasks in the same track can mostly run in parallel with each other; cross-track dependencies are called out explicitly.
- Sized for **3–4 developers**. If you have fewer people, follow the priority order within each track (P0 before P1 before P2).

---

## Critical path (do these first, in order — everything else depends on them)

1. **BE-1, BE-2, BE-3** (missing controllers) →
2. **BE-9** (migrations verified) →
3. **BE-10 / OPS-1** (staging deployment) →
4. **MO-9** (mobile e2e testing against live backend)

If only one backend dev is available, they should do BE-1 → BE-2 → BE-3 → BE-9 → BE-10 before anything else on this list, since mobile testing is blocked until a real API exists to hit.

---

## Backend (.NET) — Track BE

### BE-1 — Proposal Controller `P0` · ~0.5 day
Wire the existing `ProposalService` to HTTP endpoints (service layer is done, only the controller is missing).
- `POST /api/v1/proposals`, `GET /proposals/incoming`, `GET /proposals/outgoing`, `POST /proposals/{id}/accept`, `POST /proposals/{id}/reject`, `POST /proposals/{id}/cancel`
- **Acceptance:** all 6 endpoints return correct status codes per `chess_app_api_spec.md`; manual Postman check against a seeded user pair.
- **Depends on:** nothing.

### BE-2 — Match Controller `P0` · ~0.5 day
- `POST /api/v1/matches`, `GET /api/v1/matches/history`
- **Acceptance:** recording a match updates `UserStats` (wins/losses/draws) correctly.
- **Depends on:** nothing.

### BE-3 — Block Controller `P0` · ~0.25 day
- `POST /api/v1/blocks`, `DELETE /api/v1/blocks/{userId}`, `GET /api/v1/blocks`
- **Acceptance:** a blocked user can no longer send proposals to the blocker (covered by existing `ProposalService` check — just confirm the controller exposes it).
- **Depends on:** nothing.

### BE-4 — FluentValidation on all DTOs `P1` · ~1 day
Register/login/create-proposal/record-match/update-profile requests currently have no server-side validation beyond manual checks.
- **Acceptance:** invalid payloads return `400` with field-level error messages matching the `ApiError` shape mobile already expects.
- **Depends on:** BE-1, BE-2 (validators live next to the controllers they guard).

### BE-5 — Password hashing: SHA-512 → bcrypt `P1` · ~0.5 day
- **Acceptance:** new registrations hash with bcrypt; document that existing seeded/test accounts will need to re-register (no production users yet, so no migration needed).
- **Depends on:** nothing.

### BE-6 — Rate limiting `P2` · ~0.5 day
Add `AspNetCoreRateLimit` (or equivalent) on `/auth/*` and `/proposals` to prevent abuse.
- **Acceptance:** hitting `/auth/login` >10x/min from one IP returns `429`.
- **Depends on:** nothing. Can be deferred past this week if time is tight.

### BE-7 — Wire SignalR broadcasts into services `P0` · ~1 day
`NotificationHub` exists but isn't invoked from `ProposalService`/`UserService` yet.
- Broadcast `ProposalReceived` on proposal creation, `ProposalResponded` on accept/reject, `UserAvailabilityChanged` on availability toggle.
- **Acceptance:** two Postman/websocket-test-client connections can see events fire live.
- **Depends on:** BE-1. **Blocks:** MO-1 (mobile can't test real-time until this exists).

### BE-8 — Unit tests for core services `P1` · ~1.5 days
xUnit + Moq (already in the `.csproj`) for `AuthService`, `ProposalService`, `UserService` — focus on the paths that would silently break things: blocking checks, duplicate-proposal checks, token refresh, availability expiry.
- **Acceptance:** `dotnet test` passes; critical-path coverage, not 100%.
- **Depends on:** BE-1–BE-3 (nothing to test on the controller layer until they exist, though services can be tested standalone sooner).

### BE-9 — Verify migrations & seed data `P0` · ~0.5 day
Confirm `dotnet ef migrations add InitialCreate` + `database update` produce a schema matching current `Models.cs` (especially after the proposal `maxDistanceKm` removal). Add a seed script with 5–10 test users at varied locations for mobile testing.
- **Acceptance:** fresh DB + migration + seed script gets a mobile dev to a usable test environment in one command.
- **Depends on:** nothing. **Blocks:** BE-10, MO-9.

### BE-10 — Deploy to Azure (staging) `P0` · ~1 day
Azure App Service (API) + Azure Database for PostgreSQL (with PostGIS) + Azure Blob Storage, with secrets in App Service configuration (not `appsettings.json`).
- **Acceptance:** `https://<staging-url>/swagger` is reachable and mobile's `.env` can point at it.
- **Depends on:** BE-9. **Blocks:** MO-9, X-4.

---

## Mobile (React Native) — Track MO

### MO-1 — SignalR client integration `P0` · ~1 day
Connect on login, disconnect on logout; listen for `ProposalReceived` / `ProposalResponded` / `UserAvailabilityChanged` and update `proposalStore` / `locationStore` live instead of only on pull-to-refresh.
- **Acceptance:** accepting a proposal on Device A removes it from Device B's incoming list within a couple seconds, no manual refresh.
- **Depends on:** BE-7.

### MO-2 — Profile photo upload UI `P0` · ~0.5 day
`apiClient.uploadPhoto` already exists but nothing in `EditProfileScreen` calls it. Add `react-native-image-picker`, a tap-to-change-photo affordance, and wire it up.
- **Acceptance:** picking a photo updates the avatar on `ProfileScreen` immediately after upload.
- **Depends on:** nothing.

### MO-3 — Environment config `P0` · ~0.25 day
Replace the hardcoded `BASE_URL` in `src/services/api.ts` with `.env`-driven config (`react-native-config` or `react-native-dotenv`) so the app can point at local / staging / prod without code changes.
- **Acceptance:** switching `.env` and rebuilding hits a different API without touching `api.ts`.
- **Depends on:** nothing. **Should land before** MO-9 so testers can point at staging easily.

### MO-4 — iOS native setup `P0` · ~1 day
`Info.plist` location permissions, `Podfile` finalized, Google Maps iOS API key wired in, app builds and runs on simulator without crashing (fonts for `react-native-vector-icons` are a common gotcha — confirm they're linked).
- **Acceptance:** app launches on an iOS simulator, login screen renders correctly.
- **Depends on:** nothing.

### MO-5 — Android native setup `P0` · ~1 day
`AndroidManifest.xml` permissions, Google Maps Android API key, app builds and runs on an emulator.
- **Acceptance:** app launches on an Android emulator, login screen renders correctly.
- **Depends on:** nothing.

### MO-6 — App icons & splash screen `P1` · ~0.5 day
Currently using default RN branding. Needs at minimum a placeholder chess-themed icon set for both platforms.
- **Acceptance:** custom icon shows on home screen for both iOS and Android builds.
- **Depends on:** nothing.

### MO-7 — Forgot Password screen `P2` · ~0.5 day
Currently a placeholder in `AuthStack`. **Needs a backend password-reset endpoint that doesn't exist yet** — either scope this down to "Contact support" copy for launch, or add a paired backend task. Flagging so it's a conscious decision, not a forgotten one.
- **Depends on:** a new backend task if done properly; otherwise trivial as a stub.

### MO-8 — Push notifications `P2` · ~1.5 days
Firebase Cloud Messaging (Android) + APNs (iOS) so a proposal shows a system notification when the app is backgrounded. This is the single most time-consuming mobile item — **treat as stretch goal**, cut first if the week is running short.
- **Depends on:** MO-1 (real-time infra should exist first), device provisioning for APNs (needs an Apple Developer account).

### MO-9 — End-to-end testing against staging `P0` · ~1 day
Full manual pass on real/simulated devices: register → login → set availability → discover nearby players → propose → accept (other device) → record match → view history → block/unblock → logout. Log bugs as you go.
- **Depends on:** BE-10, MO-3, MO-4, MO-5.

### MO-10 — Error states & polish `P1` · ~0.5 day
Network-failure toasts, retry affordances, empty/loading state sanity check across all 14 screens (most already have `LoadingSpinner`/`EmptyState` — this is about catching the ones that don't).
- **Depends on:** nothing, but best done after MO-9 surfaces real gaps.

---

## Cross-cutting / Infra — Track OPS

### OPS-1 — Google Maps API keys `P0` · ~0.25 day
Provision iOS + Android Maps SDK keys (Google Cloud Console), set billing alerts, restrict keys by bundle ID/package name. **This is a "someone with account access" task — do it Day 1**, it blocks MO-4/MO-5.
- **Depends on:** nothing. **Blocks:** MO-4, MO-5.

### OPS-2 — API contract sync check `P1` · ~0.25 day
Once BE-1–BE-3 land, do a quick pass comparing `chess_app_api_spec.md` against the actual controllers and update either the docs or the mobile `types/index.ts` if anything drifted (the earlier `maxDistanceKm` removal is a good example of the kind of drift to watch for).
- **Depends on:** BE-1, BE-2, BE-3.

### OPS-3 — Full QA pass `P0` · ~1 day
Same flow as MO-9 but done by someone who *didn't* write the code, on both platforms, specifically trying to break things (bad input, airplane mode mid-request, expired tokens, etc.).
- **Depends on:** MO-9.

### OPS-4 — Store listing prep `P1` · ~1 day
Bundle IDs, provisioning profiles, screenshots, store descriptions, privacy policy page (location + photo data requires one for both stores). Get this submitted by Friday even if review hasn't completed — that's the realistic "done" state for this week.
- **Depends on:** MO-6 (icons), OPS-3 (no point submitting a broken build).

---

## Suggested day-by-day (4-person team: 1 backend, 2 mobile, 1 floating/DevOps+QA)

| Day | Backend dev | Mobile dev A | Mobile dev B | Floating (OPS/QA) |
|-----|---|---|---|---|
| **Mon** | BE-1, BE-2, BE-3 | MO-4 (iOS setup) | MO-5 (Android setup) | OPS-1 (Maps keys) — unblocks MO-4/5 by midday |
| **Tue** | BE-9, BE-7 (SignalR) | MO-3 (env config), MO-2 (photo upload) | MO-1 (SignalR client) — pairs with backend once BE-7 lands | OPS-2 (contract sync) |
| **Wed** | BE-10 (deploy staging) | MO-6 (icons/splash) | MO-1 cont'd / MO-10 (polish) | Prep test accounts, staging smoke test |
| **Thu** | BE-4 (validation), BE-5 (bcrypt) | MO-9 (e2e testing, iOS) | MO-9 (e2e testing, Android) | OPS-3 (independent QA pass) |
| **Fri** | BE-6, BE-8 (tests) — or bug fixes from Thu's QA | Bug fixes from QA | Bug fixes from QA | OPS-4 (store submission prep) |

**MO-8 (push notifications) and MO-7 (forgot password) are intentionally not scheduled** — treat them as buffer/stretch work only if Thu/Fri finish early. Cutting them doesn't block a usable, demoable app.

---

## What "done by next week" looks like

- ✅ Backend fully deployed to Azure staging, all endpoints live and validated
- ✅ Mobile app installs and runs on both iOS and Android (simulator or physical device)
- ✅ Full user journey works end-to-end: discover → propose → accept → play → record → history
- ✅ Real-time proposal notifications work while app is open (push notifications are stretch)
- ✅ Known bugs from QA triaged (fixed or explicitly deferred with a reason)
- ⏳ App store submissions filed, review pending (outside our control to accelerate)
