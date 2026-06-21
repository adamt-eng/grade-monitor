## :mortar_board: Grade Monitor For Faculty of Engineering - Ain Shams University

An application that automates the retrieval of grades from the [ASU-ENG faculty portal](https://eng.asu.edu.eg/login) and sends them directly to the user via Discord. Supports semester selection, robust retry logic for server downtimes, and session management for faster grade access.

## :zap: API-Based (v2)

This version talks directly to the faculty's official mobile JSON API (`https://eng.asu.edu.eg/api`) — the same backend the ASU-ENG mobile app uses — instead of logging in through a browser and scraping HTML pages. The improvements over the previous scraping approach:

- **No CAPTCHA:** the API login endpoint takes only your student ID and password and returns a JWT access token. There is no image CAPTCHA to solve, so logins are instant and reliable.
- **~3.5× faster:** a full grade refresh takes about **2 seconds** instead of **~7 seconds** (and the old number assumed the CAPTCHA was already solved).
- **Far less code:** the migration removed Selenium, the CAPTCHA solver, the HTML parsers, and the per-course page fetches — a net reduction of **~830 lines (~42% of the codebase)**.
- **One request for all grades:** every course's grade breakdown comes back in a single `my_courses` call rather than one HTTP request per course page.

## :toolbox: Used Technologies

- **.NET 10 (C#):** Core framework for building and running the application logic.
- **Faculty Mobile JSON API:** The official `eng.asu.edu.eg/api` endpoints (login, `my_courses`, `my_results`, `my_details`) that power the faculty mobile app.
- **HttpClient + System.Text.Json (System.Net):** Performs the login and grade-fetching flow as JSON requests and parses the responses.
- **JWT Bearer Auth:** The access token returned by the login endpoint authorizes every subsequent request.
- **Json.NET (Newtonsoft.Json):** Handles the JSON config file.
- **Discord.Net:** A C# wrapper for the Discord API, enabling bot interaction, slash commands, and message handling.

---

## 📸 Showcase

![Showcase](Showcase.gif)

---

## :sparkles: Features

### :closed_lock_with_key: Login

Use the `/get-grades` command to log in and retrieve your grades for the first time. You’ll need to provide your **Student ID** and **Password**.

**Example:**

```
/get-grades student-id:23P0001 password:tHiSiSmYpAsSwOrD
```

---

### :books: Semester Selection

Choose which semester’s grades to view using the dropdown menu. The current term shows live, in-progress grades (midterm, activities, etc.) from `my_courses`, while past semesters show their final letter grades from `my_results`. By default, the current term is selected.

---

### :gear: Mode Selection

Choose between two grade-fetching modes for the current term:

- **Mode 1 – Final Grades:**
  Shows only the final course grade once it has been released.

- **Mode 2 – All Grades (Default):**
  Shows the full grade breakdown such as midterm, activities, practical, etc.

---

### :arrows_counterclockwise: Manual Refresh

- **Refresh Grades Button:**
  Manually refresh the grade data for the selected semester and mode. Because `my_courses` is always live, this always reflects the latest data with no caching to clear.

---

### :stopwatch: Custom Update Intervals

Adjust how often the app checks for grade updates using the `/update-interval` command. You can configure:

- `normal-interval`: Time (in minutes) between checks under normal conditions.
- `interval-after-errors`: Time (in minutes) between checks when an error occurs, this is recommended to be lower than `normal-interval` to allow the app to retry more frequently until recovery.

**Example:**

```
/update-interval normal-interval:60 interval-after-errors:5
```

---

## :gear: Technical Features

* **:key: Login via JSON API:**
  Logs in by POSTing the student ID and password to the faculty's API login endpoint, which returns a JWT access token — no browser automation and no CAPTCHA.

* **:ticket: Token Persistence:**
  The access token is stored and reused across refreshes. When it expires (the API returns `401`), the app transparently logs in again to obtain a fresh token.

* **:repeat: Robust Retry System:**
  Automatically switches to a shorter retry interval during faculty site downtime, so grades are retrieved as soon as the site comes back online.

> :warning: **Note:**
> The app must remain running to monitor grades. Consider using a [VPS](https://cloud.google.com/learn/what-is-a-virtual-private-server) for 24/7 uptime or simply run it locally as needed.

---

## :wrench: Configuration

- The config file `config.json` stores user credentials and application settings. **Do not edit manually.**
- Each user entry holds the **Student ID**, **Password**, and an auto-managed **AccessToken**. The token is refreshed automatically; you never need to touch it.
- If you change your password on the faculty site, update it in the application by re-running the `/get-grades` command with the new password.

---

## :rocket: Setup Instructions

### :one: Create a Discord Bot

- Visit the [Discord Developer Portal](https://discord.com/developers/applications).
- Create a new application and enable the following scopes:

  - `bot`
  - `applications.commands`
- Copy your **Bot Token**.
- Invite the bot to your server using the OAuth2 URL.

---

### :two: Prerequisites

Make sure you have [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed.

To verify installation:

```bash
dotnet --version
```

---

### :three: Clone the Repository

```bash
git clone https://github.com/adamt-eng/grade-monitor
```

---

### :four: Navigate to the Project Directory

```bash
cd grade-monitor
```

---

### :five: Restore and Build the Project

```bash
dotnet restore
dotnet build --configuration Release
```

---

### :six: Run the Application

```bash
dotnet run
```
