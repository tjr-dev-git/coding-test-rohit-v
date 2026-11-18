# Active context

Last updated: 2026-11-18  
Phase: **POST, PATCH, and Angular UI complete and verified; handoff cleanup pending**  
Application code changed: **yes — POST, PATCH, tests, and UI**

## 1. Current objective

Implement the Material Movement Dispatch Board brief end-to-end without
rewriting the supplied architecture:

1. **POST create — complete and verified.**
2. **PATCH assignment/status — complete and verified.**
3. **Angular dispatch board and dialogs — complete and verified.**
4. **Backend and UI tests — complete and passing.**
5. Remaining: secret/evaluator cleanup and final handoff.

## 2. Completed work

### POST `api/sites/{siteId}/movements`

- `MovementService.CreateAsync`:
  - verifies the site inside its partition,
  - validates through `MovementRules.ValidateCreate`,
  - creates a `Movement` with a GUID ID, `Status = Requested`, no equipment,
    and `RequestedAtUtc`/`LastUpdatedAtUtc` from `TimeProvider`,
  - trims material and location text before persisting,
  - saves once and returns the DTO, including the Cosmos-generated ETag.
- `MovementsController.Create` maps:
  - success → `201` with `MovementDto`,
  - business validation → `400` `ValidationProblemDetails` with field keys,
  - missing site → `404` `ProblemDetails`.
- `MutationResult` supports field-level `MutationError` entries.
- `MovementRules.ValidateCreate` enforces:
  - material 2–100 characters after trimming,
  - source/destination 2–120 characters after trimming,
  - source ≠ destination, case-insensitive,
  - tonnes > 0 and ≤ 500,
  - priority defined (`Normal` or `Urgent`).
- `CreateMovementRequest.EstimatedTonnes` annotation is `[Range("0","500")]`
  so the strict `> 0` rule is enforced by the service.

### PATCH `api/sites/{siteId}/movements/{movementId}`

- `MovementService.UpdateAsync`:
  - loads the movement with
    `WithPartitionKey(siteId).SingleOrDefaultAsync(x => x.Id == movementId && x.SiteId == siteId, ...)`;
  - returns `404` when missing;
  - requires a non-empty ETag and compares it to the loaded ETag **before**
    business validation; mismatch → `409` with the current DTO;
  - rejects undefined status numerics;
  - rejects all changes to terminal movements (`Completed`, `Cancelled`);
  - applies the approved transition matrix;
  - rejects empty `equipmentId` strings;
  - when `equipmentId` is supplied, loads the equipment inside the site
    partition and rejects missing/cross-site equipment;
  - checks availability only when the supplied equipment differs from the
    current assignment;
  - requires effective equipment for `Assigned` and `InProgress`;
  - updates `Status`, optionally `EquipmentId`, and `LastUpdatedAtUtc`;
  - sets the ETag property's `OriginalValue`, saves, and maps
    `DbUpdateConcurrencyException` to `409` with the freshly loaded current DTO;
  - returns the updated DTO on success.
- `MovementRules` adds:
  - `IsTerminal`,
  - `RequiresEquipment`,
  - `IsTransitionAllowed`.
- `MovementsController.Update` uses the shared `MapMutation` path and returns:
  - `200` with the updated DTO,
  - `400` `ValidationProblemDetails`,
  - `404` `ProblemDetails`,
  - `409` `ProblemDetails` with `Extensions["current"]` when available.
- `MutationResult` adds an optional `Current` DTO for conflicts.

### Approved PATCH transition matrix

| From \ To | Requested | Assigned | InProgress | Completed | Cancelled |
|---|---:|---:|---:|---:|---:|
| Requested | allow | allow* | reject | reject | allow |
| Assigned | reject | allow* | allow* | reject | allow |
| InProgress | reject | reject | allow* | allow | allow |
| Completed | reject | reject | reject | reject | reject |
| Cancelled | reject | reject | reject | reject | reject |

`*` requires effective equipment. `equipmentId: null` means “keep existing”,
not “unassign”.

### Tests

- `MovementsControllerTests` covers:
  - GET,
  - POST success, field validation, generic validation, missing site,
  - PATCH success, validation, missing movement, conflict with `current`.
- `MovementRulesTests` covers:
  - all create rules and boundaries,
  - terminal/active status classification,
  - the supported transitions and key rejected transitions,
  - equipment requirements for each status.
- Backend: `dotnet test DispatchBoard.slnx --nologo` → **47 passed, 0 failed**.
- UI: `npm test -- --watch=false --browsers=ChromeHeadless` → **8 passed,
  0 failed**.

### UI

- New pure helpers in `ui/src/app/movement-ui.ts`:
  - status labels/icons,
  - priority icons,
  - terminal/equipment rules,
  - allowed transitions,
  - site-local time formatting,
  - site-local shift calculation from an instant.
- New `MovementCreateDialogComponent`:
  - Material reactive form with required/length/number validators,
  - same-route group validator,
  - server `400` field errors mapped back to controls,
  - `404`/`409`/network summary messages,
  - creates through `DispatchApiService.createMovement`,
  - returns the created movement to the board.
- New `MovementManageDialogComponent`:
  - summary of the movement, route, tonnes, equipment, and site-local times,
  - status options from the same transition matrix,
  - dynamic equipment-required validator,
  - unavailable equipment disabled unless it is the current assignment,
  - no unassign option when equipment is already assigned,
  - sends the row ETag with the update,
  - maps `400` field errors back to the form,
  - maps `409` to a conflict panel showing the latest server state,
  - terminal movements are read-only with a status banner.
- `AppComponent`:
  - signals for sites, equipment, movements, loading, error, search, priority,
  - `Subject` + `switchMap` refresh pipeline with stale-request guarding,
  - server-side status filter; client-side search and priority filter,
  - summary metrics for active/urgent/unassigned,
  - desktop Material table and mobile cards,
  - loading, empty, filtered-empty, and error/retry states,
  - create/manage dialog launch and row replacement/removal,
  - after create: clear filters, navigate to the created movement's site-local
    shift, refresh, and show a snackbar,
  - after conflict: refresh and show an assertive snackbar,
  - snackbar feedback for create/update/conflict.
- `styles.scss` adds a screen-reader-only utility and a focus-visible outline.
- `_chips.scss` and `_dialog-shared.scss` share chip, status, error, and
  spinner styles between the board and dialogs.
- UI build: `npm run build` succeeds; initial bundle 684.89 kB raw /
  153.01 kB transfer.

### Environment

- `global.json` uses `rollForward: latestFeature` so the installed SDK
  `10.0.401` can build the pinned `10.0.202` floor.
- `npm ci` completed in `ui/`.
- The dev server must be started with `npm start` so the Angular proxy
  (`proxy.conf.json`) forwards `/api` and `/health` to port 5050. A plain
  `ng serve` without the proxy returns `index.html` for API routes with HTTP
  200 and breaks JSON parsing.

## 3. Important implementation notes

### ETag ordering

The ETag comparison happens before transition/equipment validation. This means
a stale request always gets `409`, even if its requested status would also be
invalid. That is deliberate: the client should reload the latest movement
before attempting any further change.

The Cosmos provider uses the ETag property's `OriginalValue` as `If-Match`.
A Cosmos `412` becomes `DbUpdateConcurrencyException`; there is no automatic
retry.

### Equipment semantics

- `equipmentId: null` keeps the current equipment.
- Availability is checked only when the supplied equipment is different from
  the current one.
- This lets an operator cancel or complete a movement that retains an
  assignment whose availability flag has since changed.
- The UI does not offer an unassign action because the API has no explicit
  unassign contract.

### Shift navigation

The board defaults to the seeded `2026-08-17` day shift. A movement created
“now” belongs to the current site-local shift, so after creation the UI:

1. clears the status/search/priority filters,
2. computes the shift using the site's IANA time zone,
3. navigates to that date/shift if needed,
4. refreshes and shows a snackbar.

### Conflict handling

The API returns `409` with the latest DTO when a concurrent update is detected.
The manage dialog shows the latest status/equipment/update time and replaces
the form with a conflict panel. “Reload board” closes the dialog with a
conflict result; the board refreshes and announces the reload. No silent retry
or overwrite occurs.

## 4. API semantics

- Unknown site on create → `404`.
- Unknown movement on patch → `404`.
- Unknown/unavailable/cross-site equipment → `400` with an `EquipmentId` error.
- Stale ETag → `409`, no write.
- Terminal movement change → `400`.
- Invalid transition → `400`.
- Missing equipment for `Assigned`/`InProgress` → `400`.
- ETag is in the request body; no `If-Match` request header is used.
- `409` includes the current movement under the `current` extension when
  available.

## 5. File status

### Changed

- `global.json`
- `src/Dispatch.Api/Contracts/MovementContracts.cs`
- `src/Dispatch.Api/Controllers/MovementsController.cs`
- `src/Dispatch.Api/Dispatch.Api.csproj`
- `src/Dispatch.Api/Services/MovementService.cs`
- `src/Dispatch.Api/Services/ServiceModels.cs`
- `tests/Dispatch.Api.Tests/MovementsControllerTests.cs`
- `ui/src/app/app.component.ts`
- `ui/src/app/app.component.html`
- `ui/src/app/app.component.scss`
- `ui/src/app/models.ts`
- `ui/src/styles.scss`
- `AI-NOTES.md`
- `CODEBASE-NOTES.md`
- `ACTIVE-CONTEXT.md`

### New

- `src/Dispatch.Api/Services/MovementRules.cs`
- `tests/Dispatch.Api.Tests/MovementRulesTests.cs`
- `ui/src/app/movement-ui.ts`
- `ui/src/app/movement-ui.spec.ts`
- `ui/src/app/movement-create-dialog.component.ts`
- `ui/src/app/movement-create-dialog.component.html`
- `ui/src/app/movement-create-dialog.component.scss`
- `ui/src/app/movement-manage-dialog.component.ts`
- `ui/src/app/movement-manage-dialog.component.html`
- `ui/src/app/movement-manage-dialog.component.scss`
- `ui/src/app/_chips.scss`
- `ui/src/app/_dialog-shared.scss`

### Unchanged / intentionally preserved

- `src/Dispatch.Api/Data/SeedData.cs`
- `src/Dispatch.Api/Data/DispatchDbContext.cs`
- Existing GET/shift logic and route shape.
- `ui/src/app/dispatch-api.service.ts` (its create/update methods were already
  present).
- `ui/src/app/app.component.spec.ts` (the existing initial-load test still
  passes).

### Still needs attention

- `src/Dispatch.Api/appsettings.json` — pre-existing staged Cosmos key; rotate
  and move it to `appsettings.Development.json` or user secrets.
- `src/Dispatch.Api/appsettings.Development.json` — does not exist.
- `evaluator/` — tracked but deleted in the working tree; decision still open.

## 6. Ordered work plan

- [x] **Phase 0 — unblock environment**
  - [x] Resolve SDK resolution (`latestFeature`).
  - [x] `npm ci`.
  - [x] Baseline API startup and GET.
  - [ ] Move/rotate the Cosmos key (pending; do not commit the staged key).
- [x] **Phase 1 — backend create**
  - [x] `MovementRules` + `CreateAsync` + controller mapping.
  - [x] Controller and rule tests.
  - [x] Manual `201`/`400`/`404` verification.
- [x] **Phase 2 — backend update**
  - [x] Transitions, equipment, terminal, ETag/409.
  - [x] Manual verification of stale, terminal, unavailable, and cross-site
        cases.
- [x] **Phase 3 — update controller tests**
  - [x] Update success, invalid, missing, conflict.
- [x] **Phase 4 — UI create flow**
  - [x] Create dialog, validation, priority, success navigation.
- [x] **Phase 5 — UI manage flow**
  - [x] Assignment, status, conflict handling, row updates.
- [x] **Phase 6 — UI polish**
  - [x] Mobile cards, focus/labels, non-colour cues, empty/error recovery,
        responsive layout.
- [x] **Phase 7 — verification and notes**
  - [x] Backend/UI build and tests.
  - [x] Browser walkthrough of create, assign, conflict, terminal, and search.
  - [x] Notes updated.
- [ ] **Phase 8 — handoff cleanup**
  - [ ] Rotate and relocate the Cosmos key.
  - [ ] Decide whether to restore `evaluator/` in this working tree.
  - [ ] Optional: add automated dialog component tests.

## 7. Verification log

| Date | Check | Result |
|---|---|---|
| 2026-11-18 | Read all API, UI, test, config, scripts, and evaluator files | Complete |
| 2026-11-18 | Confirmed EF Core Cosmos ETag behaviour against provider source | ETag set after writes; `If-Match` uses `OriginalValue`; 412 → `DbUpdateConcurrencyException` |
| 2026-11-18 | `dotnet --version` after `rollForward` change | `10.0.401` resolved |
| 2026-11-18 | `dotnet test DispatchBoard.slnx --nologo` (POST slice) | 15 passed, 0 failed |
| 2026-11-18 | Live POST valid create | `201`; `IND-JSP`; `Requested`; non-empty ETag |
| 2026-11-18 | Live POST same route differing by case | `400` with both route errors |
| 2026-11-18 | Live POST zero tonnes | `400` with `EstimatedTonnes` error |
| 2026-11-18 | Live POST unknown site | `404` with detail |
| 2026-11-18 | `dotnet test DispatchBoard.slnx --nologo` (PATCH slice) | 47 passed, 0 failed |
| 2026-11-18 | Live PATCH create → assign | `201` → `200`; Assigned with `PC-17` |
| 2026-11-18 | Live PATCH stale ETag | `409` with current state |
| 2026-11-18 | Live PATCH `Assigned → InProgress`, null equipment | `200`; `PC-17` retained |
| 2026-11-18 | Live PATCH `InProgress → Completed` | `200` |
| 2026-11-18 | Live PATCH terminal/backward/unavailable/cross-site/missing-equipment/missing-movement | `400`/`404` as expected |
| 2026-11-18 | `npm ci` in `ui/` | Completed |
| 2026-11-18 | `npm run build` | Success; 684.89 kB raw / 153.01 kB transfer |
| 2026-11-18 | `npm test -- --watch=false --browsers=ChromeHeadless` | 8 passed, 0 failed |
| 2026-11-18 | Browser: load board | Site/date/shift loaded; mobile cards responsive; summary metrics correct |
| 2026-11-18 | Browser: create Chromium ore | Created; navigated to `2026-11-18` night; card visible; snackbar shown |
| 2026-11-18 | Browser: assign `PC-17` | Card updated to Assigned; unassigned count dropped |
| 2026-11-18 | Browser: concurrent update + stale submit | `409` conflict panel with latest state; Reload board refreshed to In progress |
| 2026-11-18 | Browser: completed movement | Read-only terminal banner; only Close |
| 2026-11-18 | Browser: search “Chromium” | One matching card; clearing restored five |

## 8. Blockers and risks

1. **Cosmos secret** — `appsettings.json` still contains the staged account
   key. Rotate and move it to the gitignored development file or user secrets
   before committing or handing off.
2. **Equipment race** — assignment checks availability but does not reserve
   equipment; two dispatchers could assign the same unit.
3. **No real-time refresh** — concurrent changes are detected on submit, not
   proactively.
4. **No equipment unassignment** — the API's `null` semantics mean “keep
   existing”; the UI does not offer an unassign action.
5. **Cosmos integration testing** — controller/rule tests and Karma tests
   cannot exercise `WithPartitionKey` or ETags. Live verification remains
   necessary.
6. **Seed date vs now** — the board defaults to `2026-08-17`; creation
   navigates to the created movement's current shift.
7. **Evaluator directory** — tracked but deleted in the working tree. Do not
   restore it into a candidate copy without confirmation.
8. **Test data** — the live verification created and changed a few movements in
   the shared Cosmos database.
9. **Desktop visual check** — the browser walkthrough ran at a narrow viewport
   and exercised the mobile cards. The desktop table compiled and is wired to
   the same handlers; a manual desktop-width pass is still worthwhile.

## 9. Open questions / handoff

1. Rotate and relocate the Cosmos connection string to
   `appsettings.Development.json` or user secrets.
2. Restore `evaluator/` in this working tree, or leave it deleted?
3. Optional: add component-level dialog tests with `MatDialogHarness` or a
   TestBed-based dialog test.
4. Optional: add a desktop-width visual/browser pass.
