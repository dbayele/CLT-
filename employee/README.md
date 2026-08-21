# CLT++ Employee Processing Portal

Separate ASP.NET Core 8 web application for employees who process CLT++ service requests.

## Permission model

Authorization is enforced server-side using claims:

- `department` claim: grants access to one department queue. Users may have multiple department claims.
- `Employee` role: sees only requests whose mapped/assigned department matches one of the user's department claims.
- `Supervisor` role: may see and process requests across departments.
- `Administrator` role: may see and process requests across departments.

Default category routing:

| Citizen category | Employee department |
|---|---|
| Police | Police |
| Fire | Fire |
| Streets & Transportation | Transportation |
| Solid Waste | Solid Waste |
| Neighborhoods | Housing & Neighborhood Services |
| Trees & Environment | General Services |
| Water | Charlotte Water |
| Animals | Animal Care & Control |

A request can later carry `processing.assignedDepartment`; when present, that department becomes authoritative for access checks.

## Processing features

Employees can:

- view a department-filtered work queue
- search by tracking number, service, or location
- filter by status and permitted department
- open request details and citizen-provided fields
- assign a request to an employee/team
- change workflow status
- add internal-only notes
- review internal processing history

Every request detail read and every processing write re-checks department authorization on the server. Hiding a queue item in the UI is not treated as the security boundary.

## Authentication

The prototype uses ASP.NET Core cookie authentication. Set `EMPLOYEE_USERS` as a semicolon-separated list:

```text
username:password:role:Department A|Department B;username2:password2:Employee:Police
```

For local `Development` only, when `EMPLOYEE_USERS` is unset, three demo accounts are available:

- `police.demo / ChangeMe!`
- `fire.demo / ChangeMe!`
- `supervisor.demo / ChangeMe!`

Do not use the development accounts in production. Replace the prototype login with Microsoft Entra ID or another organizational identity provider while preserving the same role/department claims.

## Shared request storage

The employee portal reads the same JSON request file as the citizen API. Set `CLTPP_DATA_PATH` in both processes to the same path.

```bash
cd employee
CLTPP_DATA_PATH=../server/bin/Debug/net8.0/data/requests.json dotnet run
```

For a real deployment, replace JSON-file persistence with a transactional database. Two independent processes writing a shared JSON file is suitable only for a prototype.

## Docker

Build from the repository root:

```bash
docker build -f employee/Dockerfile -t cltpp-employee .
```

Run with a persistent/shared data directory and configured users:

```bash
docker run --rm -p 5090:5090 \
  -e CLTPP_DATA_PATH=/data/requests.json \
  -e 'EMPLOYEE_USERS=police1:replace-me:Employee:Police' \
  -v cltpp-data:/data \
  cltpp-employee
```

Open `http://localhost:5090`.

## Production requirements

Before operational use: integrate organizational SSO/MFA, move requests to PostgreSQL/SQL Server, add immutable audit logging, encrypt sensitive data, define retention/redaction policies, add granular permission administration, use secure secrets management, and perform security/accessibility testing.
