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
