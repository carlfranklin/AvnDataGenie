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

### Configuration Persistence
- Save configuration to JSON file
- Load existing configurations
- Export configurations with timestamps
- Auto-load last configuration on startup

## Getting Started

### Prerequisites
- .NET 10.0 SDK or later
- SQL Server database (tested with SQL Server 2019+)
- Windows, Linux, or macOS

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/AvnDataGenie.git
   cd AvnDataGenie
   ```

2. Build the solution:
   ```bash
   cd src
   dotnet build
   ```

3. Run the Admin App:
   ```bash
   cd AdminApp
   dotnet run
   ```

4. Open your browser and navigate to the URL shown in the console (typically `https://localhost:5001`)

## Usage Guide

### Step 1: Generate Database Schema

1. On the **Home** page, enter your SQL Server connection string
   - Example: `Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=YourDB;Integrated Security=True`
2. Click **Generate Schema**
3. Wait for the schema extraction to complete
4. The schema is automatically saved to `database_schema.json`

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

### Step 4: Export and Share

- Click **Export JSON** to create a timestamped backup
- Share configuration files with team members
- Version control your JSON files for tracking changes

## File Structure

```
src/
├── AdminApp/               # Blazor web UI for configuration
│   ├── Components/
│   │   └── Pages/
│   │       ├── Home.razor        # Schema generation page
│   │       └── Config.razor      # Configuration page
│   ├── AppState.cs              # Application state management
│   └── Program.cs
├── QueryGenerator/        # Core schema extraction logic
│   ├── Generator.cs             # Database schema generator
│   └── Models/
│       ├── DatabaseSchema.cs    # Schema models
│       ├── TableSchema.cs
│       ├── ColumnSchema.cs
│       ├── ForeignKeySchema.cs
│       └── LlmConfiguration.cs  # Configuration models
└── ServiceDefaults/       # Shared service configuration
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

- [ ] Query execution interface
- [ ] Integration with Ollama for local LLM support
- [ ] Query result visualization
- [ ] Query history and favorites
- [ ] Support for additional database types (PostgreSQL, MySQL)
- [ ] Multi-user support with role-based access
- [ ] Query templates and saved queries

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with .NET Aspire and Blazor
- Inspired by the need for secure, performant natural language database querying

