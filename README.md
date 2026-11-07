# Revathryn

**Repository intelligence. Deeper understanding. Better software.**

Revathryn is an extensible repository intelligence and code analysis platform designed to help developers understand, inspect and maintain software projects.

It aims to go beyond simple file scanning or isolated static analysis by providing a structured approach to repository discovery, code analysis, issue identification and operational monitoring.

The goal is to build a platform that can progressively understand a software repository, identify potential problems and provide a manageable environment for reviewing and resolving findings.

---

## Vision

Software repositories are constantly evolving. Understanding their structure, identifying potential problems and maintaining visibility over their quality can become increasingly difficult as projects grow.

Revathryn aims to simplify this process through a combination of:

- **Repository intelligence** – Discovering and understanding repository structures and their contents.
- **Incremental discovery** – Identifying changes efficiently rather than repeatedly processing everything.
- **Code analysis** – Applying specialised analysers to identify potential issues and areas of concern.
- **Issue management** – Consolidating analysis outcomes into actionable findings.
- **Administration and visibility** – Providing a centralised interface for managing repositories, analysis and platform configuration.
- **Operational resilience** – Ensuring reliable execution through recovery, retries, monitoring and fault handling.

The underlying philosophy is straightforward:

> Understand the repository, analyse what matters, and make the results manageable.

---

## Core Capabilities

Revathryn is built around several complementary, largely implemented capabilities.

### Repository Management

Repository discovery and management form the foundation of the platform.

Implemented capabilities include:

- Registering and managing repositories, including their local paths and operational state.
- Repository scanning with configurable options, progress tracking and persisted scan records.
- File identification and content fingerprinting for efficient change detection.
- Manifest-based incremental scanning, so only new or modified files are reprocessed.
- Git-ignore and glob matching to focus scanning on relevant files.
- Project discovery within repositories.
- Maintaining a structured understanding of repository contents, files and projects.

### Code Analysis

A modular analysis architecture allows specialised analysers to inspect repository contents.

Implemented capabilities include:

- A Roslyn-based C# analyser that maps code symbols such as methods, properties, fields, indexers, operators and constructors into a structured model.
- Symbol indexing and querying for navigating code structure.
- Architecture analysis summarising repository structure.
- AI-assisted review engines that apply configured prompts to repository contents (currently via OpenAI-compatible APIs).
- An extensible analyser and analysis-engine contract so additional languages can be introduced over time.

### Review and Prompt Pipeline

Reviews orchestrate analysis and AI-driven inspection into repeatable workflows.

Implemented capabilities include:

- Per-repository review types with seeded defaults.
- Scheduled and queued review processing driven by a dedicated background worker.
- Prompt management with immutable versioning and statistics.
- Prompt optimisation and prompt test-run workflows, each with its own background worker.
- Token usage tracking for AI review operations.

### Issue Management

Analysis results are consolidated into actionable findings.

Implemented capabilities include:

- Findings persisted with category, severity and status.
- Reconciliation of analysis outcomes so findings reflect the current state of the code.
- Browsing and inspecting findings through the web interface.
- Issue validation workflows that re-check findings against repository context, driven by a background worker.
- Duplicate detection to consolidate equivalent findings.
- Configurable severity rules that override how findings are classified.

### GitHub Integration

A GitHub webhook integration lets Revathryn react to repository events.

Implemented capabilities include:

- A webhook endpoint with request signature validation.
- Support for a broad range of GitHub events, including push, pull request, issues, comments, workflow runs, deployments and more.
- A queued webhook processing pipeline.

### Administration and Dashboard

A central web interface provides visibility and control over the platform.

Implemented capabilities include:

- An operational dashboard with live status of repositories, findings and processing.
- Repository management.
- Review engine and AI engine configuration.
- Review type and severity rule management.
- AI review usage and token tracking.
- Issue validation review.
- System health and uptime monitoring.

### Reliability and Recovery

Reliable operation is a fundamental part of the architecture.

Implemented capabilities include:

- Retry handling with exponential backoff.
- Recovery from interrupted processing on startup.
- A processing gate with pause and revalidation.
- Health checks and readiness reporting.
- Operational diagnostics and logging.
- Resilient background processing through hosted worker services.

---

## Architecture and Design Principles

Revathryn is being developed with the following principles in mind:

**Modularity**

Individual capabilities should remain independently understandable and maintainable, with clear boundaries between components.

**Extensibility**

New analysers, repository types and processing capabilities should be introduced without requiring fundamental architectural changes.

**Incremental Processing**

Avoid unnecessary work by identifying what has changed and processing only what requires attention.

**Separation of Concerns**

Repository discovery, analysis execution, issue management, administration and operational monitoring should have clearly defined responsibilities.

**Operational Visibility**

The platform should make its own operation understandable, providing sufficient information to diagnose problems and manage its behaviour.

**Pragmatic Persistence**

Persist meaningful outcomes and information rather than creating unnecessary operational history.

---

## Technology

The platform is implemented in C# on .NET 10.

- ASP.NET Core MVC web application with Razor views.
- SQLite persistence via Microsoft.Data.Sqlite.
- Roslyn (Microsoft.CodeAnalysis.CSharp) for C# code analysis and symbol indexing.
- OpenAI-compatible API integration for AI-assisted review.
- PluginManager for extensibility.
- Hosted background services for review, issue validation, duplicate detection, prompt optimisation and prompt test runs.
- MSTest for the unit test suites.

The solution is organised into focused projects:

- **Revathryn** — the ASP.NET Core web application, controllers, views and worker services.
- **Revathryn.Contracts** — interfaces, models and enums shared across the platform.
- **Revathryn.Engine** — core services for scanning, issue management, review orchestration and reliability.
- **Revathryn.Review** — C# analysis and AI review engines.
- **Revathryn.Data** — SQLite persistence implementations.
- **Revathryn.Resources** — user-facing strings.
- **Revathryn.Models** — shared view models.
- Test projects for Contracts, Engine, Review, Data and the web application.

The initial language focus is C#, with the architecture intended to support additional languages and analysis capabilities over time.

---


## Long-term Direction

The longer-term ambition is for Revathryn to become a flexible repository intelligence platform capable of understanding software projects across multiple languages and technologies.

Rather than being tied to a single analyser or development workflow, it should provide a foundation upon which additional analysis capabilities can be built.

The intention is to create something that is useful not just for finding problems, but for making the ongoing understanding and maintenance of software repositories easier.

---

## Contributing

The project is currently under active initial development.

Contribution guidelines and development documentation will be introduced as the project and its interfaces mature.

---

**Revathryn**  
*Understand more. Analyse intelligently. Build with confidence.*