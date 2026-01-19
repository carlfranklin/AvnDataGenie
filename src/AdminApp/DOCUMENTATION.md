# AvnDataGenie Admin App - Code Documentation

## Overview
This Blazor Server application provides a configuration interface for AvnDataGenie, an AI-powered natural language to SQL query generator.

## Key Components

### AppState.cs ✅ FULLY COMMENTED
Central state management service that:
- Manages database schema and LLM configuration
- Tracks setup wizard progress
- Persists query history to browser localStorage
- Provides file management for configuration files
- Notifies UI components of state changes

**Key Methods:**
- `Initialize(IJSRuntime)` - Set up browser storage access
- `LoadStateAsync()` - Load query history from localStorage
- `SaveStateAsync()` - Persist query history to localStorage
- `AddQueryToHistoryAsync(string)` - Add query to history with auto-save
- `RefreshFileStatus()` - Update file metadata from disk

### Pages

#### Home.razor
**Purpose:** Database schema generation and loading
**Key Features:**
- Enter SQL Server connection string
- Generate database schema via AvnDataGenie.Generator
- Load/save schema JSON files
- Auto-load schema and config on startup

#### TestRuntime.razor
**Purpose:** Test natural language query generation
**Key Features:**
- Input natural language queries
- Generate SQL using LLM
- Display query history with remove capability
- Auto-execute on page load (if configured)
- Advanced mode for custom schema/config

#### SQLResults.razor
**Purpose:** Execute generated SQL and display results
**Key Features:**
- Auto-execute SQL query on navigation
- Use connection string from AppState
- Display results in tabular format
- Manual re-execution capability

#### Config.razor (not shown)
**Purpose:** Configure LLM metadata and rules
**Key Features:**
- Table configurations
- Join hints
- Required filters
- Business term mappings

### Layout Components

#### MainLayout.razor
**Purpose:** Application shell and state initialization
**Key Features:**
- Initialize AppState with JSRuntime on first render
- Load persisted state from localStorage
- Dispose pattern for cleanup

#### NavMenu.razor
**Purpose:** Navigation sidebar with progress tracking
**Key Features:**
- Setup wizard progress indicator
- File status cards (schema/config)
- Statistics summaries
- Restart/reset functionality
- Conditional navigation based on completion

## State Persistence

### Browser Storage (localStorage)
- **What:** Query history (last 20 queries)
- **When:** Automatically saved when queries are added
- **Key:** `AppState_QueryHistory`
- **Format:** JSON array of strings

### File Storage
- **Schema File:** `database_schema.json` - Database structure
- **Config File:** `llm_config.json` - LLM metadata and rules

## Setup Wizard Flow

1. **Connect** - Enter database connection string
2. **Generate** - Extract database schema
3. **Configure** - Add LLM metadata (optional if loading from file)
4. **Test** - Generate and execute queries

## Key Design Patterns

### Scoped Services
`AppState` is registered as scoped (one instance per SignalR circuit), ensuring state isolation between users.

### Event-Driven UI Updates
Components subscribe to `AppState.OnChange` event for reactive UI updates.

### Async/Await Throughout
All I/O operations (file, database, LLM) use async patterns for responsiveness.

### Fire-and-Forget Saves
Query history saves happen asynchronously without blocking UI.

## Configuration

### appsettings.json
- LLM endpoint configuration
- Model selection
- Timeout and token settings

### Dependency Injection
Services registered in `Program.cs`:
- `AppState` (Scoped)
- `AvnDataGenie.Generator` (from package)

## Browser Compatibility

Requires modern browser with:
- JavaScript enabled
- localStorage support
- WebSocket (SignalR)

## Development Notes

### Debugging
- Console logging throughout AppState operations
- Breakpoint location marked in TestRuntime.razor (line 651)

### Common Issues
1. **Configuration not loading** - Ensure `llm_config.json` exists and is valid
2. **Connection drops** - SignalR disconnects shown in console (app continues working)
3. **State not persisting** - Check browser localStorage permissions

### Future Enhancements
- Persist connection string to localStorage
- Export/import configuration bundles
- Query result caching
- Query performance metrics
