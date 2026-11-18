# Material Movement Dispatch Board assessment

This repository is the starter for a 150-minute senior full-stack exercise. Read `CANDIDATE-BRIEF.md` before coding.

## Prerequisites

- .NET SDK 10.0.202 or a compatible .NET 10 patch
- Node.js 20 or later and npm
- PowerShell 7 recommended

## One-time setup

```powershell
.\scripts\setup.ps1
```

Configure the development Cosmos DB account connection string in
`src\Dispatch.Api\appsettings.Development.json`. The file is ignored by Git, so
the account key is not committed.

```powershell
notepad src\Dispatch.Api\appsettings.Development.json
```

Replace `<replace-with-development-cosmos-account-key>` with the development
account key. The API creates and seeds the `DispatchBoard` database on its first
startup. It does not delete the database thereafter. To use a separate database,
set `Cosmos:DatabaseName` in this file before running the API.

## Run

```powershell
.\scripts\run.ps1
```

Open `http://localhost:4200`. The API runs at `http://localhost:5050` and its
health check is `/health`.

To run components in separate terminals:

```powershell
dotnet run --project src/Dispatch.Api/Dispatch.Api.csproj --launch-profile http
```

```powershell
Set-Location ui
npm start
```

## Test and build

```powershell
dotnet test DispatchBoard.slnx
Set-Location ui
npm test -- --watch=false
npm run build
```

## Repository map

- `CANDIDATE-BRIEF.md`: task and business rules.
- `src/`, `tests/`, `ui/`: API, tests, and Angular starter.
- `AI-NOTES.md`: completion template.

`npm audit --omit=dev` is the production-dependency check. Angular 20's pinned build tooling may report development-only transitive advisories; review those before each interview and upgrade within Angular 20 when patched packages become available.
