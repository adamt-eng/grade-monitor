### Grade Monitor For Faculty of Engineering - Ain Shams University Students

An application that automates the retrieval of grades from the [ASU-ENG faculty portal](https://eng.asu.edu.eg/login) and sends them directly to the user via Discord. The app allows users to select different semesters and refresh grade data efficiently. It is designed with robust retry mechanisms to handle server downtimes and uses cookies to maintain sessions for faster access.

## Used Technologies

- **.NET 9 (C#):** Core framework for building and running the application logic.
- **Selenium WebDriver:** Used to automate browser interactions for login and reCAPTCHA solving.
- **SolveCaptcha API:** Used to programmatically solve Google reCAPTCHA v2 challenges.
- **Json.NET (Newtonsoft.Json):** For parsing and handling JSON configurations and API responses.
- **CookieContainer (System.Net):** Maintains session cookies between HTTP requests for persistent login sessions.
- **Discord.Net:** A C# wrapper for the Discord API, enabling bot interaction, slash commands, and message handling.

## Showcase
![Showcase](Showcase.gif)

## Features

- **Automatic Grade Retrieval:** The app logs in and retrieves the user's grades from the relevant pages on the faculty portal, sending the results directly to the user in a private message.

- **Semester Selection:** Users can select different semesters to view grades from previous terms. The current semester is selected by default, but users have the flexibility to choose any other available semester.

- **Mode 1: Final Grades:** When faculty servers are under heavy load, this mode reduces the number of HTTP requests by retrieving only the final grades from the courses registration page. While it shows fewer details, it is particularly useful when awaiting final course grades.

- **Mode 2: All Grades:** This mode fetches all course grades such as final, midterm, activities, etc.

- **Refresh Grades Button:** Users can manually refresh the grades data to check for updates based on the current semester and load selection.

- **Refresh Courses Button:** Users can use the `Refresh Courses` button to force refetching of their course data, this is particularly useful to remove/add courses that users have dropped/withdrawn/registered.

- **Session Persistence:** The app uses a `CookieContainer` to manage session cookies. This allows it to maintain a session across multiple requests without needing to log in repeatedly, saving network resources and reducing the time taken to get grades.

- **Retry Mechanism:** Given the frequent downtimes of the faculty website, the app employs a retry mechanism to ensure reliable grade retrieval. If a request fails, the app will use a shorter refresh interval to check for grades more frequently so that we're able to retrieve the grades as soon as the server is back up.

- **Update Interval:** The app allows you to customize how frequently it fetches your grades. There are two interval settings: one for normal conditions, and another used as part of the retry mechanism, which activates after an error to refresh more frequently until recovery.

- **CAPTCHA Solver:** The app includes a [reCAPTCHA solver](solvecaptcha.com) to automatically solve the `I'm not a robot` challenges that are on the login page of the faculty site.

## Usage

### 1. Get Grades

- Use the `/get-grades` command to get your grades report. This command requires your student id and password.

**Example:**

```
/get-grades student-id:23P0001 password:tHiSiSmYpAsSwOrD
```

### 2. Select Semester

- After the initial command execution, the bot will send a message with an interactive dropdown menu.
  
- Select the semester you want to view grades for from the menu, the current (or latest) semester is selected by default.

### 3. Select Mode

The bot provides an option to manage how grades are retrieved:

- **Mode 1: Final Grades:** Reduces the number of HTTP requests by retrieving only the final grades. Useful during peak times when the faculty servers are under heavy load.

- **Mode 2: All Grades:** Retrieves detailed course grades. This is the default setting.

### 4. Refresh Grades

To manually refresh and check for updated grades:

- Click the "Refresh Grades" button.

- The bot will refresh the grade data based on the current semester and mode selection.

This interaction flow ensures that you always have access to your most up-to-date grades while providing flexibility to manage how data is retrieved based on server conditions.

### 5. Update Interval

To update the intervals 

- You can customize how often the app fetches your grades using the `/update-interval` command. This command allows you to set:
	- normal-interval: The interval (in minutes) used under normal conditions.
	- interval-after-errors: The interval (in minutes) used when an error occurs, allowing the app to retry more frequently until recovery.

**Example:**

```
/update-interval normal-interval:60 interval-after-errors:5
```

### 6. reCAPTCHA Solver

- **CAPTCHA Solver:** The app includes a [reCAPTCHA solver](solvecaptcha.com) to automatically solve the `I'm not a robot` challenges that are on the login page of the faculty site.
- Because of this reCAPTCHA, the app now utilizes [Selenium](https://github.com/SeleniumHQ/selenium) **for the login process only**, to be able to load the reCAPTCHA, solve it, and return the response.

## Setup Instructions

### 1. **Create a Discord App**

- Create a new application from the [Discord Developer Portal](https://discord.com/developers/applications).

- Application scopes must contain `application.commands` and `bot`.

- Copy the bot's token; you'll need this later.

- Copy the bot's install link and use it to add the bot to your desired server.

### 2. **Prerequisites**

- Ensure that you have [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) installed on your machine:

- You can verify the .NET SDK installation by running:

  ```bash
  dotnet --version
  ```

### 3. **Download the Source Code**

- Clone this repository using the following command:

  ```bash
  git clone https://github.com/adamt-eng/grade-monitor
  ```

### 4. **Navigate to the Project Directory**

- After cloning the repository, navigate into the project directory:

  ```bash
  cd grade-monitor
  ```

### 5. **Compile the Source Code**

- Restore dependencies with the following command:

  ```bash
  dotnet restore
  ```

- Once dependencies are restored, compile the project using:

  ```bash
  dotnet build --configuration Release
  ```

### 6. **Run the Application:**

- If the build is successful, you can run the application with:

  ```bash
  dotnet run
  ```

> :warning: **Important Note:** 
> For the application to continuously monitor grades, you must keep it running. You might consider using a [Virtual Private Server (VPS)](https://cloud.google.com/learn/what-is-a-virtual-private-server) to keep it running 24/7. Alternatively, you can run it locally on your machine whenever needed.

## Configuration

   - The bot's configuration, including user credentials, is stored in `config.json`. It is recommended not to modify this file manually.
   - The `Laravel_Session` value in `config.json` is no longer modifiable via command. Previously, users could log in by directly providing the session cookie, but this is no longer supported due to the expiration date now embedded in the cookie. Instead, the login process uses the student ID and password via Selenium to retrieve and store a valid session token.
   - If you change your password on the faculty site, you can update it in the application by re-running the `/get-grades` command with the new password.
