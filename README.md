### Grade Monitor For Faculty of Engineering - Ain Shams University Students

A Discord bot that automates the retrieval of grades from the [faculty portal](https://eng.asu.edu.eg/login) and sends them directly to the user via private messages. The bot allows users to select different semesters, manage server load, and refresh data efficiently. It is designed with robust retry mechanisms to handle server downtimes and uses cookies to maintain sessions for faster access.

## Features

- **Automatic Grade Retrieval:** The bot automatically logs in and fetches the user's grades from the faculty portal, sending the results directly to the user in a private message.

- **Semester Selection:** Users can select different semesters to view grades from previous terms. The current semester is selected by default, but users have the flexibility to choose any other available semester.

- **Heavy Load Mode:** When faculty servers are under heavy load, this mode reduces the number of HTTP requests by fetching only the final grades from the courses registration page. While it shows fewer details, it is particularly useful when waiting for final grades.

- **Refresh Functionality:** Users can manually refresh the grades data to check for updates based on the current semester and load selection.

- **Session Persistence:** The bot uses a `CookieContainer` to manage session cookies. This allows the bot to maintain a session across multiple requests without needing to log in repeatedly, saving network resources and reducing the time taken to fetch grades.

- **Retry Mechanism:** Given the frequent downtimes of the faculty website, the bot employs a retry mechanism to ensure reliable grade retrieval. If a request fails, the bot will retry up to 10 times, with the delay between retries increasing exponentially. It starts with a 3-second delay and increases to a maximum of 30 seconds.

## How It Works

- **Fetching Grades:** The bot fetches grades by navigating to the relevant pages on the faculty portal. Depending on the load selection specified by the user, it either fetches detailed grades (Normal Load) or only the final grades (Heavy Load).

- **Session Management:** By utilizing cookies, the bot maintains a session across multiple requests, avoiding the need for frequent logins.

- **Error Handling:** The bot provides feedback if the login fails due to incorrect credentials or if the faculty server is down. In the event of a failure, it retries the process using the built-in retry mechanism.

## Usage

Once the bot is [set up](#setup-instructions) and running on your Discord server, you can interact with it using the following commands and options:

### 1. Fetch Grades

Use the `/get-grades` slash command to retrieve your grades. This command requires your student ID and password.

**Example:**

```
/get-grades student-id:23P0001 password:tHiSiSmYpAsSwOrD
```

The bot will send you a private message with your current grades.

### 2. Select Semester

- After the initial command execution, the bot will send a message with an interactive dropdown menu.
  
- Select the semester you want to view grades for from the menu, the current (or latest) semester is selected by default.

### 3. Manage Server Load

The bot provides an option to manage how grades are fetched based on server load:

- **Normal Load:** Retrieves detailed course grades. This is the default setting.

- **Heavy Load:** Reduces the number of HTTP requests by fetching only the final grades. Useful during peak times when the faculty servers are under heavy load.

### 4. Refresh Grades

To manually refresh and check for updated grades:

- Click the "Refresh" button in the private message sent by the bot.

- The bot will re-fetch the grade data based on the current semester and load selection.

This interaction flow ensures that you always have access to your most up-to-date grades while providing flexibility to manage how data is retrieved based on server conditions.

## Setup Instructions

1. **Create a Discord Bot:**

   - Create a new application from the [Discord Developer Portal](https://discord.com/developers/applications).

   - Application scopes must contain `application.commands` and `bot`.

   - Copy the bot's token; you'll need this later.
     
   - Copy the bot's install link and use it to add the bot to your desired server.

2. **Download the Project:**

   - Download the latest release of the project from the [releases page](https://github.com/adamt-eng/grade-monitor/releases).

3. **Run the Application:**

   - Execute the application and input your Discord bot token.

   - The bot will automatically register the `/get-grades` command on your server.

4. **Using the Bot:**

   - Use the `/get-grades` slash command in your Discord server:

     ```
     /get-grades student-id:23P0001 password:tHiSiSmYpAsSwOrD
     ```

   - The bot will send the grades to you in a private message.

5. **Configuration:**

   - The bot's configuration, including user credentials, is stored in `config.json`. It is recommended not to modify this file manually unless you are sure about what you are doing.

   - If your password changes, you can update it by re-running the `/get-grades` command with the new password.
