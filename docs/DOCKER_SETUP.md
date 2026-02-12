# Docker setup guide (Windows)

This guide walks you through installing Docker and running the Incident Intelligence Platform with it. No prior Docker experience required.

---

## What you’ll install

- **Docker Desktop for Windows** – includes the Docker engine, CLI, and Docker Compose. One installer gives you everything this app needs.

---

## Step 1: Check Windows version and optional features

Docker Desktop on Windows works best with **WSL 2** (Windows Subsystem for Linux 2).

1. **Windows version**
   - Press `Win + R`, type `winver`, press Enter.
   - You need **Windows 10 version 2004+** (build 19041+) or **Windows 11**.
   - You’re on Windows 10/11, so you’re fine.

2. **Enable WSL 2 (if not already)**
   - Open **PowerShell as Administrator** (right‑click Start → Windows Terminal (Admin) or PowerShell (Admin)).
   - Run:
     ```powershell
     wsl --install
     ```
   - If it says WSL is already installed, you can skip. Otherwise, restart when prompted.
   - After restart, PowerShell will often finish the WSL setup; if it asks for a Linux username/password, choose one (you’ll use it only for WSL).

3. **Confirm WSL 2**
   - In PowerShell (no need for Admin):
     ```powershell
     wsl --list --verbose
     ```
   - Your default distro should show **VERSION 2**. If it shows 1, run:
     ```powershell
     wsl --set-default-version 2
     ```

---

## Step 2: Download Docker Desktop

1. Go to: **https://www.docker.com/products/docker-desktop/**
2. Click **“Download for Windows”**.
3. Run the installer (**Docker Desktop Installer.exe**).

---

## Step 3: Install Docker Desktop

1. In the installer:
   - Leave **“Use WSL 2 instead of Hyper-V”** checked (recommended).
   - Optionally add **“Add shortcut to desktop”**.
2. Click **OK** and wait for the install to finish.
3. When it says **“Installation successful”**, choose **“Close and restart”** (or restart manually). Docker Desktop needs a restart to load its drivers.

---

## Step 4: First start and sign-in (optional)

1. After restart, **Docker Desktop** should start (or start it from the Start menu).
2. You may see:
   - **Docker Subscription Service Agreement** – accept if you want to use Docker Desktop.
   - **Sign in** – you can sign in with a Docker Hub account or skip. You don’t need an account to run this app.
3. If it shows a short **tutorial** (“Getting started”), you can close it; we’ll use the command line and this app only.
4. Wait until the **whale icon** in the system tray (bottom‑right) is steady (not animating). That means Docker is running.

---

## Step 5: Verify installation

Open a **new** PowerShell or Command Prompt (so it sees the updated `PATH`).

1. **Docker**
   ```powershell
   docker --version
   ```
   You should see something like: `Docker version 27.x.x, build ...`

2. **Docker Compose** (used by this app)
   ```powershell
   docker compose version
   ```
   You should see something like: `Docker Compose version v2.x.x` (or similar).

3. **Quick test**
   ```powershell
   docker run hello-world
   ```
   You should see a short message from Docker (“Hello from Docker!”). That means the engine is working.

If any of these fail:

- Make sure **Docker Desktop is running** (whale icon in tray).
- Try closing and reopening the terminal.
- If `docker` is not found, restart the computer once after installing Docker Desktop.

---

## Step 6: Run the IncidentBrain app with Docker

All commands below are from the **project root** (the folder that contains `docker-compose.yml` and the `src` folder).

1. **Open a terminal** in the project root, e.g.:
   ```powershell
   cd C:\Code\IncidentBrain
   ```

2. **Optional: set the API URL for the browser**
   - Copy the example env file:
     ```powershell
     copy .env.example .env
     ```
   - Open `.env` and set (so the browser can call the API):
     ```
     NEXT_PUBLIC_API_URL=http://localhost:8080
     ```
   - Save the file.  
   (If you don’t create `.env`, the app may already be configured to use port 8080 in compose; the frontend Dockerfile can pass this at build time.)

3. **Build and start everything**
   ```powershell
   docker compose up --build
   ```
   - First time this will:
     - Download base images (e.g. .NET, Node).
     - Build the API and the Next.js app.
     - Start both containers.
   - You’ll see logs from both the API and the web app. Leave this window open.

   **Ollama**  
   Local dev defaults to Ollama for AI summaries. Start Ollama on your machine; the override points the API at `http://host.docker.internal:11434`. If Ollama is not running, the app falls back to Mock. Use **Settings** to switch provider if desired.

4. **Check that it’s running**
   - **API**: open in browser or run:
     ```powershell
     Invoke-RestMethod -Uri "http://localhost:8080/api/health"
     ```
     You should get a healthy status.
   - **Web**: open **http://localhost:3000** in your browser. The stream starts automatically on the Dashboard; incidents appear as they are created. No button to press.

5. **Stop the app**
   - In the terminal where `docker compose up` is running, press **Ctrl+C**.
   - Then run:
     ```powershell
     docker compose down
     ```
   - This stops and removes the containers. Your code and `.env` are not deleted.

---

## Step 7: Useful Docker commands for this app

| Goal | Command |
|------|--------|
| Start (build if needed) | `docker compose up --build` |
| Start in background (detached) | `docker compose up -d --build` |
| Stop and remove containers | `docker compose down` |
| View running containers | `docker compose ps` |
| View logs (both services) | `docker compose logs -f` |
| View API logs only | `docker compose logs -f api` |
| Rebuild after code changes | `docker compose up --build` (or `docker compose build` then `docker compose up`) |

With Ollama: start Ollama on the host, then the same command (`docker compose up --build`).

---

## Troubleshooting

**“Docker is not running” or “Cannot connect to the Docker daemon”**
- Start **Docker Desktop** from the Start menu and wait until the whale icon is steady.
- Then try your command again.

**Port 8080 or 3000 already in use**
- Stop whatever is using that port (e.g. another instance of the API or Next.js), or change the port in `docker-compose.yml`, e.g.:
  ```yaml
  ports:
    - "9080:8080"   # use 9080 on the host instead of 8080
  ```

**“No such file or directory” or “docker-compose.yml not found”**
- Run the command from the **project root** (the folder that contains `docker-compose.yml`), e.g. `C:\Code\IncidentBrain`.

**Frontend can’t reach the API (e.g. 404 or network error)**
- The browser runs on your machine, so the API URL must be **localhost** and the **host** port (e.g. 8080). In `.env`:  
  `NEXT_PUBLIC_API_URL=http://localhost:8080`  
- Rebuild the web app after changing `.env` (env is baked in at build time):  
  `docker compose up --build`

**Build fails for the API or web**
- Ensure you have no syntax errors and that the repo is complete (see README).
- For the web app, the first build can take a few minutes (npm install + Next.js build).

**Containers start but the app doesn’t work as expected**
- Check logs: `docker compose logs -f`
- Verify health: `Invoke-RestMethod http://localhost:8080/api/health`
- Then follow the [Manual Verification Guide](MANUAL_VERIFICATION_GUIDE.md) for app behavior.

---

## Summary

1. Install WSL 2 if needed (`wsl --install`), then restart.
2. Download and install Docker Desktop for Windows; restart when asked.
3. Start Docker Desktop and verify with `docker --version`, `docker compose version`, and `docker run hello-world`.
4. In the project root: `docker compose up --build`, then open http://localhost:3000 and http://localhost:8080/api/health.
5. Use `docker compose down` to stop.
6. Optional: start Ollama on the host to use it from the API; same command.

After this, you have everything you need to run the Incident Intelligence Platform with Docker.
