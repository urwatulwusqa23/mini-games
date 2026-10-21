# Real-Time Multiplayer Connect 4

A browser-based Connect 4 game for two players with live spectating, built with
ASP.NET Core + SignalR on the backend and Angular 17 (standalone, signals) on
the frontend.

## Project structure

```
backend/
  Connect4.Engine/        Pure game logic (no SignalR dependency)
  Connect4.Engine.Tests/  xUnit tests for the engine
  Connect4.Server/        ASP.NET Core Web API + SignalR hub, game state manager
frontend/
  src/app/core            Models, GameService (SignalR client), ThemeService
  src/app/pages/lobby      Create / join game screen
  src/app/pages/game       The game board screen
  src/app/shared           Reusable BoardComponent
```

## Architecture

- **GameEngine** (`Connect4.Engine`) is pure C#: a `ConnectFourBoard` class that
  validates moves, enforces turn order, and detects wins (all 4 directions) and
  draws. It has no knowledge of SignalR, networking, or storage.
- **GameManager** (`Connect4.Server/Game/GameManager.cs`) is an in-memory,
  thread-safe store of active `GameSession`s, keyed by a 6-character room code.
- **GameHub** (`Connect4.Server/Game/GameHub.cs`) is the SignalR transport
  layer. It never implements game rules itself — it delegates every move to
  `ConnectFourBoard.DropPiece(...)` and broadcasts the resulting state. Each
  game's mutations are serialized with a `SemaphoreSlim` so concurrent moves
  from both players can't race.
- The client only ever sends **intents** (`DropPiece`, `RequestRematch`, ...)
  and renders whatever state the server broadcasts — it never computes a win
  or validates a move locally.

## Prerequisites

- .NET 8 SDK
- Node.js 18+ and npm
- Angular CLI 17 (the frontend uses `npx ng ...`, so a global install isn't required)

## Running the backend

```bash
cd backend/Connect4.Server
dotnet run
```

The API/SignalR hub listens on `http://localhost:5055` (see
`Properties/launchSettings.json`). The hub endpoint is `/hubs/game`.

To run the engine's unit tests:

```bash
cd backend
dotnet test
```

## Running the frontend

```bash
cd frontend
npm install
npx ng serve --port 4201
```

Open `http://localhost:4201` in two browser windows (or one normal + one
private/incognito window, since the room token is stored in `localStorage`
per-origin) to play as both Red and Yellow.

If you change the backend port, update `src/environments/environment.ts`
(`hubUrl`) and the CORS origin list in `backend/Connect4.Server/Program.cs`.

## How to play

1. Enter a display name and click **Create game** — you become Red and get a
   shareable room code / link.
2. Share the room code or the `/game/:code` link with a friend; they enter a
   name and join as Yellow. A third person who joins becomes a spectator.
3. Click a column to drop a piece. Only the player whose turn it is can move;
   the server rejects anything else.
4. Four in a row (horizontal, vertical, or either diagonal) ends the game and
   highlights the winning cells; a full board with no winner is a draw.
5. After the game ends, both players can request a rematch — once both agree,
   the board resets and the loser of the previous game goes first.

## Reconnection

Each seat is tied to a random token stored in the browser's `localStorage`. If
a player's connection drops, their opponent sees a "Reconnecting" status and
the seat is held open for 60 seconds; reloading the page within that window
restores the same seat. If the window elapses without reconnecting, the
remaining player is awarded the win.

## Notes

- There's no authentication in v1 — anyone with the room code/link can join as
  a player (if a seat is open) or a spectator.
- Game state lives in memory only (`ConcurrentDictionary`); restarting the
  backend clears all active games. Match history / persistence was left out of
  scope for v1, as it's optional per the spec.

## Deploying for free (Render + Vercel)

The backend needs a real persistent process (SignalR holds WebSocket
connections), so it can't run on a serverless platform — but it fits
comfortably on **Render's free web service tier**. The frontend is a static
build, so it deploys to **Vercel** (or Netlify/Cloudflare Pages).

**Trade-off of the free tier:** Render's free services spin down after ~15
minutes of inactivity. The next request wakes it back up (taking maybe
30-60s), and because game state is in-memory, any games in progress during a
spin-down are lost. Fine for a demo/portfolio piece, not for real users.

### 1. Backend → Render

1. Push this repo to GitHub.
2. In the [Render dashboard](https://dashboard.render.com), click **New +** →
   **Blueprint**, and point it at your repo — it will pick up
   [`render.yaml`](render.yaml) at the repo root automatically.
3. Render builds `backend/Connect4.Server/Dockerfile` and deploys it. Note the
   resulting URL, e.g. `https://connect4-backend.onrender.com`.
4. In the service's **Environment** tab, set `Cors__AllowedOrigins__0` to your
   Vercel frontend's URL once you have it (step 2 below) — e.g.
   `https://connect4-frontend.vercel.app` (no trailing slash) — and save
   (this triggers a redeploy).

If you'd rather deploy manually instead of via Blueprint: create a new **Web
Service**, set **Root Directory** to `backend`, **Dockerfile Path** to
`Connect4.Server/Dockerfile`, and **Docker Build Context** to `.` (the
`backend` folder).

### 2. Frontend → Vercel

1. Before deploying, edit
   [`frontend/src/environments/environment.prod.ts`](frontend/src/environments/environment.prod.ts)
   and replace `hubUrl` with your Render backend's hub URL, e.g.
   `https://connect4-backend.onrender.com/hubs/game`. Commit that change.
2. In [Vercel](https://vercel.com), **Add New** → **Project**, import the repo,
   and set **Root Directory** to `frontend`. Vercel will pick up
   [`vercel.json`](frontend/vercel.json) for the build command, output
   directory, and the SPA rewrite rule (needed so `/game/:code` links don't
   404 on refresh).
3. Deploy. Note the resulting URL and go back to step 1.4 above to finish
   wiring up CORS on the backend.

### 3. Verify

Open the Vercel URL, create a game, and open the `/game/:code` link in a
second (private/incognito) browser window to confirm the real-time connection
works end-to-end. If the lobby shows "Could not create a game," check the
browser console for a CORS error — it almost always means step 1.4 (the
`Cors__AllowedOrigins__0` env var) wasn't set to the exact frontend origin.
