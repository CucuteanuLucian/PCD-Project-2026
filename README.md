# PCD Project 2026 – Distributed Comment Processing System

Sistem distribuit de procesare asincronă a comentariilor cu analiză de sentiment
și notificări în timp real, construit pe Azure cloud, pornind de la aplicația
**RealWorld (Conduit) – ASP.NET Core**.

---

## Arhitectura sistemului

```
Client (Browser)
    │
    │ POST /api/articles/{slug}/comments  (HTTP REST)
    ▼
Service A – RealWorld API (ASP.NET Core)
    │ publică eveniment asincron
    ▼
Azure Service Bus – comments-queue
    │ trigger automat
    ▼
Service B – Sentiment Processor (Azure Function .NET)
    │ actualizează PostgreSQL
    │ publică eveniment asincron
    ▼
Azure Service Bus – comments-processed
    │ consume mesaje
    ▼
Service C – Notification Service (ASP.NET Core + SignalR)
    │ push real-time
    ▼
Client (Browser) – status: pending → processed + sentiment score
```

---

## Componente

| Serviciu | Stack | Hosting | Rol |
|---|---|---|---|
| Service A – RealWorld API | ASP.NET Core 10, EF Core, PostgreSQL | Azure App Service | API principal, publică în Service Bus |
| Service B – Sentiment Processor | Azure Functions (.NET 8, isolated) | Azure Function App | Procesare asincronă, analiză sentiment |
| Service C – Notification Service | ASP.NET Core 10, SignalR | Azure App Service | Notificări real-time via WebSocket |
| Frontend | HTML5, Vanilla JS, SignalR JS | Static / App Service | UI cu status live și metrici |

## Servicii cloud Azure

| Serviciu | Tip | Rol |
|---|---|---|
| Azure App Service | PaaS | Hosting Service A + C |
| Azure Functions | FaaS | Service B – procesare asincronă |
| Azure Service Bus | Messaging | Comunicare event-driven între servicii |
| Azure Database for PostgreSQL | Stateful DB | Persistență comentarii + scoruri |

---

## Structura repository

```
PCD-Project-2026/
├── src/
│   ├── Conduit/               ← Service A: RealWorld API (extins)
│   ├── SentimentProcessor/    ← Service B: Azure Function
│   └── NotificationService/   ← Service C: SignalR notifications
├── frontend/
│   └── index.html             ← Dashboard real-time
├── docs/
│   ├── ARCHITECTURE.md        ← Diagrame + flux complet
│   ├── TASKS.md               ← Task list cu status
│   ├── SERVICE_B_SENTIMENT.md ← Documentație Service B
│   ├── SERVICE_C_NOTIFICATION.md ← Documentație Service C
│   ├── FRONTEND.md            ← Documentație Frontend
│   └── AZURE_SETUP.md         ← Resurse Azure create
└── README.md
```

---

## Build local

### Cerințe
- .NET 10 SDK
- .NET 8 SDK (pentru Azure Function)
- Azure CLI (pentru deployment)

### Service A – RealWorld API
```bash
cd src/Conduit
dotnet run
# API disponibil la http://localhost:5000
# Swagger la http://localhost:5000/swagger
```

> Necesită variabila de mediu `ConnectionStrings__ServiceBus` pentru publicarea în Service Bus.
> Fără ea, folosește `FakeMessageBus` (doar log în consolă).

### Service C – Notification Service
```bash
cd src/NotificationService
# Setează connection string în appsettings.json sau env var:
# ConnectionStrings__ServiceBus = <connection-string>
dotnet run
# Disponibil la http://localhost:5001
# SignalR hub la http://localhost:5001/hubs/comments
# Health check la http://localhost:5001/health
```

### Service B – Azure Function (local)
```bash
# Instalare Azure Functions Core Tools
npm install -g azure-functions-core-tools@4

cd src/SentimentProcessor
# Completează local.settings.json cu connection strings
func start
```

### Frontend
```bash
# Deschide direct în browser:
open frontend/index.html

# Sau cu server HTTP:
cd frontend && python3 -m http.server 3000
```

---

## Deploy pe Azure

### Cerințe prealabile
```bash
az login
# Resource group și resurse deja create – vezi docs/AZURE_SETUP.md
```

### 1. App Service Plan
```bash
az appservice plan create \
  --name pcd-app-plan \
  --resource-group pcd-project-rg \
  --sku B1 --is-linux
```

### 2. Deploy Service A
```bash
az webapp create --name pcd-realworld-api \
  --resource-group pcd-project-rg \
  --plan pcd-app-plan --runtime "DOTNETCORE:10.0"

cd src/Conduit && dotnet publish -c Release -o ./publish
cd publish && zip -r ../service-a.zip . && cd ..
az webapp deployment source config-zip \
  --name pcd-realworld-api \
  --resource-group pcd-project-rg --src service-a.zip

az webapp config appsettings set --name pcd-realworld-api \
  --resource-group pcd-project-rg \
  --settings \
    ConnectionStrings__ServiceBus="<sb-connection-string>" \
    ConnectionStrings__DefaultConnection="<pg-connection-string>"
```

### 3. Deploy Service C
```bash
az webapp create --name pcd-notification-service \
  --resource-group pcd-project-rg \
  --plan pcd-app-plan --runtime "DOTNETCORE:10.0"

cd src/NotificationService && dotnet publish -c Release -o ./publish
cd publish && zip -r ../service-c.zip . && cd ..
az webapp deployment source config-zip \
  --name pcd-notification-service \
  --resource-group pcd-project-rg --src service-c.zip

az webapp config appsettings set --name pcd-notification-service \
  --resource-group pcd-project-rg \
  --settings \
    ConnectionStrings__ServiceBus="<sb-connection-string>" \
    AllowedOrigin="https://pcd-realworld-api.azurewebsites.net"
```

### 4. Deploy Service B (Azure Function)
```bash
az storage account create --name pcdfunctionstorage \
  --resource-group pcd-project-rg --sku Standard_LRS

az functionapp create --name pcd-sentiment-processor \
  --resource-group pcd-project-rg \
  --storage-account pcdfunctionstorage \
  --consumption-plan-location northeurope \
  --runtime dotnet-isolated --runtime-version 8 --functions-version 4

cd src/SentimentProcessor && dotnet publish -c Release -o ./publish
cd publish && zip -r ../service-b.zip . && cd ..
az functionapp deployment source config-zip \
  --name pcd-sentiment-processor \
  --resource-group pcd-project-rg --src service-b.zip

az functionapp config appsettings set \
  --name pcd-sentiment-processor \
  --resource-group pcd-project-rg \
  --settings \
    ServiceBusConnection="<sb-connection-string>" \
    PostgresConnection="<pg-connection-string>"
```

---

## Testare end-to-end

1. Deschide `frontend/index.html` (sau URL-ul Azure)
2. Register / Login cu un cont nou
3. Selectează un articol din dropdown
4. Scrie un comentariu și apasă **Postează**
5. Comentariul apare cu status **⏳ pending**
6. După ~1-3 secunde, statusul devine **✅ processed** cu scorul de sentiment

---

## Load Testing

```bash
# Instalare k6
brew install k6

# Rulare test (50 useri, 2 minute)
k6 run load-test/comments-load-test.js
```

---

## Metrici urmărite

| Metrică | Descriere |
|---|---|
| Latență end-to-end | De la POST comentariu până la notificarea SignalR |
| Throughput | Comentarii procesate / minut de Azure Function |
| Consistency window | Intervalul pending → processed |
| Error rate | % mesaje eșuate în Service Bus |
