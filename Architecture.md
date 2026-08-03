# Architecture Overview

## Application Type
- Blazor Web App using Interactive Server rendering.
- Structured as a modular single solution.

## Technology Stack
- .NET 10
- Blazor Web App
- Ant Design Blazor
- Entity Framework Core with SQLite
- SignalR for real-time progress updates
- Serilog for logging
- HtmlAgilityPack for HTML parsing

## High-Level Layers

### Presentation Layer
- `Components/Pages` contains Razor pages and code-behind classes.
- `Components/Layout` contains the shared application shell.
- `Components/Shared` contains reusable base components and UI helpers.

### Application Layer
- `Import`, `Export`, `Search`, and `Automation` contain feature modules and orchestration services.
- `Services` contains application services and cross-cutting implementations.
- `Interfaces` defines contracts used through dependency injection.

### Domain Layer
- `Domain/Entities` contains business entities.
- `Domain/Enums` contains domain-level enumerations.
- `Domain/ValueObjects` contains small immutable domain concepts.

### Infrastructure Layer
- `Data` contains the EF Core `ApplicationDbContext` and initialization logic.
- `Infrastructure` contains file, archive, parser, and persistence helpers.
- `Repositories` contains data access abstractions.

## Current Feature Direction
- Phase 2 established the import pipeline for Instagram exports.
- Phase 3 added relationship exploration and data browsing.
- Future phases should extend the same patterns instead of introducing new architectural styles.

## Architectural Principles
- Keep UI thin.
- Keep orchestration in services.
- Keep parsing isolated from persistence.
- Keep progress reporting separate from core import logic.
- Use `DbContextFactory` for scenarios that may outlive a Blazor circuit scope.

## Important Notes
- The app currently uses SQLite for local persistence.
- Database initialization is currently development-oriented and should be replaced or migrated properly before production hardening.
- Runtime warnings that do not block functionality should be tracked separately from feature work.
