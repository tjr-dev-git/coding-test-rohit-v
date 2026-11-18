# AI usage notes

AI assistance is allowed. This file records the tools, prompts, decisions, and
verification for this exercise. It contains no account keys or full chat
transcripts.

## Tools used

- Tool/model: DeepSeek V4.1 Flash Free, running in OpenCode.
- What I used it for: codebase exploration, requirement and business-rule
  mapping, API/UI solution design, POST and PATCH implementation, Angular
  board/dialog implementation, test drafting, live API verification, an
  end-to-end browser walkthrough, and documentation.
- OpenCode capabilities used: file reading/editing, PowerShell shell commands,
  `dotnet test`, `npm ci`, `npm run build`, `npm test`, HTTP calls to a locally
  running API, and the browser tools for UI verification.

## Important prompts and decisions

| Prompt summary | Suggestion received | Accepted, changed, or rejected | Why |
|---|---|---|---|
| Analyse the starter and map the brief to the code | Identified the stubbed POST/PATCH path, existing ETag/partition configuration, read-only UI, and missing mutation tests | Accepted | Matched direct source inspection |
| Propose an implementation approach | Keep the supplied Controller → Service → EF Cosmos seam; add a pure rules class and Material dialogs rather than a rewrite | Accepted | Fits the existing architecture and the exercise scope |
| How should optimistic concurrency work? | Compare the request ETag, set it as the concurrency token's original value, save, and translate a Cosmos `412` into `409` | Accepted | Confirmed against the EF Core Cosmos provider source |
| Should equipment availability be reserved on assignment? | Keep `IsAvailable` as an operator flag; validate it when assigning, but do not mutate it | Accepted | Seed data assigns available equipment to multiple movements |
| How should same-route validation compare values? | Trim and compare case-insensitively | Accepted | The evaluator's invalid route example differs only by case |
| Should the UI recover from a stale update? | Show a conflict state and reload; do not retry automatically | Accepted | The brief explicitly forbids silent overwrites |
| Should the board default to today's shift? | Keep the seeded `2026-08-17` default for the demo, then navigate to the created movement's shift after creation | Accepted | Seed data is fixed to that date |
| Where should create validation live? | Keep DataAnnotations for request shape and add a service-side rules class for business rules | Accepted | The service must remain safe even if called directly; the controller's automatic 400 only covers annotations |
| How should the service check that a site exists? | Use a partition-scoped `AnyAsync` query | **Changed** | The live Cosmos query rejected the generated SQL with `Identifier 'root' could not be resolved`; switched to `SingleOrDefaultAsync`, matching the existing movement/site read pattern |
| Should tonnes use the starter's `0.01` minimum? | Align the annotation to `0` and enforce the strict `> 0` rule in the service | Accepted | The brief says greater than zero, not a 10 kg minimum |
| Should POST return a `Location` header? | Not required; return `201` with the created representation | Accepted | There is no GET-by-id endpoint, so a `Location` would not resolve |
| What PATCH transitions are legal? | Same status allowed for non-terminal movements; `Assigned`/`InProgress` require equipment; no backward transitions; `Assigned → Completed` rejected; terminal states immutable | Accepted | Matches the lifecycle implied by the brief and keeps completion auditable |
| In what order should PATCH check ETag and business rules? | Check the loaded movement and ETag first, then status/transition/equipment rules | Accepted | A stale write must reliably return `409` even if the rest of the request is also invalid |
| What does `equipmentId: null` mean on PATCH? | Keep the existing equipment; check availability only when equipment is newly supplied or changed | Accepted | Avoids rejecting a retained assignment if its availability flag later changes |
| How should the UI handle a conflict? | Show the latest server state in the dialog and offer “Reload board”; never retry automatically | Accepted | Preserves the operator's intent and prevents silent overwrites |
| How should the UI be structured? | Signals plus a `Subject`/`switchMap` refresh pipeline; standalone create/manage dialogs; shared pure UI helpers; desktop table plus mobile cards | Accepted | Keeps state predictable and the board usable at both widths |
| Where should a created movement appear? | Clear board filters, navigate to the created movement's site-local shift, and confirm with a snackbar | Accepted | A movement created “now” may not belong to the seeded `2026-08-17` board |
| Should the UI allow unassigning equipment? | Do not offer an unassign action; `null` is a keep-existing signal in the API | Accepted | The API has no explicit unassign contract, so the UI mirrors it honestly |

## Verification

- Read the API controllers, service, contracts, domain model, DbContext, seed
  data, UI components/service/models/styles, test project, scripts, and
  configuration before changing code.

### Backend

- Implemented POST with partition-scoped site lookup, `MovementRules`, server
  timestamps/status, field-level `MutationResult` errors, and controller
  mapping to `201` / `400` `ValidationProblemDetails` / `404` `ProblemDetails`.
- Implemented PATCH with:
  - partition-scoped movement and equipment reads,
  - ETag comparison before business validation,
  - terminal-state and transition checks,
  - equipment existence/availability checks when equipment is supplied,
  - `OriginalValue` ETag concurrency and `DbUpdateConcurrencyException` → `409`,
  - optional latest movement DTO in the conflict response.
- Added controller tests for update success, validation, missing movement, and
  conflict; added rule tests for terminal states, the transition matrix, and
  equipment requirements.
- `dotnet test DispatchBoard.slnx --nologo` passes: **47 tests, 0 failed**.
- Live API verification against Cosmos:
  - POST → `201`, `Requested`, non-empty ETag;
  - GET current-shift query found the created movement;
  - PATCH `Requested → Assigned` with available `PC-17` → `200`;
  - PATCH with the original (stale) ETag → `409` with the current state;
  - PATCH `Assigned → InProgress` with `equipmentId: null` kept `PC-17` → `200`;
  - PATCH `InProgress → Completed` → `200`;
  - PATCH the terminal movement → `400`;
  - PATCH missing movement → `404`;
  - PATCH seeded `Assigned → Completed` → `400`;
  - PATCH unavailable `HT-09` → `400`;
  - PATCH cross-site `HT-22` → `400` (not found in the site partition);
  - PATCH `Assigned` without equipment → `400`;
  - PATCH `Requested → Cancelled` cleanup → `200`.
- The earlier live `AnyAsync` failure was fixed by using the existing
  `WithPartitionKey(...).SingleOrDefaultAsync(...)` pattern. New reads keep that
  shape.

### UI

- `npm ci` completed in `ui/`.
- `npm run build` succeeds. Initial bundle: 684.89 kB raw / 153.01 kB
  transfer, under the 2 MB budget.
- `npm test -- --watch=false --browsers=ChromeHeadless` passes: **8 tests,
  0 failed** (initial board load plus seven pure UI-helper tests).
- Browser walkthrough against the live API:
  - loaded the seeded board and summary counts;
  - created “Chromium ore” (33.5 t, Urgent) through the dialog; the board
    navigated to the created movement's site-local night shift
    (`2026-11-18`), cleared filters, showed the snackbar, and displayed the new
    card;
  - managed it `Requested → Assigned` with `PC-17`; the card updated and the
    unassigned count dropped;
  - updated the movement behind the dialog via the API to `InProgress`; the
    stale dialog submit returned `409`, displayed the latest server state, and
    “Reload board” refreshed the board to `In progress`;
  - opened a completed movement with “View”; the dialog was read-only with a
    terminal banner and only a Close action;
  - client-side search for “Chromium” reduced the board to one card, and
    clearing it restored all five cards.
- A manually started `ng serve` (without the proxy) initially returned the
  Angular `index.html` for `/api/sites` with HTTP 200, which surfaced as a JSON
  parse error in the UI. Restarting with the project's `npm start`
  (`ng serve --proxy-config proxy.conf.json`) fixed it. Use the npm script for
  local verification.
- The temporary API and UI dev-server processes used for verification were
  stopped afterward; no test listener remains on the exercise ports.

## Limitations and unfinished work

- The PATCH and UI slices are implemented and verified. The remaining work is
  handoff cleanup, not feature implementation.
- The UI has unit tests for the pure helpers and the initial board load, but no
  automated component tests for the dialogs. They were verified manually in the
  browser.
- The browser walkthrough exercised the responsive card layout at a narrow
  viewport. The desktop table is compiled and uses the same data/handlers, but
  it was not visually exercised at desktop width in this run.
- No real-time refresh or polling: a concurrent change is detected when the
  operator submits, not before.
- Equipment availability is not reserved; two dispatchers could assign the same
  available unit concurrently.
- The UI cannot unassign equipment because the API treats `equipmentId: null`
  as “keep existing”. Adding unassignment would require an explicit contract.
- `POST` returns `201` with the representation but no `Location` header; there
  is no GET-by-id endpoint.
- The ETag is returned in the body, not headers; the supplied contract uses the
  body.
- The create request's DataAnnotations and `MovementRules` overlap for
  defensive depth. The service remains authoritative for business rules.
- The live verification created and changed a small number of movements in the
  shared Cosmos database.
- The Cosmos connection string is currently staged in tracked configuration;
  it should be rotated and moved to the gitignored `appsettings.Development.json`
  or user secrets.
- `global.json` uses `rollForward: latestFeature` so the installed SDK
  `10.0.401` can build the pinned `10.0.202` floor.
- No authentication, audit history, pagination, real-time refresh, or
  equipment-reservation workflow is included.
- The `evaluator/` directory is tracked but currently deleted in the working
  tree. It is private and should not be included in a candidate copy.
