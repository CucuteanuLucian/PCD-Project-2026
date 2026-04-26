# TODO – Ce mai rămâne de făcut

## 🔴 BLOCKER – Service A trebuie fixat (Echipa)

Service A are SQLite hardcodat în `Program.cs`. Trebuie schimbat să suporte PostgreSQL
prin variabile de mediu, altfel deploy-ul pe Azure nu funcționează.

**Fișier de modificat:** `src/Conduit/Program.cs`

```csharp
// Înlocuiește:
var defaultDatabaseConnectionString = "Filename=realworld.db";
var defaultDatabaseProvider = "sqlite";

// Cu:
var defaultDatabaseConnectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Filename=realworld.db";
var defaultDatabaseProvider =
    Environment.GetEnvironmentVariable("DatabaseProvider") ?? "sqlite";
```

Și adaugă suport PostgreSQL în `Directory.Packages.props`:
```xml
<PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.4" />
```

Și în `src/Conduit/Conduit.csproj`:
```xml
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
```

Și în `Program.cs` în blocul `AddDbContext`:
```csharp
else if (databaseProvider.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
{
    options.UseNpgsql(connectionString);
}
```

---

## 🟡 T1 – Deploy Service A pe Azure App Service (Cosmin)

**Timp estimat: ~20 min**

```bash
az appservice plan create \
  --name pcd-app-plan \
  --resource-group pcd-project-rg \
  --sku B1 --is-linux

az webapp create \
  --name pcd-realworld-api \
  --resource-group pcd-project-rg \
  --plan pcd-app-plan \
  --runtime "DOTNETCORE:10.0"

cd src/Conduit
dotnet publish -c Release -o ./publish
cd publish && zip -r ../service-a.zip . && cd ..

az webapp deployment source config-zip \
  --name pcd-realworld-api \
  --resource-group pcd-project-rg \
  --src service-a.zip

az webapp config appsettings set \
  --name pcd-realworld-api \
  --resource-group pcd-project-rg \
  --settings \
    DatabaseProvider="postgresql" \
    ConnectionStrings__DefaultConnection="Host=pcd-postgres-server.postgres.database.azure.com;Database=conduit;Username=pcdadmin;Password=PcdProject2026!;SslMode=Require" \
    ConnectionStrings__ServiceBus="<sb-connection-string din .env.azure>"
```

---

## 🟡 T2 – Deploy Service C pe Azure App Service (Cosmin)

**Timp estimat: ~15 min**

```bash
az webapp create \
  --name pcd-notification-service \
  --resource-group pcd-project-rg \
  --plan pcd-app-plan \
  --runtime "DOTNETCORE:10.0"

cd src/NotificationService
dotnet publish -c Release -o ./publish
cd publish && zip -r ../service-c.zip . && cd ..

az webapp deployment source config-zip \
  --name pcd-notification-service \
  --resource-group pcd-project-rg \
  --src service-c.zip

az webapp config appsettings set \
  --name pcd-notification-service \
  --resource-group pcd-project-rg \
  --settings \
    ConnectionStrings__ServiceBus="<sb-connection-string din .env.azure>" \
    AllowedOrigin="https://pcd-realworld-api.azurewebsites.net"
```

---

## 🟡 T3 – Deploy Service B pe Azure Function App (Cosmin)

**Timp estimat: ~20 min**

```bash
az storage account create \
  --name pcdfunctionstorage \
  --resource-group pcd-project-rg \
  --sku Standard_LRS \
  --location northeurope

az functionapp create \
  --name pcd-sentiment-processor \
  --resource-group pcd-project-rg \
  --storage-account pcdfunctionstorage \
  --consumption-plan-location northeurope \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4

cd src/SentimentProcessor
dotnet publish -c Release -o ./publish
cd publish && zip -r ../service-b.zip . && cd ..

az functionapp deployment source config-zip \
  --name pcd-sentiment-processor \
  --resource-group pcd-project-rg \
  --src service-b.zip

az functionapp config appsettings set \
  --name pcd-sentiment-processor \
  --resource-group pcd-project-rg \
  --settings \
    ServiceBusConnection="<sb-connection-string din .env.azure>" \
    PostgresConnection="Host=pcd-postgres-server.postgres.database.azure.com;Database=conduit;Username=pcdadmin;Password=PcdProject2026!;SslMode=Require"
```

---

## 🟡 T4 – Firewall PostgreSQL pentru Azure Services (Cosmin)

**Timp estimat: ~5 min**  
Permite App Service și Function App să acceseze PostgreSQL.

```bash
az postgres flexible-server firewall-rule create \
  --name AllowAzureServices \
  --resource-group pcd-project-rg \
  --server-name pcd-postgres-server \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0
```

---

## 🟡 T5 – Update frontend cu URL-urile Azure (Cosmin)

**Timp estimat: ~5 min**  
Fișier: `frontend/index.html`, primele 2 linii din `<script>`:

```javascript
const API_URL = 'https://pcd-realworld-api.azurewebsites.net/api';
const NOTIFICATION_URL = 'https://pcd-notification-service.azurewebsites.net';
```

---

## 🟡 T6 – Load Testing cu k6 (Cosmin)

**Timp estimat: ~45 min**

```bash
brew install k6
```

Crează fișierul `load-test/comments-load-test.js` și rulează:
```bash
k6 run --out json=results.json load-test/comments-load-test.js
```

Metrici de capturat:
- Latență end-to-end (p50, p95, p99)
- Throughput (comentarii procesate / minut)
- Error rate
- Consistency window

Salvează rezultatele în `load-test/results/` pentru raport.

---

## 🟡 T7 – Raport științific PDF (Toată echipa)

**Timp estimat: ~3-4 ore**  
Minim 2000 cuvinte, ~4-5 pagini.

### Structura raportului

1. **Introducere** – contextul proiectului, motivație
2. **Arhitectura sistemului**
   - Diagramă componente (din `docs/ARCHITECTURE.md`)
   - Descrierea fiecărui serviciu
   - Fluxurile de date
3. **Analiza comunicării**
   - Sincron vs. asincron pentru fiecare interacțiune
   - Justificare alegeri
4. **Analiza consistenței**
   - Model de consistență eventuală
   - Teorema CAP – trade-off-uri
   - Consistency window măsurată
5. **Performanță și scalabilitate**
   - Rezultate load testing (grafice latență, throughput)
   - Identificare bottleneck-uri
   - Comportament la scalare
6. **Reziliență**
   - Comportament la căderea fiecărei componente
   - Mecanisme de recuperare (retry, dead letter queue)
7. **Comparație cu sistem real**
   - Reddit/Disqus – procesare asincronă comentarii
   - Pattern-uri similare și diferențe
8. **Concluzii**
   - Ce am învățat
   - Ce am îmbunătăți
   - **Secțiune AI usage** (obligatorie) – ce instrumente AI am folosit și cum

---

## Rezumat priorități

| Prioritate | Task | Responsabil |
|---|---|---|
| 🔴 Urgent | Fix Service A – PostgreSQL support | Echipa |
| 🟡 | T1 – Deploy Service A | Cosmin |
| 🟡 | T2 – Deploy Service C | Cosmin |
| 🟡 | T3 – Deploy Service B | Cosmin |
| 🟡 | T4 – Firewall PostgreSQL | Cosmin |
| 🟡 | T5 – Update frontend URLs | Cosmin |
| 🟡 | T6 – Load testing k6 | Cosmin |
| 🟡 | T7 – Raport științific | Toată echipa |
