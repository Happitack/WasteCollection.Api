# Waste Collection Request Application

## Description

This is a full-stack web application designed to simulate a system for submitting and processing waste collection requests. Users can submit details about a waste collection need, including selecting the location on an interactive map. The request is then processed asynchronously in the background.

## Features

* **Request Submission:** Users can submit waste collection requests via a web form.
* **Map Integration:** Select the precise location for the request using an interactive Leaflet map.
* **Database Storage:** Requests are stored persistently in a PostgreSQL database using Entity Framework Core.
* **Asynchronous Processing:** New requests trigger a message published to a RabbitMQ queue. A background worker service consumes these messages to simulate processing (updating request status).
* **Request Tracking:** View a list of submitted requests with their current status (Pending, Processing, Completed) and details.
* **Manual Completion:** Mark pending/processing requests as complete via the UI.
* **RESTful API:** A .NET Core Web API backend provides endpoints for managing requests.

## Technologies Used

* **Backend:**
    * C#
    * .NET 8 (or your specific version)
    * ASP.NET Core Web API
    * Entity Framework Core (EF Core)
    * PostgreSQL (Database)
    * RabbitMQ (Message Queue)
* **Frontend:**
    * HTML5
    * CSS3
    * JavaScript (ES6+)
    * Leaflet.js (Interactive Maps)
    * Fetch API (for backend communication)
* **Development Tools:**
    * Visual Studio Code (or Visual Studio)
    * .NET CLI
    * Git / GitHub
    * Docker / Docker Compose (for running dependencies)

## Architecture Overview

1.  **Frontend (HTML/CSS/JS):** User interacts with the form and map, sends requests to the API.
2.  **Backend API (ASP.NET Core):**
    * Receives HTTP requests (POST, GET, PUT).
    * Validates input.
    * Interacts with the database via EF Core (Saves new requests, updates status).
    * Publishes a message (containing the request ID) to RabbitMQ upon new request creation.
3.  **PostgreSQL Database:** Stores all `Request` data.
4.  **RabbitMQ:** Message broker holding the queue (`request_processing_queue`) of pending requests.
5.  **Background Worker (IHostedService):**
    * Runs within the API process.
    * Connects to RabbitMQ and listens for messages on the queue.
    * When a message is received:
        * Retrieves request details from the database using the ID from the message.
        * Simulates processing (e.g., updates status to 'Processing', waits, updates to 'Completed').
        * Saves changes back to the database via EF Core.
        * Acknowledges the message on the RabbitMQ queue.

## Prerequisites

Before you begin, ensure you have the following installed:

* [.NET SDK](https://dotnet.microsoft.com/download) (Version 8.0 or the version used in the project)
* [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine on Linux) - Required to easily run PostgreSQL and RabbitMQ.
* [Git](https://git-scm.com/downloads) (for cloning the repository)
* A code editor like [Visual Studio Code](https://code.visualstudio.com/) or Visual Studio.

## Setup & Running Locally

1.  **Clone the Repository:**
    ```bash
    git clone <your-repository-url>
    cd <repository-directory>/WasteCollection.Api
    ```

2.  **Configure Backend Settings:**
    * Create the `WasteCollection.Api/appsettings.Development.json` file.
    * **Crucially, update the `ConnectionStrings.DefaultConnection`**:
        * Set the `Host`, `Port`, `Database`, `Username`, and `Password` to match the values you will use for the PostgreSQL container (see Docker Compose step below). The defaults used in the `docker-compose.yml` are typically `Host=localhost`, `Port=5432`, `Database=WasteCollectionDb`, `Username=wasteadmin`, `Password=your_strong_password`. **Choose a strong password.**
    * **Verify the `RabbitMQ` section**:
        * Ensure `HostName`, `Port`, `UserName`, and `Password` match the values for the RabbitMQ container. The defaults in the `docker-compose.yml` are typically `HostName=localhost`, `Port=5672`, `UserName=guest`, `Password=guest`.
    * **Security Note:** Do *not* commit sensitive passwords directly into your Git repository if it's public. For real applications, use .NET User Secrets, environment variables, or a proper secrets management tool. For this local development setup, using `appsettings.Development.json` is acceptable if you use non-production passwords.

3.  **Run Backend Services (Database & Message Queue):**
    * Make sure Docker Desktop is running.
    * In the **root directory** of the cloned repository (where the `docker-compose.yml` file should be located - see below), run:
        ```bash
        docker-compose up -d
        ```
        *(This command will download the PostgreSQL and RabbitMQ images if you don't have them and start containers in the background based on the `docker-compose.yml` file).*
    * **Example `docker-compose.yml` file** (Create this file in the root of your project if it doesn't exist):
        ```yaml
        version: '3.8'
        services:
          postgres_db:
            image: postgres:latest
            container_name: waste-postgres # Matches name used previously
            environment:
              POSTGRES_DB: WasteCollectionDb # MUST match Database in appsettings
              POSTGRES_USER: wasteadmin # MUST match Username in appsettings
              POSTGRES_PASSWORD: your_strong_password # MUST match Password in appsettings
            ports:
              - "5432:5432" # Map host port 5432 to container port 5432
            volumes:
              - postgres_data:/var/lib/postgresql/data # Persist data

          rabbitmq_server:
            image: rabbitmq:3-management-alpine
            container_name: waste-rabbit # Matches name used previously
            ports:
              - "5672:5672" # AMQP port
              - "15672:15672" # Management UI port
            environment:
              # Default user/pass is guest/guest, suitable for local dev
              # RABBITMQ_DEFAULT_USER: guest
              # RABBITMQ_DEFAULT_PASS: guest
            volumes:
              - rabbitmq_data:/var/lib/rabbitmq/ # Persist data/config (optional)

        volumes:
          postgres_data:
          rabbitmq_data:
        ```
    * Wait a minute for the containers to initialize fully. You can check their status with `docker ps`.

4.  **Apply Database Migrations:**
    * Navigate to the API project directory in your terminal (if not already there):
        ```bash
        cd WasteCollection.Api
        ```
    * Run the EF Core database update command:
        ```bash
        dotnet ef database update
        ```
        *(This will create the `Requests` table in the PostgreSQL database running in Docker).*

5.  **Run the .NET Application:**
    * In the same `WasteCollection.Api` directory, run:
        ```bash
        dotnet run
        ```
    * Observe the console output. It should show the application starting, connecting to the database, and the `RabbitMqListenerService` starting and connecting to RabbitMQ. Note the URL the application is listening on (e.g., `http://localhost:5150`).

6.  **Access the Frontend:**
    * Open your web browser and navigate to the URL reported by `dotnet run` (e.g., `http://localhost:5150`).
    * You should see the Waste Collection Request form and map.

## API Endpoints

* `GET /api/Requests`: Retrieves all requests (ordered by submission date ascending).
* `GET /api/Requests/{id}`: Retrieves a specific request by its ID.
* `POST /api/Requests`: Creates a new request. Expects JSON body: `{ "Description": "...", "WasteType": "...", "ContactInfo": "...", "Latitude": ..., "Longitude": ... }`.
* `PUT /api/Requests/{id}/complete`: Marks the request with the specified ID as 'Completed'.

## (Optional) Future Enhancements

* Implement user authentication/authorization.
* Add more robust error handling and retry logic (e.g., dead-letter queues in RabbitMQ).
* Implement editing or cancellation of requests.
* Add input validation on the backend using FluentValidation.
* Write unit and integration tests.
* Containerize the API application itself using a Dockerfile for easier deployment.
* Improve frontend UI/UX (e.g., better map markers, loading indicators).
