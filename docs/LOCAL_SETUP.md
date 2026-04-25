# Setup Local – Ghid complet pentru toți membrii echipei

## Cerințe prealabile

| Tool | Versiune | Instalare |
|---|---|---|
| .NET SDK | 10.0.203 | https://dotnet.microsoft.com/download |
| .NET SDK | 8.0.x (pentru Azure Function) | https://dotnet.microsoft.com/download |
| Git | orice | https://git-scm.com |
| Azure CLI | 2.85+ | `brew install azure-cli` (Mac) |

---

## 1. Clonare repo

```bash
git clone git@github.com:<org>/PCD-Project-2026.git
cd PCD-Project-2026
```

---

## 2. Connection strings (cere-le lui Cosmin pe Discord/WhatsApp)

Ai nevoie de două valori din fișierul `.env.azure`:
- `SERVICE_BUS_CONNECTION_STRING`
- `POSTGRES_CONNECTION_STRING`

Acestea NU sunt în repo (excluse din git pentru securitate).

---

## 3. Rulare Service A – RealWorld API

```bash
cd src/Conduit
```

Crează fișierul `appsettings.Local.json` (nu se comite în git):
```json
{
  "ConnectionStrings": {
    "ServiceBus": "<SERVICE_BUS_CONNECTION_STRING de la Cosmin>"
  }
}
```

Sau setează variabile de mediu:
```bash
# Mac/Linux
export ConnectionStrings__ServiceBus="<connection-string>"
export DatabaseProvider="sqlite"   # sau "postgresql" pentru Azure DB

dotnet run
```

```powershell
# Windows
$env:ConnectionStrings__ServiceBus="<connection-string>"
$env:DatabaseProvider="sqlite"
dotnet run
```

API disponibil la: **http://localhost:5000**
Swagger la: **http://localhost:5000/swagger**

> **Fără connection string Service Bus?** Modifică temporar `Program.cs`:
> înlocuiește `AzureServiceBusMessageBus` cu `FakeMessageBus` — comentariile
> vor fi salvate dar nu trimise în Service Bus.

---

## 4. Rulare Service C – Notification Service

```bash
cd src/NotificationService
```

Editează `appsettings.json` sau setează env var:
```bash
export ConnectionStrings__ServiceBus="<connection-string>"
dotnet run
```

Service disponibil la: **http://localhost:5001**
Health check: **http://localhost:5001/health**
SignalR hub: **http://localhost:5001/hubs/comments**

---

## 5. Rulare Service B – Azure Function (local)

```bash
# Instalare Azure Functions Core Tools
npm install -g azure-functions-core-tools@4 --unsafe-perm true

cd src/SentimentProcessor
```

Editează `local.settings.json`:
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnection": "<SERVICE_BUS_CONNECTION_STRING>",
    "PostgresConnection": "<POSTGRES_CONNECTION_STRING>"
  }
}
```

```bash
func start
```

---

## 6. Rulare Frontend

```bash
# Opțiunea 1: direct în browser
open frontend/index.html

# Opțiunea 2: server HTTP
cd frontend && python3 -m http.server 3000
# Deschide http://localhost:3000
```

**IMPORTANT:** Dacă rulezi local, editează primele 2 linii din `frontend/index.html`:
```javascript
const API_URL = 'http://localhost:5000/api';
const NOTIFICATION_URL = 'http://localhost:5001';
```

Pentru Azure (producție), valorile sunt deja setate:
```javascript
const API_URL = 'https://pcd-realworld-api.azurewebsites.net/api';
const NOTIFICATION_URL = 'https://pcd-notification-service.azurewebsites.net';
```

---

## 7. Testare end-to-end locală

1. Pornește Service A (`dotnet run` în `src/Conduit`) → http://localhost:5000
2. Pornește Service C (`dotnet run` în `src/NotificationService`) → http://localhost:5001
3. Pornește Service B (`func start` în `src/SentimentProcessor`)
4. Deschide `frontend/index.html`
5. Register un cont nou
6. Postează un comentariu
7. Verifică că statusul se schimbă din `pending` în `processed`

---

## 8. URL-uri Azure (producție)

| Serviciu | URL |
|---|---|
| Service A – RealWorld API | https://pcd-realworld-api.azurewebsites.net |
| Swagger | https://pcd-realworld-api.azurewebsites.net/swagger |
| Service C – Notification Service | https://pcd-notification-service.azurewebsites.net |
| Health Check | https://pcd-notification-service.azurewebsites.net/health |
| Frontend | deschide `frontend/index.html` local |

---

## 9. Build complet (CI)

```bash
dotnet run --project build/build.csproj
```

Rulează: format → build → teste → publish

---

## Troubleshooting

| Problemă | Soluție |
|---|---|
| `ServiceBus connection string missing` | Setează env var `ConnectionStrings__ServiceBus` |
| Service A cade la startup | Verifică că PostgreSQL connection string e corect |
| SignalR nu se conectează | Verifică CORS — `AllowedOrigin` în Service C |
| Azure Function nu se declanșează | Verifică că `ServiceBusConnection` e setat în `local.settings.json` |
