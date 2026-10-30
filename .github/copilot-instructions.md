# Project Instructions

* Use C# 13 features where appropriate.
* Prefer async/await over blocking calls.
* Use MSTest for unit tests.
* Follow existing dependency injection patterns.
* Do NOT use nullable reference types.
* Add XML documentation to public APIs.
* Avoid introducing new third-party packages unless requested.
* All date/times should be UTC
* Any classes/enums etc required by Abstractions should be placed within ThemisForge.Kernel
* ONLY interface allowed within ThermisForge.Abstractions

# Code Instructions

* One comment one line
* Braces on new lines
* Do NOT use var, use explicit type instead
* Class layout is important:

  * Fields
  * Constructors
  * Properties
  * Public Methods
  * Protected Methods
  * Private Methods
* Once Class per file unless a private class
* Interfaces belong in BThemisForge.Abstractions
* Use simplified object initializers where possible (i.e. ConcreteType n = new() { Property = value };)
* If an expression value is not used, prefix with "\_ =" which shows intent to ignore the value
* Use pattern matching where possible (i.e. if (order.State is not ProductionOrderState.InProgress and not ProductionOrderState.Queued))
* Use null propergation where possible (i.e. string? name = order.Customer?.Name;)
* No magic strings, use constants or enums where possible
* No magic numbers, use constants or enums where possible

## .cshtml files
* All models should have the full namespace in the @model directive

# Unit Tests

* Add ExcludeFromCodeCoverage attribute to all test classes so they are not counted in code coverage
* Use Assert.HasCount instead of Assert.AreEqual for collection counts

## Unit tests should cover:

- Happy path
- Boundary conditions
- Null inputs
- Empty collections
- Invalid inputs
- Exception paths

# Build

* Always ask before building

# General

All timestamps are UTC

