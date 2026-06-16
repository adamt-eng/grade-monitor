## :mortar_board: Grade Monitor For Faculty of Engineering - Ain Shams University

An application that automates the retrieval of grades from the [ASU-ENG faculty portal](https://eng.asu.edu.eg/login) and sends them directly to the user via Discord. Supports semester selection, robust retry logic for server downtimes, and session management for faster grade access.

## :toolbox: Used Technologies

- **.NET 10 (C#):** Core framework for building and running the application logic.
- **HttpClient (System.Net):** Performs the entire login and grade-fetching flow over plain HTTP requests.
- **SixLabors.ImageSharp:** Cleans up the faculty's image CAPTCHA (isolates the digits from the noise) before recognition.
- **ocr.space API:** Reads the digits out of the image CAPTCHA shown on the login page.
- **Json.NET (Newtonsoft.Json):** Handles JSON config files and API responses.
- **CookieContainer (System.Net):** Stores and manages session cookies across HTTP requests.
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

Choose which semester’s grades to view using the dropdown menu. By default, the most recent semester is selected.

---

### :gear: Mode Selection

Choose between two grade-fetching modes to suit your needs:

- **Mode 1 – Final Grades:**
  Fetches only final grades to reduce HTTP requests. Useful during peak times when the faculty servers are already under heavy load.

- **Mode 2 – All Grades (Default):**
  Retrieves all course grades such as final, midterm, activities, etc.

---

### :arrows_counterclockwise: Manual Refresh Options

- **Refresh Grades Button:**
  Manually refresh the grade data for the selected semester and mode.

- **Refresh Courses Button:**
  Force-refresh your course list. Useful if you’ve recently registered, dropped, or withdrawn from courses.

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

* **:key: Login via HTTP:**
  Logs in by talking to the faculty portal directly over HTTP — no browser automation required.

* **:robot: CAPTCHA Solver Integration:**
  Reads the faculty's image CAPTCHA automatically: the digits are isolated from the background noise with [ImageSharp](https://github.com/SixLabors/ImageSharp) and recognized via [ocr.space](https://ocr.space). Because new CAPTCHAs are free to fetch, the app keeps trying fresh ones until it gets a confident read before submitting. (The previous reCAPTCHA + [SolveCaptcha](https://solvecaptcha.com) solver is kept in the codebase in case the faculty switches back.)

* **:cookie: Session Persistence:**
  Uses `CookieContainer` to store session cookies and reduce the need for repeated logins.

* **:repeat: Robust Retry System:**
  Automatically switches to a shorter retry interval during faculty site downtime, so grades are retrieved as soon as the site comes back online.

> :warning: **Note:**
> The app must remain running to monitor grades. Consider using a [VPS](https://cloud.google.com/learn/what-is-a-virtual-private-server) for 24/7 uptime or simply run it locally as needed.

---

## :wrench: Configuration

- The config file `config.json` stores user credentials and application settings. **Do not edit manually.**
- The `Laravel_Session` value in `config.json` is no longer modifiable via command. Previously, users could log in by directly providing the session cookie, but this is no longer supported due to the expiration date now embedded in the cookie. Instead, the login process uses the student ID and password via Selenium to retrieve and store a valid session token.
- If you change your password on the faculty site, you can update it in the application by re-running the `/get-grades` command with the new password.

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

### :two: Get an API Key from ocr.space

- Go to [https://ocr.space/ocrapi](https://ocr.space/ocrapi) and register for a free API key (the free tier allows 25,000 requests/month).
- Copy the **API Key** that is emailed to you for use in the application.

---

### :three: Prerequisites

Make sure you have [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) installed.

To verify installation:

```bash
dotnet --version
```

---

### :four: Clone the Repository

```bash
git clone https://github.com/adamt-eng/grade-monitor
```

---

### :five: Navigate to the Project Directory

```bash
cd grade-monitor
```

---

### :six: Restore and Build the Project

```bash
dotnet restore
dotnet build --configuration Release
```

---

### :seven: Run the Application

```bash
dotnet run
```
