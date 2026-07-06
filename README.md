---
title: TaskTrackingSystem
colorFrom: blue
colorTo: green
sdk: docker
app_port: 7860
---

# TaskTrackingSystem

This repo runs the Web API and Web App together inside one Docker container for Hugging Face Spaces.

## Hosting Setup

- Database: Supabase PostgreSQL
- Public app: Hugging Face Space
- API and Web App: same container, routed through Nginx

## Local Notes

- The app now expects PostgreSQL instead of SQL Server.
- The API creates the schema on first startup with `EnsureCreatedAsync()`.
- The Web App talks to the API at `http://127.0.0.1:5001/api/` by default inside the container.

## Default Demo Accounts

- `admin` / `P@ssw0rd123!`
- `mgr01` / `P@ssw0rd123!`
- `emp01` / `P@ssw0rd123!`
