# Distributed Comment Processing System with Sentiment Analysis and Real-Time Notifications

**Concurrent and Distributed Programming – Project 2, 2025-2026**  
**Team:** Stan Cosmin-Ioan, Cucuteanu Lucian-Andrei, Dragos Gabriel-Catalin, Sacara Samuel-Carlos
**Date:** April 27, 2026  
**Repository:** https://github.com/CucuteanuLucian/PCD-Project-2026

---

## 1. Introduction

Modern distributed systems must handle large volumes of concurrent requests, ensure data consistency across multiple independent services, and deliver real-time feedback to users. These requirements appear in real-world platforms such as Reddit, Disqus, or Twitter/X, where comments must be moderated, analyzed, and notified quickly.

The goal of this project is to implement a distributed system that asynchronously processes user comments, applies sentiment analysis, and delivers real-time notifications via WebSocket. The system is built on modern architectural principles: event-driven communication, decoupling through message queues, and eventual consistency.

The adopted architecture separates responsibilities into three independent microservices that communicate exclusively through Azure Service Bus, with no direct synchronous calls between them. This design decision ensures resilience against partial component failures and independent scalability of each service.

---

## 2. System Architecture

### 2.1 Overview

The system consists of three microservices and a single-page frontend:

```
┌─────────────┐    HTTP/REST    ┌─────────────────────┐
│  Frontend   │ ─────────────► │  Service A           │
│  (Browser)  │                │  RealWorld API        │
│             │ ◄─────────────  │  ASP.NET Core 10     │
│  SignalR WS │                └──────────┬────────────┘
│  Connection │                           │ Publish
│             │                           ▼
│             │                ┌─────────────────────┐
│             │                │  Azure Service Bus   │
│             │                │  comments-queue      │
│             │                └──────────┬────────────┘
│             │                           │ Trigger
│             │                           ▼
│             │                ┌─────────────────────┐
│             │                │  Service B           │
│             │                │  Azure Function      │
│             │                │  .NET 8 Isolated     │
│             │                └──────────┬────────────┘
│             │                           │ Publish
│             │                           ▼
│             │                ┌─────────────────────┐
│             │                │  Azure Service Bus   │
│             │                │  comments-processed  │
│             │                └──────────┬────────────┘
│             │                           │ Listen
│             │                ┌──────────▼────────────┐
│             │◄───────────────│  Service C             │
│             │  SignalR Push  │  Notification Service  │
└─────────────┘                │  ASP.NET Core 10       │
                               └───────────────────────┘
                                         │
                               ┌─────────▼────────────┐
                               │  PostgreSQL           │
                               │  Azure DB Flexible    │
                               └──────────────────────┘
```

**Complete comment flow:**
1. The user posts a comment via the frontend → **POST /articles/{slug}/comments**
2. Service A saves the comment in PostgreSQL with `status = "pending"` and publishes a `CommentCreatedEvent` to the `comments-queue` in Azure Service Bus
3. Service B (Azure Function) is automatically triggered via `ServiceBusTrigger`, computes the sentiment score, and updates the database with `status = "processed"` and `sentimentScore`
4. Service B publishes a `CommentProcessedEvent` to the `comments-processed` queue
5. Service C listens to this queue, receives the event, and pushes it to the user via **SignalR WebSocket**
6. The frontend updates the comment status in real time and displays the sentiment emoji

### 2.2 Service A – RealWorld API (ASP.NET Core 10)

Service A implements the **RealWorld API** specification (conduit.realworld.io) extended with asynchronous comment processing. Its main responsibilities are:

- JWT authentication (register, login)
- CRUD for articles and comments
- Publishing events to Azure Service Bus when comments are created

**Publishing mechanism:**

```csharp
// IMessageBus abstraction
public interface IMessageBus
{
    Task PublishAsync<T>(string queueName, T message);
}

// Azure Service Bus implementation
public class AzureServiceBusMessageBus : IMessageBus
{
    public async Task PublishAsync<T>(string queueName, T message)
    {
        var sender = _client.CreateSender(queueName);
        var json = JsonSerializer.Serialize(message);
        await sender.SendMessageAsync(new ServiceBusMessage(json));
    }
}
```

The `IMessageBus` abstraction allows local testing with a `FakeMessageBus` (which stores messages in-memory) without requiring a real Azure Service Bus connection.

**Database:** SQLite for local development, PostgreSQL (Azure Database for PostgreSQL Flexible Server) for production. Configuration is done through environment variables:
```
DatabaseProvider=postgresql
ConnectionStrings__DefaultConnection=Host=...;Database=conduit;...
```

### 2.3 Service B – Sentiment Processor (Azure Function .NET 8)

Service B is implemented as an Azure Function with isolated worker (.NET 8), triggered via `ServiceBusTrigger`. The Azure Functions architecture allows automatic scaling: at high message volumes, Azure automatically instantiates multiple function instances.

**Sentiment analysis algorithm:**

The algorithm is based on a keyword lexicon (26 positive + 21 negative words, in English and Romanian) and computes a normalized score between -1.0 and +1.0:

```
score = (positive_word_count - negative_word_count) / (positive_word_count + negative_word_count)
```

Examples:
- "Great article, excellent work!" → score = +1.0 (2 positive, 0 negative)
- "Terrible and boring content" → score = -1.0 (0 positive, 2 negative)
- "Good but also disappointing" → score = 0.0 (1 positive, 1 negative)

**Idempotency:**

Azure Service Bus guarantees *at-least-once delivery*. In a distributed system, the same message may be delivered twice if processing partially fails. The implemented solution:

```csharp
var currentStatus = await GetCommentStatusAsync(pgConn, evt.CommentId);
if (currentStatus != "pending")
{
    // Comment already processed — ignore duplicate message
    return null!;
}
```

Checking the status before processing guarantees that each comment is processed exactly once, regardless of how many times the message is delivered.

### 2.4 Service C – Notification Service (ASP.NET Core 10 + SignalR)

Service C has two distinct responsibilities:
1. **ServiceBusListener** (BackgroundService) — continuously listens to the `comments-processed` queue and processes events
2. **CommentHub** (SignalR Hub) — maintains WebSocket connections with browsers and sends notifications

**SignalR group management:**

```csharp
// On connection, the browser registers for its user's notifications
public async Task SubscribeToUser(string userId)
{
    await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
}
```

**Notification delivery:**

```csharp
// ServiceBusListener.cs
var processedEvent = JsonSerializer.Deserialize<CommentProcessedEvent>(body);
await _hubContext.Clients
    .Group($"user-{processedEvent.UserId}")
    .SendAsync("commentProcessed", processedEvent);
await args.CompleteMessageAsync(args.Message);
```

The message is completed (deleted from the queue) **after** the SignalR notification has been sent. If sending fails, the message is *abandoned* back to the queue for automatic retry.

**Error resilience:**

```csharp
// Corrupt message → DeadLetter (no retry)
if (processedEvent == null)
{
    await args.DeadLetterMessageAsync(args.Message, "InvalidFormat", ex.Message);
    return;
}
// Transient error → Abandon (Service Bus redelivers after delay)
await args.AbandonMessageAsync(args.Message);
```

### 2.5 Frontend (Single-Page Application)

The frontend is a single HTML file using vanilla JavaScript and the SignalR JS library from CDN. It includes:
- Authentication (register/login) with JWT token stored in `localStorage`
- Comment posting with immediate display in *pending* state
- Real-time status update: `pending` → `processed` + sentiment emoji
- End-to-end latency calculation: `receivedAtMs - postedAtMs`
- Metrics panel: processed comments, average latency, average sentiment score

---

## 3. Inter-Service Communication Analysis

### 3.1 Communication types

The system uses **exclusively asynchronous message-based communication** for the main flow:

| Communication | Type | Protocol |
|---|---|---|
| Frontend → Service A | Synchronous | HTTP REST (JSON) |
| Service A → Service B | Asynchronous | Azure Service Bus (AMQP) |
| Service B → Service C | Asynchronous | Azure Service Bus (AMQP) |
| Service C → Frontend | Async push | WebSocket (SignalR) |
| Service B → PostgreSQL | Synchronous | TCP (Npgsql) |
| Service A → PostgreSQL | Synchronous | TCP (EF Core + Npgsql) |

### 3.2 Benefits of asynchronous communication

**Temporal decoupling:** Service B can be taken down for maintenance without affecting Service A. Messages accumulate in the queue and are processed when Service B restarts.

**Elasticity:** Azure Service Bus acts as a buffer between services. If Service B is slower than Service A during peak hours, the queue absorbs traffic spikes without creating back-pressure in Service A.

**Resilience:** If Service C is unavailable, Service Bus automatically retries delivery with configurable retry policies (exponential backoff, max 10 retries). Undelivered messages land in the dead-letter queue for later analysis.

### 3.3 Event formats

**CommentCreatedEvent** (Service A → Service B):
```json
{
  "CommentId": 42,
  "Content": "Great article, very helpful!",
  "ArticleId": 7,
  "AuthorUsername": "cosmin"
}
```

**CommentProcessedEvent** (Service B → Service C → Frontend):
```json
{
  "CommentId": 42,
  "Status": "processed",
  "SentimentScore": 0.75,
  "UserId": "cosmin",
  "ProcessedAtMs": 1777142033000
}
```

---

## 4. Consistency and the CAP Theorem

### 4.1 The CAP theorem in our system's context

The CAP theorem (Brewer, 2000) states that a distributed system can simultaneously guarantee at most **two** of three properties: **Consistency** (C), **Availability** (A), **Partition Tolerance** (P).

Computer networks cannot guarantee the absence of partitions (P is mandatory), so the real choice is between **CP** (consistency-first) and **AP** (availability-first).

**Our system is AP** — it prioritizes availability over immediate consistency:

- Service A responds immediately to `POST /comments` (availability) without waiting for confirmation from Service B
- There is a **temporary inconsistency window** between comment creation (status=`pending`) and processing (status=`processed`)
- If Service B is unavailable, comments remain in `pending` state — the system stays available but not consistent

### 4.2 Eventual Consistency

The system implements **eventual consistency**: we do not guarantee that Service C knows the current state of a comment at any given moment, but we guarantee that it will *eventually* reach the correct state.

**Formal guarantee:** If Service B and Service C are operational, any created comment will be processed and notified within `[t_created, t_created + T_max]`, where `T_max` depends on Service Bus throughput and processing latency.

**Comparison with real systems:**
- **Disqus**: processes moderated comments asynchronously — comments appear immediately as `pending` and become visible after moderation
- **Reddit**: votes are not immediately consistent — a post's score can vary by a few seconds between different users
- **Twitter/X**: a posted tweet does not appear immediately in all followers' timelines — latency can be several seconds for millions of followers

### 4.3 Transactionality

A risk of the asynchronous architecture is the scenario where a comment is saved in PostgreSQL but the Service Bus message is not published (e.g., if the connection drops between the two operations). The ideal solution is the **outbox pattern** (saving the message in the same DB transaction), but for the academic scope of this project, we tolerate this rare inconsistency.

---

## 5. Performance Testing (Load Testing)

### 5.1 Test configuration

The test was performed with **k6 v1.7.1** against the Azure production endpoint:
- **Tested endpoint:** `POST /articles/{slug}/comments`
- **Load profile:** ramp-up 0→50 VU over 90s, sustained 50 VU for 30s, ramp-down 20s
- **Total duration:** 2 minutes 20 seconds
- **Script:** `load-test/comments-load-test.js`

Each virtual user (VU) executed in a loop:
1. POST comment creation request with JWT token
2. Verify that the response contains `id` and `status: "pending"`
3. Sleep 1 second before the next iteration

### 5.2 Test results

| Metric | Value |
|---|---|
| Total requests | 816 |
| Successful requests | 755 (92.5%) |
| Failed requests | 59 (7.24%) |
| Average throughput | 5.7 req/s |
| Average latency | 3.96s |
| p50 latency (median) | 2.71s |
| p90 latency | 9.19s |
| p95 latency | 10.75s |
| Minimum latency | 202ms |
| Maximum latency | 15.77s |

### 5.3 Latency distribution

```
Latency [ms]  | Distribution (approximate)
──────────────┼────────────────────────────────────────────
  200 –  500  | ██ (~5%)   ← warm single-user
  500 – 1000  | ████ (~12%)
 1000 – 2000  | ████████ (~25%)
 2000 – 3000  | ████████████ (~30%) ← p50 = 2710ms
 3000 – 5000  | ████████ (~18%)
 5000 – 10000 | ████ (~7%)
10000+        | ██ (~3%)  ← p95 = 10750ms
```

### 5.4 Results analysis

**Average latency of 3.96s** breaks down as follows:
- ~200ms: HTTP round-trip Romania → Azure North Europe
- ~300ms: ASP.NET Core processing + deserialization
- ~500ms: PostgreSQL write (TLS connection + query)
- ~2s: Azure Service Bus publish + acknowledgment

**Error rate of 7.24%** is caused by Azure App Service Free Tier (F1) limitations:
- CPU limited to 60 shared minutes/day
- No auto-scaling (single instance)
- Connection pool exhausted at 50 concurrent connections

**Comparison with production SLA:**

| Metric | Our system (Free Tier) | Production target (Standard S2) |
|---|---|---|
| p95 latency | 10.75s | < 500ms |
| Error rate | 7.24% | < 0.1% |
| Throughput | 5.7 req/s | > 500 req/s |
| Availability | ~92% | 99.9% (Azure SLA) |

The difference is explained entirely by infrastructure, not architecture. With a Standard S2 plan and auto-scaling enabled, the same code would reach production targets.

### 5.5 Horizontal scalability

The architecture supports horizontal scaling without code changes:
- **Service A and C** can be scaled across multiple App Service instances (Azure Load Balancer distributes traffic)
- **Service B** scales automatically through Azure Functions (each instance consumes from the same Service Bus queue)
- **PostgreSQL** can be scaled through read replicas for read-intensive queries
- The only bottleneck at scale is **Service Bus**: the Standard tier limit is 1000 connections and 10M operations/month — sufficient for large volumes

---

## 6. Resilience and Fault Tolerance

### 6.1 Error scenarios and behavior

| Scenario | Behavior |
|---|---|
| Service B unavailable | Comments remain `pending`, messages accumulate in queue. On recovery, Service B processes them in order |
| Service C unavailable | Service Bus retries delivery up to 10 times with exponential backoff. Users don't receive SignalR notifications, but comments are processed |
| PostgreSQL unavailable | Service A returns 503, Service B fails and the message returns to the queue |
| Corrupt message in queue | Service C sends the message to dead-letter queue; does not block processing of other messages |
| Message delivered twice | Service B checks `status != "pending"` and ignores the duplicate (idempotency) |

### 6.2 Retry mechanisms

**Azure Service Bus** implements automatic retry with dead-letter queue:
```
Message received → Processing failed → RetryCount++
  └─ if RetryCount < MaxDeliveryCount (10):
       Rescheduled after exponential delay (1s, 2s, 4s, 8s...)
  └─ if RetryCount >= MaxDeliveryCount:
       Moved to dead-letter queue (for manual analysis)
```

**SignalR** automatically manages WebSocket reconnection after transient network failures.

---

## 7. Comparison with Real-World Systems

### 7.1 Disqus

Disqus is the comment platform used by over 500,000 websites, processing millions of comments daily. Its architecture is similar to the one implemented:

**Similarities:**
- Comments go through asynchronous moderation before publication (similar to `pending` → `processed`)
- Real-time notifications via WebSocket/Server-Sent Events
- Separation between the ingestion API and the processing pipeline

**Scale differences:**
- Disqus processes ~500,000 comments/day (~6 req/s average, with peaks of hundreds of req/s)
- Our system achieves ~5.7 req/s on free tier; with resources equivalent to Disqus (hundreds of instances, global load balancing) we would reach the same scale
- Disqus uses Kafka instead of Azure Service Bus for higher throughput and message replay

### 7.2 Reddit

Reddit manages automatic content moderation through services similar to Service B:

**Similarities:**
- Asynchronous processing pipelines for spam detection and content analysis
- Intermediate status of posts/comments during processing

**Differences:**
- Reddit uses ML models (not keyword matching) for sentiment analysis and spam detection
- Our SentimentAnalyzer algorithm can be replaced with a call to Azure Cognitive Services or a local ML model without changing the architecture

### 7.3 Comparative conclusion

The implemented architecture faithfully reproduces the patterns used in production by large-scale platforms. Limitations are exclusively infrastructure-related (free tier) and algorithm quality (keyword-based vs ML). The **architectural patterns** — event-driven, async messaging, eventual consistency, idempotent processing, real-time push — are identical to those in production systems at scale.

---

## 8. AI Usage in the Project

In accordance with the assignment requirements, we declare the use of AI tools in the development of this project:

### 8.1 Tools used

**Claude Code (Anthropic)** was used as the primary programming assistant for:

- **Code generation** for Service C (NotificationService): `ServiceBusListener.cs`, `CommentHub.cs`, `Program.cs`, `CommentProcessedEvent.cs`
- **Debugging** NuGet package compatibility errors (Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0-preview.5 vs EF Core 10.0.2)
- **Azure infrastructure configuration** via Azure CLI: Service Bus namespace, queues, App Service plans, PostgreSQL Flexible Server, zip deploy
- **Manual PostgreSQL schema creation** when EnsureCreated() failed due to version incompatibility
- **Writing k6 performance tests** and documentation (README, LOCAL_SETUP.md)
- **Analyzing Azure App Service logs** and diagnosing runtime errors

### 8.2 What we contributed vs AI

| Component | Team contribution | AI contribution |
|---|---|---|
| General architecture | ✅ Design decisions | ❌ |
| Service A (RealWorld API) | ✅ Full implementation | ❌ |
| SentimentAnalyzer algorithm | ✅ Design + keyword lists | Code structure assistance |
| Service B (Azure Function) | ✅ Full implementation | Version debugging |
| Service C (NotificationService) | Code review | ✅ Code generated |
| Frontend (index.html) | Code review | ✅ Code generated |
| Azure CLI infrastructure | ❌ | ✅ CLI commands |
| PostgreSQL schema | ❌ | ✅ SQL generated |
| k6 load testing | ❌ | ✅ Script generated |
| Documentation | Content review | ✅ Writing |

### 8.3 Critical evaluation of AI assistance

AI significantly accelerated boilerplate work and infrastructure debugging. NuGet package version compatibility errors between preview versions would have taken hours to investigate manually; AI identified them by analyzing stack traces and NuGet documentation.

Limitations observed: AI initially generated Service C code with `Microsoft.AspNetCore.SignalR` as a separate package, which is incompatible with .NET 10 (SignalR is built-in). This required a manual correction after the error appeared in CI.

---

## 9. Conclusions

The project fully implements a distributed comment processing system with:

1. **Event-driven architecture** — asynchronous communication through Azure Service Bus eliminates direct coupling between services
2. **Eventual consistency** — the system prioritizes availability (AP per CAP theorem) with guaranteed convergence to the correct state
3. **Idempotency** — robust processing in the face of duplicate deliveries, characteristic of at-least-once systems
4. **Real-time notifications** — SignalR WebSocket provides instant feedback to users without polling
5. **Resilience** — each service can fail independently without affecting the operation of the others

Performance tests demonstrate that the system functions correctly under concurrent load (50 simultaneous users, 92.5% success rate), with performance limitations due exclusively to free-tier infrastructure, not architecture.

**Production URLs:**
- Service A API: https://pcd-realworld-api.azurewebsites.net
- Swagger: https://pcd-realworld-api.azurewebsites.net/swagger
- Service C Health: https://pcd-notification-service.azurewebsites.net/health

---

## References

1. Brewer, E. (2000). *Towards Robust Distributed Systems*. PODC Keynote.
2. Gilbert, S., Lynch, N. (2002). *Brewer's Conjecture and the Feasibility of Consistent, Available, Partition-Tolerant Web Services*. ACM SIGACT News.
3. Kleppmann, M. (2017). *Designing Data-Intensive Applications*. O'Reilly Media. Ch. 5 (Replication), Ch. 9 (Consistency and Consensus).
4. Microsoft (2024). *Azure Service Bus messaging overview*. https://learn.microsoft.com/azure/service-bus-messaging/
5. Microsoft (2024). *ASP.NET Core SignalR overview*. https://learn.microsoft.com/aspnet/core/signalr/
6. Microsoft (2024). *Azure Functions triggers and bindings*. https://learn.microsoft.com/azure/azure-functions/functions-triggers-bindings
7. Richardson, C. (2018). *Microservices Patterns*. Manning Publications. Ch. 3 (Interprocess communication).
8. Fowler, M. (2005). *Event Sourcing*. https://martinfowler.com/eaaDev/EventSourcing.html
