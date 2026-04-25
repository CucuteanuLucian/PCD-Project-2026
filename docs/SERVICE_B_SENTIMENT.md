# Service B – Sentiment Processor (Azure Function)

## Ce face

Azure Function declanșată automat de mesajele din coada `comments-queue`.
Analizează textul comentariului, calculează un scor de sentiment, actualizează
baza de date PostgreSQL și publică rezultatul în coada `comments-processed`.

---

## Flux

```
Azure Service Bus (comments-queue)
        ↓  ServiceBusTrigger [automat]
    CommentProcessorFunction.Run()
        ↓
    Idempotență: verifică dacă status != "pending" → skip dacă da
        ↓
    SentimentAnalyzer.Analyze(text) → scor [-1.0 ... +1.0]
        ↓
    UPDATE Comments SET Status='processed', SentimentScore=@score
        ↓
    ServiceBusOutput → publică în comments-processed
```

---

## Structura proiectului

```
src/SentimentProcessor/
├── Program.cs                          ← host configuration (minimal)
├── Functions/
│   └── CommentProcessorFunction.cs     ← trigger + logică principală
├── Services/
│   └── SentimentAnalyzer.cs            ← algoritm sentiment keyword-based
├── Models/
│   ├── CommentCreatedEvent.cs          ← eveniment primit din comments-queue
│   └── CommentProcessedEvent.cs        ← eveniment publicat în comments-processed
├── host.json                           ← configurare Azure Functions host
└── local.settings.json                 ← config locală (exclus din git)
```

---

## Algoritmul de sentiment

`SentimentAnalyzer.Analyze(text)` returnează un scor între **-1.0** și **+1.0**:

| Scor | Interpretare |
|---|---|
| +1.0 | Complet pozitiv |
| 0.0 | Neutru / fără cuvinte cheie |
| -1.0 | Complet negativ |

**Formula:** `(pozitive - negative) / (pozitive + negative)`

Dicționar: 26 cuvinte pozitive + 21 cuvinte negative, în engleză și română.

---

## Idempotență

Înainte de procesare, funcția verifică statusul curent al comentariului în DB:
- Dacă `status != "pending"` → comentariul a fost deja procesat → skip
- Dacă `status == "pending"` → procesează normal

Asta protejează împotriva relivrărilor duplicate din Service Bus (at-least-once delivery).

---

## Configurare

`local.settings.json` (local, exclus din git):
```json
{
  "Values": {
    "ServiceBusConnection": "<connection-string-service-bus>",
    "PostgresConnection": "<connection-string-postgres>"
  }
}
```

Azure Function App — App Settings:
```
ServiceBusConnection = <connection-string>
PostgresConnection   = Host=pcd-postgres-server.postgres.database.azure.com;Database=conduit;Username=pcdadmin;Password=PcdProject2026!;SslMode=Require
```

---

## Deploy Azure Function App

```bash
# Creare Function App
az functionapp create \
  --name pcd-sentiment-processor \
  --resource-group pcd-project-rg \
  --storage-account pcdfunctionstorage \
  --consumption-plan-location northeurope \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4

# Configurare settings
az functionapp config appsettings set \
  --name pcd-sentiment-processor \
  --resource-group pcd-project-rg \
  --settings \
    ServiceBusConnection="<sb-connection-string>" \
    PostgresConnection="<pg-connection-string>"

# Deploy cod
cd src/SentimentProcessor
dotnet publish -c Release -o ./publish
cd publish && zip -r ../function.zip .
az functionapp deployment source config-zip \
  --name pcd-sentiment-processor \
  --resource-group pcd-project-rg \
  --src ../function.zip
```
