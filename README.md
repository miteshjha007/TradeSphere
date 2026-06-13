# TradeSphere

TradeSphere is a professional crypto trading web application built with .NET 8 and Angular 16+.

## Prerequisites

- .NET 8 SDK
- Node.js (LTS version)
- PostgreSQL Database

## Project Structure

- `TradeSphere.API`: Backend API (clean architecture)
- `TradeSphere-UI`: Frontend Angular application
- `TradeSphere.TradingEngine`: Background worker for trading logic

## Setup Instructions

### Database Setup

1. Create a PostgreSQL database named `TradeSphereDb`.
2. Run the script `TradeSphere_Schema.sql` in your database to create the schema.
   - Alternatively, configure the connection string in `TradeSphere.API/appsettings.json` and run migrations if set up (Schema script provided for now).
3. Update `TradeSphere.API/appsettings.json` with your connection string.

### Backend Setup

1. Navigate to `TradeSphere.API` directory.
2. Run `dotnet restore`.
3. Run `dotnet run`.
   - The API will start on `http://localhost:5083` (or configured port).
   - Swagger UI available at `http://localhost:5083/swagger`.

### Frontend Setup

1. Navigate to `TradeSphere-UI` directory.
2. Run `npm install`.
3. Run `ng s` (or `npm start`).
   - The application will start on `http://localhost:4200`.

## Features

- **Authentication**: JWT based login/register with Role-based access.
- **Dashboard**: User dashboard (skeleton ready).
- **Modules**: Auth, Dashboard, Exchanges, Strategies, etc. (skeletons ready).

## Default Login

- Register a new user via the `/auth/register` page.
