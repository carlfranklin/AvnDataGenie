# Avn Data Genie

An OSS system to allow users to query a database using natural language.

## Overview

Jeff Fritz and I had been trying to use the various SQL MCP Servers that exist to give users the ability to generate reports from a text prompt. We found that this approach is flawed in the following ways:

* **Security**
  * Giving an MCP carte-blanche access to your database is a bad idea. No constraints.
* **Performance**
  * The MCPs we saw were gathering metadata on every request
* **Flexibility**
  * Tight-coupling to models and databases

We wanted something we could more easily control. Our answer is **AvnDataGenie**.

* **Generate a Database Schema Beforehand**
  * Rather than give the tool direct access to the database, we generate a JSON file that represents the database schema.

* **Generate Metadata Beforehand**
  * Include a Metadata Management tool so users can:
    * Annotate Tables with descriptions.
    * Provide aliases for tables and columns.
    * Provide constraints such as number of records returned.
    * Select fields that should not ever be displayed. Ex: SSN or other PII
* **Use a local model (Ollama) to generate SQL Queries** (optional)
  * Ollama runs locally and never shares your data structures with Internet models. 
* **Performant architecture**
  * Metadata is loaded in before use.

## Features

### Database Schema Generation
- Connect to any SQL Server database using a connection string
- Automatically extract complete database metadata including:
  - Tables and columns with data types
  - Primary keys
  - Foreign key relationships
  - Indexes
  - Column constraints (nullable, default values, etc.)
- Export schema to JSON format for easy sharing and version control

### LLM Configuration Management
Configure how the LLM interacts with your database through a comprehensive web interface:

#### Tables & Columns Configuration
- **Friendly Names**: Provide user-friendly names for tables and columns (e.g., "Customers" instead of "tbl_cust")
- **Descriptions**: Add detailed descriptions explaining what each table and column represents
- **Aliases**: Define comma-separated alternative names that users might use (e.g., "Cust, Customer, Clients")
- **PII Marking**: Flag columns containing Personally Identifiable Information
- **Restricted Fields**: Mark columns that should never be included in query results
- Collapsible table view for easy navigation of large schemas

#### Join Hints
- **Auto-Generation**: Automatically generate join hints from foreign key relationships
- **Manual Control**: Add, edit, or remove join hints as needed
- **Custom Descriptions**: Provide hints to help the LLM understand complex relationships
- One-click regeneration from foreign keys

#### Required Filters
- Define filters that must always be applied to queries:
  - **Tenant Isolation**: Ensure multi-tenant data separation
  - **Soft Delete**: Automatically exclude deleted records
  - **Custom Filters**: Define any other mandatory filtering rules
- Specify default values for each filter

#### Business Terms
- Define domain-specific terminology (e.g., "Submitted Grants", "Active Customers")
- Provide definitions and examples for each term
- Help the LLM understand your business context and generate more accurate queries

### Natural Language to SQL Generation
- **Test Runtime Interface**: Interactive page for testing SQL generation
- Input natural language queries (e.g., "show me the top 10 selling albums of all time with their sales numbers")
- Generate SQL SELECT statements using configured schema and LLM rules
- Real-time SQL generation with loading indicators
- Syntax-highlighted SQL output with Markdown rendering
- Copy generated SQL to clipboard
- Direct navigation to execute queries
- **Query History**: Automatically saves and displays recent queries
  - Select from previously run queries
  - Remove queries from history
  - Persists across browser sessions using Protected Browser Storage

### SQL Query Execution & Results
- **SQL Results Page**: Execute generated queries against your database
- Display SQL query with syntax highlighting
- Uses connection string saved during schema generation
- Automatic query execution on page load
- Execute button for manual re-execution
- **Smart Results Display**:
  - Responsive table with scrollable results
  - Automatic column name formatting (e.g., "AlbumName" → "Album Name")
  - Row count display
  - Handles empty result sets gracefully
  - Error handling with detailed messages
- Protected navigation - requires both database schema and LLM configuration

### Configuration Persistence
- Save database schema to JSON file (`database_schema.json`)
- Save LLM configuration to JSON file (`llm_config.json`)
- Auto-load schema and configuration on startup
- Export configurations with timestamps
- Protected Browser Storage for query history and application state

## Getting Started

### Prerequisites
- .NET 10.0 SDK or later
- SQL Server database (tested with SQL Server 2019+)
- Windows, Linux, or macOS
- (Optional) Ollama for local LLM support OR Azure OpenAI / OpenAI API key for cloud LLM

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/carlfranklin/AvnDataGenie.git
   cd AvnDataGenie
   ```

2. Configure your LLM provider using User Secrets:
   ```bash
   cd src/AppHost
   dotnet user-secrets init
   ```

   See the **LLM Configuration** section below for detailed setup instructions for Azure OpenAI, OpenAI, or Ollama.

3. Build the solution:
   ```bash
   cd ../
   dotnet build
   ```

4. Run the Admin App:
   ```bash
   cd AdminApp
   dotnet run
   ```

5. Open your browser and navigate to the URL shown in the console (typically `https://localhost:5001`)

## LLM Configuration

AvnDataGenie supports multiple LLM providers. Configure your preferred provider using .NET User Secrets to keep your API keys secure.

### Supported LLM Providers

#### Azure OpenAI
Best for enterprise deployments with dedicated Azure resources.

```bash
cd src/AppHost
dotnet user-secrets set "AvnDataGenie:LlmEndpoint" "https://YOUR-RESOURCE.openai.azure.com/"
dotnet user-secrets set "AvnDataGenie:LlmApiKey" "your-api-key-here"
dotnet user-secrets set "AvnDataGenie:LlmType" "AzureOpenAI"
dotnet user-secrets set "AvnDataGenie:ModelName" "gpt-4"
```

#### OpenAI
Standard OpenAI API for cloud-based inference.

```bash
cd src/AppHost
dotnet user-secrets set "AvnDataGenie:LlmEndpoint" "https://api.openai.com/v1"
dotnet user-secrets set "AvnDataGenie:LlmApiKey" "sk-your-api-key-here"
dotnet user-secrets set "AvnDataGenie:LlmType" "OpenAI"
dotnet user-secrets set "AvnDataGenie:ModelName" "gpt-4"
```

#### Ollama (Local)
**Recommended for development and privacy-sensitive deployments.** Runs entirely on your local machine with no data sent to external services.

1. Install Ollama from [ollama.ai](https://ollama.ai)
2. Pull a model: `ollama pull qwen2.5-coder:1.5b` (or another model)
3. Configure AvnDataGenie:

```bash
cd src/AppHost
dotnet user-secrets set "AvnDataGenie:LlmEndpoint" "http://localhost:11434"
dotnet user-secrets set "AvnDataGenie:LlmApiKey" ""
dotnet user-secrets set "AvnDataGenie:LlmType" "Ollama"
dotnet user-secrets set "AvnDataGenie:ModelName" "qwen2.5-coder:1.5b"
```

**Recommended Ollama Models:**
- `qwen2.5-coder:1.5b` - Fast, small model good for testing
- `qwen2.5-coder:7b` - Better accuracy, requires more resources
- `codellama:7b` - Alternative option for SQL generation

### Configuration Parameters

| Secret Key | Description | Example Values |
|------------|-------------|----------------|
| `AvnDataGenie:LlmEndpoint` | The base URL for your LLM provider | `https://api.openai.com/v1`<br>`http://localhost:11434` |
| `AvnDataGenie:LlmApiKey` | Your API key (empty for Ollama) | `sk-...` (OpenAI)<br>`""` (Ollama) |
| `AvnDataGenie:LlmType` | The LLM provider type | `OpenAI`, `AzureOpenAI`, `Ollama` |
| `AvnDataGenie:ModelName` | The specific model to use | `gpt-4`, `gpt-3.5-turbo`, `qwen2.5-coder:1.5b` |

### Viewing Your Secrets

To view your configured secrets:
```bash
cd src/AppHost
dotnet user-secrets list
```

### Removing Secrets

To clear all secrets:
```bash
cd src/AppHost
dotnet user-secrets clear
```

## Usage Guide

### Step 1: Generate Database Schema

1. On the **Home** page, enter your SQL Server connection string
   - Example: `Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=YourDB;Integrated Security=True`
2. Click **Generate Schema**
3. Wait for the schema extraction to complete
4. The schema is automatically saved to `database_schema.json`
5. Click **Configure Data Genie** to proceed to configuration

### Step 2: Configure LLM Rules

1. Click **Configure Data Genie** to navigate to the configuration page
2. Use the tabs to configure different aspects:

#### Configuring Tables & Columns
1. Click on a table to expand it
2. Enter friendly names and descriptions for the table
3. Add comma-separated aliases (e.g., `Emp, Employee, Worker`)
4. For each column:
   - Add friendly names and descriptions
   - Add aliases for alternative column names
   - Check **PII** if the column contains sensitive personal data
   - Check **Restricted** if the column should never appear in results

#### Configuring Join Hints
1. Click **🔄 Regenerate from Foreign Keys** to auto-generate hints from your schema
2. Review and edit the generated hints
3. Add custom descriptions/hints for complex relationships
4. Click **+ Add Join Hint** to manually add additional join hints
5. Remove unwanted hints with the **×** button

#### Configuring Required Filters
1. Click **+ Add Required Filter**
2. Select the table and column
3. Choose the filter type:
   - **Tenant**: For multi-tenant data isolation
   - **Soft Delete**: For soft delete patterns
   - **Custom**: For any other mandatory filter
4. Specify the default value (e.g., `IS NULL`, `= 0`, `= @TenantId`)
5. Add an optional description

#### Configuring Business Terms
1. Click **+ Add Business Term**
2. Enter the term name (e.g., "Submitted Grants")
3. Provide a definition
4. Optionally add an example query pattern

### Step 3: Save Configuration

1. Click **Save Configuration** to persist your settings
2. Configuration is saved to `llm_config.json`
3. The configuration auto-loads on subsequent visits

### Step 4: Test Natural Language Queries

1. Navigate to **Test Runtime** from the navigation menu
2. Review the loaded database schema and LLM configuration
3. Enter a natural language query in plain English:
   - Example: "Show me the top 10 selling albums of all time with their sales numbers"
   - Example: "Get all customers who placed orders in the last 30 days"
   - Example: "List employees hired after January 2003"
4. Click **Generate SQL**
5. Review the generated SQL query with syntax highlighting
6. Use the **Copy** button to copy SQL to clipboard
7. Click **Execute** to run the query and see results
8. Your query is automatically saved to the history
9. Select from previous queries using the history dropdown
10. Remove queries from history using the **×** button

### Step 5: Execute Queries and View Results

1. After generating SQL in Test Runtime, click **Execute**
2. The SQL Results page automatically executes the query on load
3. View results in a formatted table with:
   - Friendly column headers (e.g., "Album Name" instead of "AlbumName")
   - Row count
   - Scrollable results for large datasets
4. Click **Execute Query** button to manually re-run the query
5. The **SQL Results** link appears in the navigation menu only when both schema and configuration are loaded

### Step 6: Export and Share

- Click **Export JSON** to create a timestamped backup
- Share configuration files with team members
- Version control your JSON files for tracking changes

## File Structure

```
src/
├── AdminApp/                         # Blazor web UI for configuration and testing
│   ├── Components/
│   │   ├── Pages/
│   │   │   ├── Home.razor            # Schema generation page
│   │   │   ├── Home.razor.css        # Scoped styles for Home page
│   │   │   ├── Config.razor          # LLM configuration page
│   │   │   ├── TestRuntime.razor     # Natural language query testing
│   │   │   ├── TestRuntime.razor.css # Scoped styles for Test Runtime page
│   │   │   ├── SQLResults.razor      # Query execution and results display
│   │   │   └── SQLResults.razor.css  # Scoped styles for SQL Results page
│   │   └── Layout/
│   │       ├── MainLayout.razor
│   │       └── NavMenu.razor         # Dynamic navigation menu
│   ├── AppState.cs                   # Application state (schema, config, SQL, history)
│   └── Program.cs
├── AvnDataGenie/                     # Core SQL generation logic
│   ├── Generator.cs                  # Natural language to SQL generator
│   └── SqlPromptBuilder.cs           # LLM prompt construction
├── SchemaGenerator/                  # Database schema extraction
│   ├── Generator.cs                  # Database schema generator
│   └── Models/
│       ├── DatabaseSchema.cs         # Schema models
│       ├── TableSchema.cs
│       ├── ColumnSchema.cs
│       ├── ForeignKeySchema.cs
│       └── LlmConfiguration.cs       # Configuration models
├── AppHost/                          # .NET Aspire app host for orchestration
└── ServiceDefaults/                  # Shared service configuration
```

## Configuration File Format

### database_schema.json
Contains the complete database structure:
```json
{
  "databaseName": "YourDB",
  "serverName": "localhost",
  "generatedAt": "2026-01-11T20:00:00Z",
  "tables": [
    {
      "schemaName": "dbo",
      "tableName": "Customer",
      "columns": [...],
      "primaryKey": {...},
      "foreignKeys": [...],
      "indexes": [...]
    }
  ]
}
```

### llm_config.json
Contains LLM interaction rules:
```json
{
  "tableConfigurations": [...],
  "joinHints": [...],
  "requiredFilters": [...],
  "businessTerms": [...]
}
```

## Roadmap

- [x] Database schema generation from SQL Server
- [x] LLM configuration management (tables, columns, joins, filters, business terms)
- [x] Natural language to SQL generation
- [x] Query execution interface with auto-execution
- [x] Query result visualization with formatted display
- [x] Integration with Ollama for local LLM support
- [x] Query history with persistence across sessions
- [x] Comprehensive code documentation and comments
- [x] Scoped CSS for all Razor components
- [ ] Query favorites/bookmarks
- [ ] Save SQL SELECT results along with queries
- [ ] Support for additional database types (PostgreSQL, MySQL)
- [ ] Multi-user support with role-based access
- [ ] Query templates and saved queries
- [ ] Export results to CSV/Excel
- [ ] Query performance analytics

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with .NET Aspire and Blazor
- Inspired by the need for secure, performant natural language database querying

