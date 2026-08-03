# Coding Standards

## Language and Style
- Use C# nullable reference types.
- Prefer file-scoped namespaces.
- Keep types small and focused.
- Use meaningful names for files, classes, methods, and variables.
- Avoid deeply nested conditionals when a guard clause is clearer.

## Async and Performance
- Prefer `async`/`await` for I/O operations.
- Avoid blocking calls in UI or service code.
- Minimize repeated database queries.
- Use `AsNoTracking()` for read-only queries.
- Keep allocations low in hot paths.

## Error Handling
- Catch exceptions only when you can recover or add useful context.
- Log exceptions with meaningful metadata.
- Return user-friendly messages from UI-facing operations.
- Do not swallow errors silently.

## UI Standards
- Keep Razor markup declarative and simple.
- Move business logic to `*.razor.cs` or dedicated services.
- Prefer reusable components for repeated UI patterns.
- Keep pages responsive and accessible.

## Data Standards
- Define EF Core entity configuration explicitly in `OnModelCreating` when needed.
- Use indexes for common query patterns.
- Keep entity names consistent with the domain language.
- Validate imported data before persistence.

## Module Standards
- Keep import, export, search, and automation features isolated by module.
- Expose functionality through interfaces.
- Register implementations in DI in one place per module.

## Review Checklist
Before considering a change complete:
- Build succeeds.
- Feature works in the app.
- No unnecessary architectural drift.
- No duplicate logic introduced.
- No unhandled edge cases in the new flow.
