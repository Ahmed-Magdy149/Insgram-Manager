# Project Rules

## Purpose
These rules define how Insgram Manager should be developed phase by phase.

## Core Execution Rules
- Work on **one phase at a time**.
- Do **not** start the next phase until the current phase is complete and verified.
- Keep changes **small, traceable, and reversible**.
- Preserve existing behavior unless the phase explicitly requires a change.
- Prefer **production-ready** implementations over quick fixes.
- Do not place business logic inside Razor components.
- Use code-behind classes and services for UI logic and application logic.

## Quality Rules
- Follow SOLID principles.
- Avoid duplication.
- Keep code readable, modular, and testable.
- Prefer asynchronous APIs where applicable.
- Handle errors explicitly and log them clearly.
- Use dependency injection for services and infrastructure.

## Data Rules
- Use EF Core for persistence.
- Update the schema intentionally and keep migrations aligned with the code.
- Avoid hardcoded paths and file names.
- Validate all user input and uploaded files.
- Keep import/export pipelines resilient to malformed data.

## UI Rules
- Use responsive layouts.
- Keep pages simple and focused.
- Use reusable components when possible.
- Keep UI state in the page code-behind or a service, not inline in markup.

## Verification Rules
- Build the project after meaningful changes.
- Fix compile errors before moving on.
- Resolve runtime issues before marking a phase complete.
- If a phase introduces warnings, document whether they are pre-existing or new.

## Phase Completion Criteria
A phase is complete only when:
- The requested feature is implemented.
- The project builds successfully.
- The feature is visible or usable in the app.
- The implementation is consistent with the current architecture.

## Current Status
- Phase 1: Complete
- Phase 2: Complete
- Phase 3: Complete
- Phase 4: Complete
- Next phase: Phase 5
