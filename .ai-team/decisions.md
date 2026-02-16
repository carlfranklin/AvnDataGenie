# Team Decisions

<!-- Canonical decision ledger. Agents read this before starting work. Scribe merges inbox entries here. -->

## 2026-02-16: Roadmap Priority Order and Documentation-First Strategy

**By:** Oracle

**What:** Established recommended execution order for the 7 unchecked roadmap items, with documentation completion and export-to-CSV as the top priorities before architectural features.

**Why:** The codebase has zero test coverage, no provider abstraction for multi-database support, and 66% of files are undocumented. Features like multi-database support and multi-user RBAC require foundational architecture work (provider interfaces, auth middleware) that would be risky to attempt without tests. The recommended order is:

1. **Finish code documentation** (19 files remain — unblocks all contributors)
2. **Export results to CSV/Excel** (low-risk, high-value, contained in SQLResults.razor)
3. **Query favorites/bookmarks** (extends existing AppState + localStorage pattern)
4. **Save SQL SELECT results along with queries** (extends AppState, moderate scope)
5. **Query templates and saved queries** (extends favorites, builds on #3 and #4)
6. **Query performance analytics** (needs timing instrumentation in Generator)
7. **Support for additional database types** (requires SchemaGenerator provider abstraction — largest effort)
8. **Multi-user support with RBAC** (architectural overhaul — deferred until foundation is solid)

This order front-loads low-risk, high-value work and defers features that require architectural changes until the codebase has documentation, tests, and cleaner abstractions.
