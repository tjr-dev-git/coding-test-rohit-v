# Codebase notes — working understanding

Last updated: 2026-11-18  
Status: POST, PATCH, and Angular UI implemented and verified.

## 1. Purpose

This file is a quick reference for how the starter works and where the
Material Movement Dispatch Board requirements land in the code. It is not a
substitute for `CANDIDATE-BRIEF.md`.

## 2. Solution map

| Path | What it is |
|---|---|
| `src/Dispatch.Api/` | ASP.NET Core Web API, `net10.0`, EF Core Cosmos |
| `tests/Dispatch.Api.Tests/` | xUnit + Moq controller and rule tests |
| `ui/` | Angular 20 standalone app, Angular Material, Karma/Jasmine |
| `scripts/` | `setup.ps1`, `run.ps1`, `stop.ps1` |
| `DispatchBoard.slnx` | Solution containing the API and API tests only; the UI is built with npm |
| `evaluator/` | Private black-box evaluator kit; must not be shipped to candidates |

The UI dev server runs on `http://localhost:4200`; `proxy.conf.json` forwards
`/api` and `/health` to `http://localhost:5050`.

## 3. Runtime flow

```text
Angular (4200)
  -> /api proxy
    -> ASP.NET Core controllers (5050)
      -> IMovementService
        -> DispatchDbContext (EF Core Cosmos)
          -> Cosmos DB database "DispatchBoard"
```

At startup, `Program.cs`:

1. Reads `ConnectionStrings:Cosmos` and `Cosmos:DatabaseName`.
2. Registers `DispatchDbContext` with the Cosmos provider (Gateway mode).
3. Registers `IMovementService` and `TimeProvider.System`.
4. Maps controllers, CORS for `localhost:4200`, and `GET /health`.
5. Runs `SeedData.InitialiseAsync` before serving requests.

## 4. Persistence model

### Containers

| Entity | Key | Partition key | Notes |
|---|---|---|---|
| `Movement` | `(Id, SiteId)` | `SiteId` | `ETag` is configured with `IsETagConcurrency()`; priority/status stored as strings |
| `Site` | `Id` | `Id` | `TimeZoneId` is an IANA ID |
| `Equipment` | `(Id, SiteId)` | `SiteId` | `IsAvailable` is an operator flag, not a reservation |

### Movement fields

`Id`, `SiteId`, `MaterialType`, `SourceLocation`, `DestinationLocation`,
`EstimatedTonnes` (decimal), `Priority`, `Status`, `EquipmentId`,
`RequestedAtUtc`, `LastUpdatedAtUtc`, `ETag`.

The DTO exposes these fields. `ETag` serializes as `eTag` (camel-case), which
matches the Angular model and the evaluator's model.

### Seed data (important)

The seed is deliberately built around a fixed date, **2026-08-17**, and the
India site uses `Asia/Kolkata` (UTC+5:30):

- `ind-before-day` at `00:29Z` = 05:59 IST → previous night, not 17 Aug day.
- `ind-0600` at `00:30Z` = 06:00 IST → 17 Aug day.
- `shared-001` exists at both sites with different data.
- `ind-night` at `12:30Z` = 18:00 IST → 17 Aug night.
- `ind-midnight` at `18:30Z` = 00:00 IST on 18 Aug → 17 Aug night (cancelled).
- `HT-09` is unavailable; `PC-17` and `WL-04` are available.
- `PC-17` exists at both sites, so every equipment lookup must be site-scoped.

Do not change the seed IDs, dates, or shift boundaries; the hidden black-box
checks depend on them.

## 5. API surface

| Method | Route | Behaviour |
|---|---|---|
| GET | `/api/sites` | Returns sites ordered by name |
| GET | `/api/sites/{siteId}/equipment` | Site-partitioned equipment |
| GET | `/api/sites/{siteId}/movements?shiftDate=YYYY-MM-DD&shift=day\|night&status=...` | Site-partitioned, shift-bounded movement list |
| POST | `/api/sites/{siteId}/movements` | `201` with created representation; `400` validation details; `404` unknown site |
| PATCH | `/api/sites/{siteId}/movements/{movementId}` | `200` updated representation; `400` validation/transition; `404` missing; `409` stale ETag with `current` |

The PATCH request carries `status`, optional `equipmentId`, and the row's
`eTag` in the JSON body. There is no `If-Match` request header and no
GET-by-id endpoint. POST returns `201` without a `Location` header.

## 6. Business rules and where they belong

| Rule | Server enforcement | UI mirror |
|---|---|---|
| Day 06:00–18:00, night 18:00–06:00 in site local time | Existing `GetShiftBounds` converts to UTC bounds | Display site-local times; shift navigation after create |
| Shift date = local date the shift begins | Existing `GetShiftBounds` + query | Shift date input; helper for the created movement's shift |
| Source and destination must differ | `MovementRules`, trimmed + case-insensitive | Create form group validator |
| Tonnes > 0 and <= 500 | Contract annotation + `MovementRules` | Number validators |
| Equipment must exist, be available, belong to site | Service, partitioned equipment lookup | Site equipment list; disable unavailable |
| `Assigned` / `InProgress` require equipment | `MovementRules.RequiresEquipment` | Manage dialog dynamic validator |
| Legal status transitions only | `MovementRules.IsTransitionAllowed` | Status options come from the same helper |
| `Completed` / `Cancelled` terminal | `MovementRules.IsTerminal` | Read-only manage dialog + terminal banner |
| No silent overwrite | ETag compare, `OriginalValue`, `409` | Send row ETag; show latest state; reload |

Additional policy implemented:

- Same-status updates are allowed for non-terminal movements.
- No backward transitions.
- `Assigned → Completed` is rejected; `InProgress → Completed` is allowed.
- `equipmentId: null` means “keep existing”, not “unassign”.
- Availability is checked when equipment is newly supplied/changed, not when
  an existing assignment is retained.

## 7. Key code paths

### API

- `src/Dispatch.Api/Controllers/MovementsController.cs`
  - `Get` is unchanged.
  - `Create` maps success to `201`, validation to `400`
    `ValidationProblemDetails`, and missing sites to `404`.
  - `Update` uses the shared mutation mapping: `200`, `400`, `404`, or `409`
    with the `current` extension.
- `src/Dispatch.Api/Services/MovementService.cs`
  - `GetSitesAsync`, `GetMovementsAsync`, `GetEquipmentAsync` are unchanged.
  - `CreateAsync`: site check, rules, trimmed values, `Requested` status,
    `TimeProvider` timestamps, GUID ID, save, DTO.
  - `UpdateAsync`: partition-scoped movement load, ETag-first conflict check,
    terminal/transition/equipment rules, `OriginalValue` concurrency,
    `DbUpdateConcurrencyException` → `409` with the latest DTO.
  - `GetShiftBounds` is the existing site-local shift conversion.
- `src/Dispatch.Api/Services/MovementRules.cs`
  - `ValidateCreate` checks trimmed text lengths, source/destination
    inequality (case-insensitive), tonnes range, and defined priority.
  - `IsTerminal`, `RequiresEquipment`, and `IsTransitionAllowed` hold the PATCH
    policy.
- `src/Dispatch.Api/Services/ServiceModels.cs`
  - `MutationOutcome`: `Success`, `Invalid`, `NotFound`, `Conflict`.
  - `MutationResult` carries an optional DTO, error string, field-level
    `MutationError` entries, and an optional `Current` DTO for conflicts.
- `src/Dispatch.Api/Services/MovementMappings.cs`
  - `ToDto()` maps all entity fields, including ETag.
- `src/Dispatch.Api/Contracts/MovementContracts.cs`
  - `CreateMovementRequest` uses DataAnnotations.
  - `UpdateMovementRequest` requires `Status` and `ETag`, with optional
    `EquipmentId`.
- `src/Dispatch.Api/Data/DispatchDbContext.cs`
  - Cosmos containers, composite keys, partition keys, ETag concurrency.
- `src/Dispatch.Api/Data/SeedData.cs`
  - Fixed seed records; do not alter.

### UI

- `ui/src/app/app.component.ts`
  - Signals for `sites`, `equipment`, `movements`, `loading`, `error`, search,
    priority, and the selected shift.
  - `Subject` + `switchMap` refresh pipeline; stale responses are guarded.
  - Server-side status filter; client-side search and priority filter.
  - Summary metrics for active/urgent/unassigned.
  - Dialog orchestration; row replacement/removal after create/update.
  - After create: clear filters, navigate to the created movement's site-local
    shift, refresh, snackbar.
  - After conflict: refresh and show an assertive snackbar.
- `ui/src/app/app.component.html`
  - Toolbar, filters, summary metrics, loading/empty/error states.
  - Desktop Material table and mobile cards with the same actions.
  - Create/manage dialog entry points.
- `ui/src/app/movement-ui.ts`
  - Pure helpers for status/priority labels and icons, terminal/equipment
    rules, allowed transitions, site-local time formatting, and site-local
    shift calculation.
- `ui/src/app/movement-create-dialog.component.ts` / `.html` / `.scss`
  - Reactive create form, same-route validator, server error mapping, create
    call, success result.
- `ui/src/app/movement-manage-dialog.component.ts` / `.html` / `.scss`
  - Summary, transition options, dynamic equipment validation, ETag submission,
    `400` mapping, `409` conflict panel, terminal read-only state.
- `ui/src/app/dispatch-api.service.ts`
  - `getSites`, `getEquipment`, `getMovements`, `createMovement`,
    `updateMovement` use the right routes.
- `ui/src/app/models.ts`
  - TypeScript mirrors of the API DTOs; `eTag` naming is correct.
  - `ValidationProblemDetails` mirrors the API error shape.
- `ui/src/app/_chips.scss`, `ui/src/app/_dialog-shared.scss`
  - Shared chip/status/error/spinner styles.
- `ui/src/styles.scss`
  - Screen-reader-only utility and focus-visible outline.

## 8. Tests

`tests/Dispatch.Api.Tests/MovementsControllerTests.cs`:

- GET list.
- POST success, field validation, generic validation, missing site.
- PATCH success, validation, missing movement, conflict with `current`.

`tests/Dispatch.Api.Tests/MovementRulesTests.cs`:

- Create rules and boundaries.
- Terminal/active classification.
- The supported transitions and key rejected transitions.
- Equipment requirements for each status.

The backend suite is **47 tests, all passing** with
`dotnet test DispatchBoard.slnx`.

The UI suite is **8 tests, all passing** with
`npm test -- --watch=false --browsers=ChromeHeadless` (initial board load plus
seven pure UI-helper tests).

The test project uses xUnit 2.9.3, Moq 4.20.72, and strict mocks.
`TreatWarningsAsErrors` is enabled in `Directory.Build.props`.

The evaluator kit (when present) contains black-box tests for:

- Shift partitioning and night crossing midnight.
- Create `201`, ETag, and same-route `400`.
- `Requested → Assigned → InProgress`, and missing movement `404`.
- Stale ETag `409`.
- Terminal cancelled movement cannot be reopened (`400`).

## 9. Technical gotchas

1. **ETag is store-generated.** EF Core Cosmos sets the ETag on the entity
   after a create/replace, so both POST and PATCH responses can include it.
2. **ETag is sent as `If-Match` from `OriginalValue`.** Setting
   `entry.Property(x => x.ETag).OriginalValue` makes the provider use the
   client's value. A Cosmos `412` becomes `DbUpdateConcurrencyException`.
3. **ETag values include quotes.** Never trim or normalize them.
4. **Check ETag before business rules on PATCH.** A stale request should
   reliably return `409`, even if its requested status would also be invalid.
5. **Route comparison must be case-insensitive.** `"Same area"` and
   `"same area"` are the same location for the rule.
6. **Partition every movement/equipment read and write by `siteId`.** Do not
   use cross-partition queries for a single-site operation.
7. **Do not flip `Equipment.IsAvailable` on assignment.** Seed data assigns
   available equipment to multiple movements; availability is a static flag.
8. **Do not treat `equipmentId: null` as unassign.** It means “keep existing”.
   Availability is checked only when the supplied equipment changes.
9. **The board defaults to 2026-08-17.** A movement created “now” will not
   appear in that shift. After creation the UI navigates to the created
   movement's site-local shift.
10. **The API returns UTC; dispatchers think in site-local time.** Format times
    with the selected site's time zone.
11. **Undefined enum numerics can pass model binding.** Validate with
    `Enum.IsDefined`.
12. **The tonnes annotation is `[Range("0","500")]`.** The service enforces the
    strict `> 0` rule, so zero reaches the service and returns a field-level
    `400` rather than the automatic model-validation response.
13. **`TreatWarningsAsErrors` and strict Angular templates** mean new code must
    be warning-free and fully typed.
14. **`GetMovements` returns `[]` for an unknown site.** Leave the existing GET
    contract alone; mutations return `404`.
15. **`AnyAsync` on a `WithPartitionKey` site query failed against the live
    Cosmos account** with `Identifier 'root' could not be resolved`. The
    working pattern, already used by the read path, is
    `WithPartitionKey(...).SingleOrDefaultAsync(...)`. Keep new queries in that
    shape unless verified against the live account.
16. **Plain `ng serve` does not use the proxy.** The Angular app returns
    `index.html` for `/api/...` (HTTP 200) and breaks JSON parsing. Start it
    with `npm start`, which passes `proxy.conf.json`.
17. **Material `mat-select` is not a native `<select>`.** Browser automation
    must click the combobox and then the option, not use a native select
    helper.

## 10. Environment notes

- `global.json` originally pinned SDK `10.0.202` with `latestPatch`; only
  `10.0.401` was installed, so `dotnet` commands failed. The working tree now
  uses `rollForward: latestFeature`, which resolves to `10.0.401`.
- `dotnet test DispatchBoard.slnx --nologo` passes with 47 tests.
- `npm ci` has been run in `ui/`; `npm run build` succeeds and
  `npm test -- --watch=false --browsers=ChromeHeadless` passes with 8 tests.
- UI build: initial bundle 684.89 kB raw / 153.01 kB transfer, under the 2 MB
  budget.
- Node `v24.21.0`, npm `11.19.0`, and Chrome are available.
- The configured Cosmos endpoint was reachable from this machine, and the live
  POST/PATCH verification succeeded.
- `appsettings.json` currently has a staged change containing a Cosmos account
  key. That key must be rotated and moved to the gitignored
  `appsettings.Development.json` or user secrets before any commit.
- The live verification created and changed a few test movements in the shared
  Cosmos database.
- The `evaluator/` directory is tracked but currently deleted in the working
  tree. It is private and should not be restored into a candidate copy.
- The temporary API and UI dev-server processes used for verification were
  stopped afterward.

## 11. Do not touch

- `CANDIDATE-BRIEF.md`
- `evaluator/` (private)
- `src/Dispatch.Api/Data/SeedData.cs`
- `src/Dispatch.Api/Data/DispatchDbContext.cs`
- Existing `GetShiftBounds` / GET query behaviour
- `ui/angular.json`, `ui/tsconfig*.json`, `ui/package.json` unless a new package
  is genuinely needed
