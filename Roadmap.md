# Roadmap

## Completed Phases

### Phase 1 — Infrastructure, Layout, Dashboard, Theming
- App shell and visual foundation
- Layout, navigation, and theme system
- Base dashboard experience

### Phase 2 — Instagram Import Engine
- ZIP upload and validation
- Extraction and HTML detection
- HTML parsing and profile import
- SQLite persistence
- SignalR-based progress updates
- Dashboard stats from imported data

### Phase 3 — Data Explorer
- Followers
- Following
- Pending Requests
- Blocked
- Recent Follow Requests
- Removed Suggestions
- Search
- Filter
- Sort
- Pagination
- Column Selector
- CSV Export
- Responsive UI

### Phase 4 — Dashboard & Analytics
- Statistics cards (Followers, Following, Pending, Blocked, Accounts, Verified, Private, Total Relationships)
- Relationship breakdown chart (CSS-based horizontal bar chart)
- Last import summary with duration
- Import history table
- Real-time auto-refresh via SignalR
- Manual refresh button
- Responsive design

### Phase 5 — Automation Engine
- AutomationJob entity with full lifecycle (Pending, Running, Paused, Retrying, Completed, Failed, Cancelled)
- IAutomationService + AutomationService (enqueue, pause, resume, retry, cancel, stats)
- IAutomationProgressService + AutomationProgressService (SignalR real-time broadcasts)
- BackgroundJobWorker (IHostedService, channel-based queue, concurrent workers, retry logic)
- Automation page with job creation, stats cards, job history table, lifecycle controls
- Real-time auto-refresh via SignalR
- No Instagram communication yet — placeholder execution awaiting Phase 6

## Next Phases

### Phase 6 — Instagram Communication Engine
- Communication workflows
- Message handling
- Status tracking
- Reliable interaction history

### Phase 7 — Pending Request Automation
- Pending request review
- Bulk processing tools
- Safety checks
- Progress feedback

### Phase 8 — Multi Accounts
- Multiple account support
- Account switching
- Account-specific data separation
- Shared management experience

### Phase 9 — Reports & Export
- CSV and structured exports
- Report generation
- Filters by time and relationship type
- Downloadable summaries

### Phase 10 — Production Readiness
- Security hardening
- Performance review
- Logging and observability
- Deployment readiness
- Cleanup of technical debt

## Operating Rule
- Complete and verify one phase before starting the next.
- If a phase changes the schema or core pipeline, update documentation and validate the build before continuing.
