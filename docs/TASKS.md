# Task List – PCD Project 2026

## Responsabilități

| Serviciu | Responsabil | Status |
|---|---|---|
| Service A – RealWorld API (extins) | Echipă | ✅ Done |
| Service B – Azure Function (Sentiment) | Cosmin | ✅ Done |
| Service C – Notification Service (SignalR) | Cosmin | ✅ Done |
| Frontend – HTML + Vanilla JS | Cosmin | ✅ Done |
| Azure Setup (Service Bus, PostgreSQL, App Service) | Cosmin | ✅ Done |
| Load Testing (k6) | Cosmin | ✅ Done |
| Raport științific | Toată echipa | ✅ Done |

---

## Detalii task-uri

### ✅ T1 – Service A: RealWorld API extins
- [x] Fork RealWorld ASP.NET Core
- [x] Domain `Comment` extins cu `Status` și `SentimentScore`
- [x] `IMessageBus` interface + `AzureServiceBusMessageBus`
- [x] `FakeMessageBus` pentru dev local
- [x] `POST /api/articles/{slug}/comments` → salvează `pending` + publică în Service Bus
- [x] Switchare la PostgreSQL (în loc de SQLite) pentru Azure

### ✅ T2 – Service B: Azure Function – Sentiment Processor
- [x] Proiect Azure Functions (.NET 10) creat
- [x] Trigger pe coada `comments-queue` din Service Bus
- [x] Algoritm sentiment simplu (cuvinte pozitive/negative)
- [x] Actualizare `Comment` în PostgreSQL (status → `processed`, sentiment score)
- [x] Publicare eveniment în `comments-processed` queue
- [x] Idempotență (verificare dacă comentariul a fost deja procesat)

### ✅ T3 – Service C: Notification Service (SignalR)
- [x] Proiect ASP.NET Core creat în `src/NotificationService/`
- [x] SignalR Hub configurat (`CommentHub`)
- [x] Listener Azure Service Bus pe coada `comments-processed`
- [x] Push real-time către client: `{ commentId, status: "processed", sentimentScore }`
- [x] CORS configurat pentru frontend
- [x] Dockerfile creat
- [x] Deploy pe Azure App Service (după ce echipa deployează Service A)

### ✅ T4 – Frontend
- [x] Fișier `frontend/index.html` creat
- [x] Formular: selectare articol + scriere comentariu + submit
- [x] SignalR JS client conectat la Notification Service
- [x] Afișare status live: `pending → processed`
- [x] Afișare sentiment score (emoji + valoare)
- [x] Listă comentarii cu scoruri
- [x] Metrici live: latență end-to-end, throughput, sentiment mediu

### ✅ T5 – Azure Setup
- [x] Cont Azure creat (free trial)
- [x] Resource Group creat: `pcd-project-rg`
- [x] Azure Service Bus Namespace + queue `comments-queue` + queue `comments-processed`
- [x] Azure Database for PostgreSQL creat
- [x] Azure App Service Plan creat
- [x] App Service pentru Service A (RealWorld API)
- [x] App Service pentru Service C (Notification Service)
- [x] Azure Function App pentru Service B
- [x] Connection strings configurate în fiecare serviciu

### ✅ T6 – Load Testing
- [x] k6 instalat
- [x] Script `load-test/comments-load-test.js` creat
- [x] Test: 50 users concurenți, 2 minute
- [x] Metrici capturate: latență end-to-end, throughput, error rate
- [x] Grafice generate
- [x] Rezultate documentate

### ✅ T7 – Raport științific (PDF, min 2000 cuvinte)
- [x] 1. Arhitectura sistemului (diagramă Mermaid)
- [x] 2. Analiza comunicării (sincron vs. asincron)
- [x] 3. Analiza consistenței (CAP theorem, eventual consistency)
- [x] 4. Performanță și scalabilitate (grafice load test)
- [x] 5. Reziliență (comportament la căderi)
- [x] 6. Comparație cu sistem real (ex: Twitter/Reddit comments)
- [x] Secțiune AI usage (ce am folosit, cum am validat)
