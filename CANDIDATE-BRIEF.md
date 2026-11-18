# Material Movement Dispatch Board

## The scenario

Dispatchers at fictional steel-service sites coordinate requests to move materials between locations. They need to see the current shift's work, create requests, assign equipment, and track progress. More than one dispatcher may update a movement at the same time.

This is a fictional coding exercise, not a real Harsco Environmental process or safety system. No metals-industry knowledge is expected.

## Your task

You have **150 minutes**. Completing everything is not expected. Prioritise a useful end-to-end slice, keep the application runnable, and be ready to explain your choices and unfinished work.

The starter already lists movements and equipment. Complete as much as is useful of:

1. Implement `POST /api/sites/{siteId}/movements`.
2. Implement `PATCH /api/sites/{siteId}/movements/{movementId}` for equipment assignment and status changes.
3. Build a clear Angular Material dispatch board that supports site/shift selection, filtering, creation, assignment, and status changes.
4. Add meaningful xUnit/Moq controller tests for successful and failure behavior.
5. Record assumptions and AI usage in `AI-NOTES.md`.

You may alter supplied code or contracts when you can explain the reason.

## Business rules

- A site has day (`06:00–18:00`) and night (`18:00–06:00`) shifts in its local time.
- A shift date is the local date on which the shift begins.
- Source and destination must differ.
- Estimated tonnes must be greater than zero and no more than 500.
- Equipment must exist, be available, and belong to the movement's site.
- `InProgress` requires assigned equipment.
- `Completed` and `Cancelled` are terminal.
- Concurrent dispatchers must not silently overwrite each other's updates.

Valid statuses are `Requested`, `Assigned`, `InProgress`, `Completed`, and `Cancelled`. Priorities are `Normal` and `Urgent`.

## API behavior

Existing:

- `GET /api/sites`
- `GET /api/sites/{siteId}/equipment`
- `GET /api/sites/{siteId}/movements?shiftDate=YYYY-MM-DD&shift=day|night&status=...`

To complete:

- `POST /api/sites/{siteId}/movements` returns `201` on success.
- `PATCH /api/sites/{siteId}/movements/{movementId}` returns the updated representation.
- Return validation details for invalid requests, `404` for missing resources, and `409` for stale updates.

## UI expectations

Aim for an operational interface rather than a collection of default controls. Consider loading, empty, error, validation, and stale-update states; mobile and desktop layouts; keyboard use; labels; and status communication that does not rely on colour alone.

## Review

After coding, you will have 20 minutes to demonstrate the application and discuss priorities, tests, Cosmos choices, UI decisions, AI use, and what you would improve next.

