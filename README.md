### Grade Monitor For Faculty of Engineering - Ain Shams University Students

An application that automates the retrieval of grades from the [ASU-ENG faculty portal](https://eng.asu.edu.eg/login) and sends them directly to the user via Discord. The app allows users to select different semesters, manage server load, and refresh grade data efficiently. It is designed with robust retry mechanisms to handle server downtimes and uses cookies to maintain sessions for faster access.

## Features

- **Automatic Grade Retrieval:** The app logs in and retrieves the user's grades from the relevant pages on the faculty portal, sending the results directly to the user in a private message.

- **Semester Selection:** Users can select different semesters to view grades from previous terms. The current semester is selected by default, but users have the flexibility to choose any other available semester.

- **Heavy Load Mode:** When faculty servers are under heavy load, this mode reduces the number of HTTP requests by retrieving only the final grades from the courses registration page. While it shows fewer details, it is particularly useful when awaiting final course grades.

- **Refresh Functionality:** Users can manually refresh the grades data to check for updates based on the current semester and load selection.

- **Session Persistence:** The app uses a `CookieContainer` to manage session cookies. This allows it to maintain a session across multiple requests without needing to log in repeatedly, saving network resources and reducing the time taken to get grades.

- **Retry Mechanism:** Given the frequent downtimes of the faculty website, the app employs a retry mechanism to ensure reliable grade retrieval. If a request fails, the app will use a shorter refresh interval to check for grades more frequently so that we're able to retrieve the grades as soon as the server is back up.

## Usage

### 1. Get Grades

- Use the `/get-grades-with-id-and-password` slash command to get your grades report. This command requires your student ID and password.

**Example:**

```
/get-grades-with-id-and-password student-id:23P0001 password:tHiSiSmYpAsSwOrD
```

- You can alternatively use the `/get-grades-with-session-cookie` slash command to bypass the faculty site's CAPTCHA which is sometimes required to login. This command requires your `laravel_session` cookie value.

- You can find your `laravel_session` cookie value stored in your browser after you successfully log in to the faculty's site manually.

### 2. Select Semester

- After the initial command execution, the bot will send a message with an interactive dropdown menu.
  
- Select the semester you want to view grades for from the menu, the current (or latest) semester is selected by default.

### 3. Manage Server Load

The bot provides an option to manage how grades are retrieved based on server load:

- **Normal Load:** Retrieves detailed course grades. This is the default setting.

- **Heavy Load:** Reduces the number of HTTP requests by getting only the final grades. Useful during peak times when the faculty servers are under heavy load.

### 4. Refresh Grades

To manually refresh and check for updated grades:

- Click the "Refresh" button in the private message sent by the bot.

- The bot will refresh the grade data based on the current semester and load selection.

This interaction flow ensures that you always have access to your most up-to-date grades while providing flexibility to manage how data is retrieved based on server conditions.

## Showcase
![Showcase](Showcase.gif)

- Empty result when using `Heavy Load` indicates that final grades for the specified semester aren't released yet.

## Setup Instructions

### 1. **Create a Discord App**

- Create a new application from the [Discord Developer Portal](https://discord.com/developers/applications).

- Application scopes must contain `application.commands` and `bot`.

- Copy the bot's token; you'll need this later.

- Copy the bot's install link and use it to add the bot to your desired server.

### 2. **Prerequisites**

- Ensure that you have [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed on your machine:

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

   - If you change your password on the faculty site, you can update it in the application by re-running the `/get-grades-using-id-and-password` command with the new password.
     
   - If you add/drop/withdraw courses, you can force the application to add the new courses or remove the dropped/withdrawn courses by re-running either of the `get-grade` commands.
