# Task List – PCD Project 2026

## Responsabilități

| Serviciu | Responsabil | Status |
|---|---|---|
| Service A – RealWorld API (extins) | Echipă | ✅ Done |
| Service B – Azure Function (Sentiment) | Cosmin | ✅ Done |
| Service C – Notification Service (SignalR) | Cosmin | ✅ Done |
| Frontend – HTML + Vanilla JS | Cosmin | ✅ Done |
| Azure Setup (Service Bus, PostgreSQL, App Service) | Cosmin | ✅ Done (parțial) |
| Load Testing (k6) | Cosmin | 🔲 Todo |
| Raport științific | Toată echipa | 🔲 Todo |

---

## Detalii task-uri

### ✅ T1 – Service A: RealWorld API extins
- [x] Fork RealWorld ASP.NET Core
- [x] Domain `Comment` extins cu `Status` și `SentimentScore`
- [x] `IMessageBus` interface + `AzureServiceBusMessageBus`
- [x] `FakeMessageBus` pentru dev local
- [x] `POST /api/articles/{slug}/comments` → salvează `pending` + publică în Service Bus
- [ ] Endpoint `GET /api/articles/{slug}/comments/{id}` → returnează status + sentiment score
- [ ] Switchare la PostgreSQL (în loc de SQLite) pentru Azure

### ⏳ T2 – Service B: Azure Function – Sentiment Processor
- [ ] Proiect Azure Functions (.NET 10) creat
- [ ] Trigger pe coada `comments-queue` din Service Bus
- [ ] Algoritm sentiment simplu (cuvinte pozitive/negative)
- [ ] Actualizare `Comment` în PostgreSQL (status → `processed`, sentiment score)
- [ ] Publicare eveniment în `comments-processed` queue
- [ ] Idempotență (verificare dacă comentariul a fost deja procesat)

### ✅ T3 – Service C: Notification Service (SignalR)
- [x] Proiect ASP.NET Core creat în `src/NotificationService/`
- [x] SignalR Hub configurat (`CommentHub`)
- [x] Listener Azure Service Bus pe coada `comments-processed`
- [x] Push real-time către client: `{ commentId, status: "processed", sentimentScore }`
- [x] CORS configurat pentru frontend
- [x] Dockerfile creat
- [ ] Deploy pe Azure App Service (după ce echipa deployează Service A)

### ✅ T4 – Frontend
- [x] Fișier `frontend/index.html` creat
- [x] Formular: selectare articol + scriere comentariu + submit
- [x] SignalR JS client conectat la Notification Service
- [x] Afișare status live: `pending → processed`
- [x] Afișare sentiment score (emoji + valoare)
- [x] Listă comentarii cu scoruri
- [x] Metrici live: latență end-to-end, throughput, sentiment mediu

### 🔲 T5 – Azure Setup
- [ ] Cont Azure creat (free trial)
- [ ] Resource Group creat: `pcd-project-rg`
- [ ] Azure Service Bus Namespace + queue `comments-queue` + queue `comments-processed`
- [ ] Azure Database for PostgreSQL creat
- [ ] Azure App Service Plan creat
- [ ] App Service pentru Service A (RealWorld API)
- [ ] App Service pentru Service C (Notification Service)
- [ ] Azure Function App pentru Service B
- [ ] Connection strings configurate în fiecare serviciu

### 🔲 T6 – Load Testing
- [ ] k6 instalat
- [ ] Script `load-test/comments-load-test.js` creat
- [ ] Test: 50 users concurenți, 2 minute
- [ ] Metrici capturate: latență end-to-end, throughput, error rate
- [ ] Grafice generate
- [ ] Rezultate documentate

### 🔲 T7 – Raport științific (PDF, min 2000 cuvinte)
- [ ] 1. Arhitectura sistemului (diagramă Mermaid)
- [ ] 2. Analiza comunicării (sincron vs. asincron)
- [ ] 3. Analiza consistenței (CAP theorem, eventual consistency)
- [ ] 4. Performanță și scalabilitate (grafice load test)
- [ ] 5. Reziliență (comportament la căderi)
- [ ] 6. Comparație cu sistem real (ex: Twitter/Reddit comments)
- [ ] Secțiune AI usage (ce am folosit, cum am validat)
