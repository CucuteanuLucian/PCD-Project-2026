# Service C – Notification Service

## Ce face

Service C este responsabil pentru **notificările în timp real** către clienți.

Ascultă evenimentele din Azure Service Bus (coada `comments-processed`) și le retransmite
imediat prin **SignalR (WebSocket)** către browser-ul utilizatorului care a postat comentariul.

---

## Flux

```
Azure Service Bus (comments-processed)
        ↓  ServiceBusListener (BackgroundService)
    deserializare CommentProcessedEvent
        ↓
    SignalR HubContext
        ↓  SendAsync("CommentProcessed", ...)
    Browser client (grupul "user-{userId}")
```

---

## Structura proiectului

```
src/NotificationService/
├── Program.cs                     ← configurare app + DI + routing
├── Hubs/
│   └── CommentHub.cs              ← Hub SignalR; clienții se abonează la grupul lor
├── Models/
│   └── CommentProcessedEvent.cs   ← modelul evenimentului primit din Service Bus
├── Services/
│   └── ServiceBusListener.cs      ← BackgroundService care ascultă Service Bus
├── appsettings.json               ← configurare (connection string Service Bus)
└── Dockerfile                     ← container pentru Azure App Service
```

---

## Componente cheie

### CommentHub (`Hubs/CommentHub.cs`)
Hub SignalR standard. Clientul apelează `SubscribeToUser(userId)` după conectare,
ceea ce îl adaugă în grupul `user-{userId}`. Notificările sunt trimise doar grupului
corespunzător — nu broadcast la toți.

### ServiceBusListener (`Services/ServiceBusListener.cs`)
`BackgroundService` care pornește odată cu aplicația și rulează permanent.
- Creează un `ServiceBusProcessor` pe coada `comments-processed`
- La fiecare mesaj: deserializează → trimite SignalR → confirmă mesajul (`Complete`)
- La eroare de procesare: `Abandon` → Service Bus retrimite automat (retry)
- La mesaj imposibil de deserializat: `DeadLetter` → nu se mai reîncearcă

### Idempotență
Mesajele confirmate (`Complete`) nu mai sunt relivrare. Mesajele abandonate sunt
relivrare de maxim `MaxDeliveryCount` ori (default 10 în Service Bus), după care
merg automat în Dead Letter Queue.

---

## Configurare

`appsettings.json`:
```json
{
  "ConnectionStrings": {
    "ServiceBus": "<connection-string-azure-service-bus>"
  },
  "AllowedOrigin": "https://<frontend-url>"
}
```

Variabile de mediu (Azure App Service):
```
ConnectionStrings__ServiceBus = <connection-string>
AllowedOrigin = https://<frontend-url>
```

---

## Endpoint-uri

| Endpoint | Descriere |
|---|---|
| `GET /health` | Health check — returnează `{ status: "healthy" }` |
| `WS /hubs/comments` | Hub SignalR — clienții se conectează aici |

---

## Evenimente SignalR

### Client → Server
```javascript
// Abonare la notificări pentru userul curent
connection.invoke("SubscribeToUser", userId);
```

### Server → Client
```javascript
// Eveniment primit când comentariul a fost procesat
connection.on("CommentProcessed", (data) => {
  // data = { commentId, status, sentimentScore, receivedAtMs }
});
```

---

## Cum rulezi local (după ce ai connection string-ul)

```bash
cd src/NotificationService
dotnet run
# Serviciul pornește pe http://localhost:5001
```

---

## Deploy Azure App Service

```bash
az webapp create \
  --name pcd-notification-service \
  --resource-group pcd-project-rg \
  --plan pcd-app-plan \
  --runtime "DOTNETCORE:10.0"

az webapp config appsettings set \
  --name pcd-notification-service \
  --resource-group pcd-project-rg \
  --settings ConnectionStrings__ServiceBus="<connection-string>"
```
