# Mentora Project Memory

Date: 2026-04-01

## Current AI Setup

- The backend AI helper is Groq-only.
- The Groq key is loaded from `api-keys.json`.
- In production, the deployed server layout uses `api-keys.json` beside the jar, and `GroqAI` checks:
  - `api-keys.json`
  - `java-server/Java-Server/api-keys.json` as a fallback
- The in-game AI help flow includes the child learning/profile context built from `Child.gameStats`.
- The existing AI profile was already being built and displayed in the Android app; this work connected that profile into the gameplay AI-help prompt.

## Web Creator Platform

- The creator platform is for authors/parents to log in and create quiz-based courses.
- MVP scope implemented:
  - login/register for creator accounts
  - course CRUD
  - quiz question authoring
  - publish/unpublish courses
  - published course listing for the Unity side
  - published course detail fetch for the Unity side
  - child course completion submission
- The course model currently supports quiz courses only.

## Frontend / Backend Split

- The Java backend is API-only.
- The embedded Spring static site was removed.
- The separate frontend project lives in:
  - `web-creator/`
- The local frontend is intended to run with Vite:
  - `npm install`
  - `npm run dev`
- The local frontend talks directly to the VPS backend.

## Local Frontend Configuration

- The separate frontend uses `VITE_API_BASE`.
- Default intended backend base:
  - `https://neuro.serenityutils.club`
- Local frontend default dev URL:
  - `http://localhost:5173`
- Example env file:
  - `web-creator/.env.example`

## Backend API

- Spring Boot is used for the HTTP API only.
- Web API endpoints are under:
  - `/api/web/auth/...`
  - `/api/web/courses/...`
- Root `/` should behave like an API-only backend, not a website.
- Whitelabel fallback was disabled.

## CORS

- CORS was added because the local frontend runs on a different origin than the VPS backend.
- Allowed local dev origins:
  - `http://localhost:5173`
  - `http://127.0.0.1:5173`
  - `http://localhost:3000`
  - `http://127.0.0.1:3000`
- CORS applies to `/api/web/**`.

## Ports and Hosting Decisions

- The custom game socket server remains separate from Spring Boot.
- Existing game socket port:
  - `49154`
- Spring Boot HTTP API was explicitly moved off `8080`.
- Current intended Spring Boot bind:
  - `server.address=127.0.0.1`
  - `server.port=8085`
- This means the API should listen on:
  - `127.0.0.1:8085`
- The API is intended to be reached publicly only through Nginx / Cloudflare Tunnel, not by exposing port `8085` directly.

## VPS / Reverse Proxy Plan

- The VPS already uses Cloudflare Tunnel and Nginx.
- The intended setup is:
  - local-only Spring Boot API on `127.0.0.1:8085`
  - Nginx proxies `/api/web/` to `http://127.0.0.1:8085`
  - the custom WebSocket/game socket remains on `49154`
- The frontend running locally on the developer machine should call the VPS domain, not localhost on the VPS.

## Unity Integration

- Unity UI for creator courses was not built here.
- Packet support was added so Unity can later:
  - fetch published courses
  - fetch full course details
  - submit course completion
- Packet IDs added for course flow:
  - `36` fetch published courses
  - `37` published courses response
  - `38` fetch course detail
  - `39` course detail response
  - `40` submit course completion

## Deployment Notes

- For local dev, the frontend should be edited/run from `web-creator/`.
- The backend should be deployed on the VPS with the Spring HTTP API on `127.0.0.1:8085`.
- Nginx should proxy the public API route to that local Spring port.
- The backend requires PostgreSQL to be running and reachable before Spring will boot successfully.

