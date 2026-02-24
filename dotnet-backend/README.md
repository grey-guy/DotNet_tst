# .NET Backend Test Project

This is the **.NET (C#) developer test project** backend. It mirrors the dotnet backend's API and behavior using **ASP.NET Core minimal APIs**.

## Stack

- .NET 10.0 (SDK)
- ASP.NET Core Minimal APIs
- Serilog (structured request logging)

## Project Structure

```
dotnet-backend/
├── dotnet-backend.csproj
├── Program.cs                  # App bootstrap, middleware, DI registration
├── Models/
│   ├── User.cs
│   ├── TaskItem.cs
│   ├── Requests.cs             # CreateUserRequest, CreateTaskRequest, UpdateTaskRequest
│   ├── Responses.cs
│   └── ValidationResult.cs     # Result type returned by ValidationService
├── Services/
│   └── ValidationService.cs    # Centralised business-rule validation
├── Endpoints/
│   ├── UserEndpoints.cs
│   ├── TaskEndpoints.cs
│   └── UtilityEndpoints.cs
└── Data/
    └── DataStore.cs            # Thread-safe in-memory data store
```

## API Overview

The .NET backend exposes the same read-only endpoints as the dotnet backend:

- `GET /health` – Health check
- `GET /api/users` – Get all users
- `GET /api/users/{id}` – Get a single user by ID
- `GET /api/tasks` – Get tasks, supports filters:
  - `GET /api/tasks?status=pending`
  - `GET /api/tasks?userId=1`
  - `GET /api/tasks?status=pending&userId=1`
- `GET /api/stats` – Aggregate statistics for users and tasks

Data is stored **in-memory** with a thread-safe `DataStore`, matching the dotnet backend's sample data and behavior.

## Running the .NET Backend

From the repository root:

```bash
cd dotnet-backend
# Make sure .NET 8 SDK is installed:
#   dotnet --version

dotnet run
```

The backend will start on:

- `http://localhost:8080`

You can override the port with the `PORT` environment variable:

```bash
PORT=8081 dotnet run
```

## Endpoints (Details)

### `GET /health`
- **Response**:
  ```json
  {"status": "ok", "message": ".NET backend is running"}
  ```

### `GET /api/users`
- **Response**:
  ```json
  {
    "users": [ { "id": 1, "name": "John Doe", ... } ],
    "count": 3
  }
  ```

### `GET /api/users/{id}`
- Returns `404` with `{ "error": "User not found" }` if user is not found.

### `GET /api/tasks`
- Query params:
  - `status`: `"pending" | "in-progress" | "completed"`
  - `userId`: integer user ID

### `GET /api/stats`
- **Response**:
  ```json
  {
    "users": { "total": 3 },
    "tasks": {
      "total": 3,
      "pending": 1,
      "inProgress": 1,
      "completed": 1
    }
  }
  ```

## For .NET Candidates

As a .NET developer, you will primarily work in the `dotnet-backend/` folder.

Typical test tasks (analogous to the dotnet test) would be:
- Implement `POST /api/users` – create user
- Implement `POST /api/tasks` – create task
- Implement `PUT /api/tasks/{id}` – update task
- Add structured request logging (middleware or filters)
- (Optionally) add persistence, validation, etc.

Focus on writing clean, idiomatic C# with ASP.NET Core best practices.

## Architecture Decisions

### Centralised Validation (`Services/ValidationService`)

All business-rule validation lives in `ValidationService` (static class) rather than being duplicated across HTTP handlers and the data layer.

**Why a separate service instead of inline validation?**

- **Single source of truth** — rules like the valid status enum (`pending`, `in-progress`, `completed`), the valid role enum (`developer`, `designer`, `manager`, `admin`), and the email regex are defined once. Adding or changing a rule automatically affects every caller.
- **Data layer safety** — `DataStore` methods call the same validators before acquiring their write lock. This means rules are enforced even if `DataStore` is called outside the REST API (tests, background jobs, CLI tools, etc.), not only when a request arrives through the HTTP pipeline.
- **No circular dependency** — `ValidationService` knows nothing about `DataStore`. Where validation requires a database look-up (e.g. "does this userId exist?"), the check is injected as a `Func<int, bool>` delegate. Callers supply the lookup; the service stays independent and is straightforward to unit-test in isolation.
- **Consistent error shape** — `ValidationResult` collects all validation errors in a single pass and returns them together, so clients receive the full list of problems in one response rather than one error at a time.

**Error handling contract:**
- HTTP endpoints receive a `ValidationResult` and return `400 Bad Request` with `{ "errors": [...] }`.
- `DataStore` methods throw `ArgumentException` when validation fails – an unexpected condition from the data layer's perspective, surfaced as a `500` by the global exception handler.

### Thread-Safe Data Store

`DataStore` uses `ReaderWriterLockSlim` to allow concurrent reads while serialising writes. All validation runs *outside* the write lock so contention on the lock is kept as short as possible.

### ID Generation

New IDs are calculated as `max(existingIds) + 1` at write time (inside the write lock). This keeps `DataStore` free of a separate mutable counter whose value could drift from the actual collection contents.
