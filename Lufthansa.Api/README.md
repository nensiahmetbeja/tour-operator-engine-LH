# Lufthansa .NET Assessment – Data Import Engine

This repository implements a tour operator pricing import and query engine for the Lufthansa .NET Assessment. It features CSV uploads, JWT authentication, PostgreSQL + EF Core, Redis caching, and SignalR real-time progress updates. Structured logging is enabled via the built-in ILogger (Serilog-compatible).

## Tech Stack

- .NET 9.0, ASP.NET Core
- EF Core, PostgreSQL
- Redis (IDistributedCache)
- SignalR (real-time progress)
- CsvHelper (CSV parsing)
- Swagger/OpenAPI
- Structured logging (ILogger; works with Serilog sinks)

## Prerequisites

- .NET 9.0 SDK
- Docker Desktop (for PostgreSQL and Redis)
- Optional: a DB client (e.g., DBeaver) for inspection
- A browser for UI testing

## Quick Start

1) Clone and restore
