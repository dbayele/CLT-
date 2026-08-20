# CLT++

CLT++ is an **unofficial demonstration** of a modern municipal service-request portal inspired by common 311 workflows. It is not affiliated with, endorsed by, or operated by the City of Charlotte, Charlotte-Mecklenburg Police Department (CMPD), or Charlotte Crime Stoppers.

The app uses a React + TypeScript frontend and an ASP.NET Core API. It includes a searchable service catalog, a four-step request wizard, request tracking, and a dedicated Police section with:

- **Report a Crime (Non-Emergency)** — a guided intake for non-urgent incidents, with emergency gating and offense choices aligned to CMPD's published online-reporting guidance.
- **Provide a Tip** — an anonymous-by-default Crime Stoppers-style intake that captures incident, suspect, vehicle, weapon/drug, evidence, and narrative details.

> Important: this demo stores submissions only in its own local JSON data file. It does **not** transmit police reports or tips to CMPD, 311, Charlotte Crime Stoppers, P3 Tips, or any government system.

## Stack

- React 18 + TypeScript + Vite
- ASP.NET Core 8 minimal API
- JSON-file persistence (zero database dependencies; easy to replace with PostgreSQL/SQL Server)
- Docker + docker compose

## Run locally

### API

```bash
cd server
dotnet restore
dotnet run
```

The API defaults to `http://localhost:5080`.

### Web app

```bash
cd client
npm install
npm run dev
```

The web app defaults to `http://localhost:5173` and proxies `/api` to the API.

### Docker

```bash
docker compose up --build
```

Then open `http://localhost:8080`.

## Public hostname

The frontend reads `VITE_PUBLIC_HOST`. The repository intentionally does not claim an official-looking City of Charlotte hostname. For deployment, set this to a domain you legitimately control and keep the **Unofficial Demo** disclosure visible.

## Police safety behavior

The police flows intentionally display official routing guidance:

- Call **911** for crimes in progress, suspects on scene, immediate danger, or urgent police/fire/medical response.
- CMPD lists **311** for non-emergency police services (or 704-336-7600 outside Mecklenburg County).
- Charlotte Crime Stoppers publishes **704-334-1600** and an anonymous online tip option.

Before any real-world production use, add authentication/authorization where appropriate, encryption at rest, audit logging, retention/deletion policies, malware scanning for uploads, rate limiting, accessibility testing, privacy/legal review, agency integration agreements, and a production database.
