# Azure Setup – Resurse create

## Resurse existente în Azure

| Resursă | Tip | Nume | Locație |
|---|---|---|---|
| Resource Group | — | `pcd-project-rg` | West Europe |
| Service Bus Namespace | Microsoft.ServiceBus | `pcd-servicebus-ns` | West Europe |
| Service Bus Queue | Queue | `comments-queue` | — |
| Service Bus Queue | Queue | `comments-processed` | — |
| PostgreSQL Server | Flexible Server v16 | `pcd-postgres-server` | North Europe |
| PostgreSQL Database | Database | `conduit` | — |

---

## Connection Strings (din .env.azure)

```
Service Bus:
Endpoint=sb://pcd-servicebus-ns.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=***

PostgreSQL:
Host=pcd-postgres-server.postgres.database.azure.com;Database=conduit;Username=pcdadmin;Password=PcdProject2026!;SslMode=Require
```

> **Secretele complete sunt în `.env.azure`** (exclus din git). Distribuie echipei prin canal privat.

---

## Resurse de creat (next steps)

### App Service Plan
```bash
az appservice plan create \
  --name pcd-app-plan \
  --resource-group pcd-project-rg \
  --sku B1 \
  --is-linux
```

### App Service – Service A (RealWorld API)
```bash
az webapp create \
  --name pcd-realworld-api \
  --resource-group pcd-project-rg \
  --plan pcd-app-plan \
  --runtime "DOTNETCORE:10.0"

az webapp config appsettings set \
  --name pcd-realworld-api \
  --resource-group pcd-project-rg \
  --settings \
    ConnectionStrings__ServiceBus="<sb-connection-string>" \
    ConnectionStrings__DefaultConnection="<postgres-connection-string>"
```

### App Service – Service C (Notification Service)
```bash
az webapp create \
  --name pcd-notification-service \
  --resource-group pcd-project-rg \
  --plan pcd-app-plan \
  --runtime "DOTNETCORE:10.0"

az webapp config appsettings set \
  --name pcd-notification-service \
  --resource-group pcd-project-rg \
  --settings \
    ConnectionStrings__ServiceBus="<sb-connection-string>" \
    AllowedOrigin="https://pcd-realworld-api.azurewebsites.net"
```

### Azure Function App – Service B (Sentiment Processor)
```bash
az storage account create \
  --name pcdfunction storage \
  --resource-group pcd-project-rg \
  --sku Standard_LRS

az functionapp create \
  --name pcd-sentiment-processor \
  --resource-group pcd-project-rg \
  --storage-account pcdfunctionstorage \
  --consumption-plan-location northeurope \
  --runtime dotnet-isolated \
  --functions-version 4
```

---

## Diagrama resurselor Azure

```
pcd-project-rg (Resource Group)
├── pcd-servicebus-ns (Service Bus)
│   ├── comments-queue
│   └── comments-processed
├── pcd-postgres-server (PostgreSQL)
│   └── conduit (database)
├── pcd-app-plan (App Service Plan)
│   ├── pcd-realworld-api (Service A)
│   └── pcd-notification-service (Service C)
└── pcd-sentiment-processor (Azure Function – Service B)
```

---

## Firewall PostgreSQL

Serverul PostgreSQL are firewall-ul configurat să accepte IP-ul curent (86.125.183.3).
Pentru Azure App Service, adaugă regula:
```bash
az postgres flexible-server firewall-rule create \
  --name AllowAzureServices \
  --resource-group pcd-project-rg \
  --server-name pcd-postgres-server \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```
