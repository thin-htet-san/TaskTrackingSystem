# Hugging Face + Supabase Deployment Guide

This repo is configured to run as a single Docker Space on Hugging Face.

## What You Need

- A Hugging Face account
- A Supabase project
- Your Supabase PostgreSQL connection string

## Step By Step

1. Create a Supabase project.
2. Open the database settings and copy the PostgreSQL connection string.
3. Push this repository to GitHub.
4. Create a new Hugging Face Space.
5. Choose `Docker` as the Space SDK.
6. Point the Space at this repository.
7. Make sure the Space uses the root [Dockerfile](./Dockerfile).
8. Add these Space secrets or variables:
   - `ConnectionStrings__DefaultConnection`
   - `JwtSettings__Key`
   - `JwtSettings__Issuer`
   - `JwtSettings__Audience`
   - `PasswordReset__RecoveryCode`
9. Set `ConnectionStrings__DefaultConnection` to your Supabase PostgreSQL connection string.
10. Leave `WebApi__BaseUrl` unset unless you want to override the internal default.
11. Deploy the Space.

## What the Container Does

- Starts the Web API on `127.0.0.1:5001`
- Starts the Web App on `127.0.0.1:5000`
- Uses Nginx on port `7860` as the public entrypoint
- Proxies `/api/*` to the API
- Proxies everything else to the Web App

## Notes

- The API now creates the schema on startup if the database is empty.
- The model has been switched from SQL Server to PostgreSQL-friendly mappings.
- If deployment fails, the first thing to check is the Supabase connection string and whether the database is reachable.

## First Login

1. Open the Hugging Face Space URL.
2. Wait for the container to finish starting.
3. Try the default demo accounts from the repository README.
4. If login works, confirm that dashboard and task pages load normally.
