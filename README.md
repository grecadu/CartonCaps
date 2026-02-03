# CartonCaps Referrals API

This repository contains the **CartonCaps Referrals backend service**, responsible for managing user referral flows.  
It allows an existing user (the **referrer**) to invite a prospective user (the **referee**) via email, SMS, or share flows.

The system generates a referral token/link, persists referrals (in-memory or SQL Server), and tracks the full referral lifecycle.

---

## High-Level Architecture

The solution follows a **Clean / Layered Architecture** approach:

```
API
└── Application
    └── Domain
        └── Infrastructure
```

Each layer has a single responsibility and depends only on abstractions from inner layers.

---

## 1. Domain Layer  
`CartonCaps.Referrals.Domain`

**Purpose**  
Contains the pure business model and domain rules.  
This layer has **no dependencies on frameworks or infrastructure**.

### Core Entity

**Referral**
- Represents an invitation from a referrer to a referee
- Created via the factory method:
  ```
  Referral.Create(...)
  ```
- Enforces all invariants and validations:
  - Contact type validation
  - Referral code format
  - Lifecycle consistency

### State Transitions
- `MarkSent`
- `MarkOpened`
- `MarkInstalled`
- `MarkRegistered`
- `Cancel`

### Key Fields
- `Id` (Guid)
- `ReferrerUserId` (Guid)
- `ReferrerReferralCode` (string)
- `ContactType` (`email` | `sms`)
- `ContactValue` (email or phone number)
- `Channel` (`text` | `email` | `share_sheet`)
- `Status` (enum)
- `LinkToken` (string)
- `CreatedAt` / `LastUpdatedAt` (`DateTimeOffset`)

---

## 2. Application Layer  
`CartonCaps.Referrals.Application`

**Purpose**  
Implements use cases and orchestrates domain behavior.  
This layer contains the application logic but no infrastructure concerns.

### Services

**ReferralService**
- Creates referrals
- Generates referral links
- Lists referrals for a user
- Retrieves referrals by ID
- Resolves referral tokens (deferred deep links)
- Tracks referral lifecycle events

### Contracts (DTOs)
- `CreateReferralRequest`
- `CreateReferralResponse`
- List / Get / Resolve response models

### Abstractions
- `IReferralRepository` – persistence abstraction
- `ICurrentUserContext` – resolves the current user from request headers
- `IClock` – time abstraction for deterministic testing

### Errors
- `ReferralAppException`
  - Contains a machine-readable `Code`
    - `rate_limited`
    - `forbidden`
    - `not_found`
    - etc.
  - Mapped by the API layer to HTTP responses

---

## 3. Infrastructure Layer  
`CartonCaps.Referrals.Infrastructure`

**Purpose**  
Provides concrete implementations for persistence, time, and referral link generation.

### Persistence

Two interchangeable implementations behind `IReferralRepository`:

#### In-Memory
- `InMemoryReferralRepository`
- Intended for:
  - Local development
  - Fast testing
- No external dependencies

#### SQL Server (EF Core)
- `ReferralsDbContext`
- `EfReferralRepository`
- Uses Entity Framework Core
- Supports migrations
- Tested using EF Core InMemory provider in unit tests

### Other Infrastructure Components

**Time**
- `SystemClock : IClock`

**Referral Links**
- `IReferralLinkService`
- `SimpleReferralLinkService`
- Generates `(token, url)` pairs using configuration:
  ```
  ReferralLinks:BaseUrl
  ```

---

## 4. API Layer  
`CartonCaps.Referrals.Api`

**Purpose**  
Exposes HTTP endpoints and wires all layers together.

### Characteristics
- ASP.NET Core Minimal API
- Dependency Injection for all services
- Centralized exception handling
- Swagger enabled in development

### Identity Headers

Every request must include:

- `X-User-Id` (Guid)
- `X-Referral-Code` (string)

For local development, default headers are injected automatically to simplify Swagger testing.

---

## Configuration

### `appsettings.json`

```json
{
  "Persistence": {
    "Provider": "InMemory | SqlServer"
  },
  "ConnectionStrings": {
    "ReferralsDb": "SQL Server connection string"
  },
  "ReferralLinks": {
    "BaseUrl": "http://localhost:5085"
  }
}
```

---

## Running with In-Memory Persistence

Set configuration:

```json
"Persistence": { "Provider": "InMemory" }
```

Run the API:

```bash
dotnet run --project src/CartonCaps.Referrals.Api
```

---

## Running with SQL Server Persistence

Set configuration:

```json
"Persistence": { "Provider": "SqlServer" }
```

```json
"ConnectionStrings": {
  "ReferralsDb": "Server=(localdb)\\mssqllocaldb;Database=CartonCaps.Referrals;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
}
```

Create and apply migrations:

```bash
dotnet ef migrations add InitialCreateSqlServer \
  --project src/CartonCaps.Referrals.Infrastructure \
  --startup-project src/CartonCaps.Referrals.Api \
  --output-dir Persistence/Ef/Migrations
```

```bash
dotnet ef database update \
  --project src/CartonCaps.Referrals.Infrastructure \
  --startup-project src/CartonCaps.Referrals.Api
```

---

## API Endpoints (High-Level)

**Base route:** `/v1/referrals`

### Create Referral
```
POST /v1/referrals
```

Headers:
- `X-User-Id`
- `X-Referral-Code`

Body:
- `contactType`
- `contactValue`
- `channel`

Creates a referral and generates a shareable referral link.

---

### List Referrals
```
GET /v1/referrals?status={optional}&skip={int}&take={int}
```

Returns a paged list of referrals for the current user.

---

### Get Referral by Id
```
GET /v1/referrals/{id}
```

Returns a referral if it belongs to the current user.

---

### Resolve Referral Token
```
GET /v1/referrals/resolve?token=...
```

Used by the client app after install or first launch to resolve a referral token.

---

### Track Referral Events
```
POST /v1/referrals/{id}/events
```

Body:
- `eventType` (`Opened`, `Installed`, `Registered`, etc.)

---

## Testing

### Unit Tests

- Written using the **AAA pattern** (Arrange, Act, Assert)
- Covers:
  - In-memory repository
  - EF repository (via EF InMemory provider)
  - `ReferralService` behavior

Run all tests:

```bash
dotnet test ./CartonCaps.sln
```

---

## Solution Structure

```
src/
 ├─ CartonCaps.Referrals.Domain
 ├─ CartonCaps.Referrals.Application
 ├─ CartonCaps.Referrals.Infrastructure
 └─ CartonCaps.Referrals.Api

tests/
 └─ CartonCaps.Referrals.Tests
```
