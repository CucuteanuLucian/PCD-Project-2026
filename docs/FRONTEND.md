# Frontend – Real-time Comment Processing Dashboard

## Ce face

Pagina web permite utilizatorului să:
1. Se autentifice (login / register) contra Service A (RealWorld API)
2. Selecteze un articol și posteze un comentariu
3. Vadă **în timp real** tranziția de status `pending → processed` prin SignalR
4. Vadă **scorul de sentiment** și **latența end-to-end** pentru fiecare comentariu
5. Monitorizeze metrici agregate: total postate, procesate, latență medie, sentiment mediu

---

## Tehnologii

| Tehnologie | Rol |
|---|---|
| HTML5 + CSS3 | Structură și stil, fără framework |
| Vanilla JavaScript | Logică UI, fetch API, gestionare state |
| SignalR JS Client (CDN) | Conexiune WebSocket la Notification Service |

---

## Structura fișierului `index.html`

```
index.html
├── <style>          ← CSS complet inline (fără dependențe externe)
├── UI Sections
│   ├── Auth Card    ← login / register contra RealWorld API
│   ├── Post Card    ← selectare articol + formular comentariu
│   ├── Metrics Card ← total, procesate, latență medie, sentiment mediu
│   └── Comments Card ← lista live cu status + scor + latență
└── <script>
    ├── CONFIG       ← API_URL și NOTIFICATION_URL
    ├── AUTH         ← login(), register(), logout()
    ├── ARTICLES     ← loadArticles() — populează dropdown
    ├── POST COMMENT ← postComment() — POST la Service A, adaugă în state local
    ├── SIGNALR      ← connectSignalR(), subscribeUser()
    │                   on("CommentProcessed") → actualizează UI
    ├── RENDER       ← renderComments(), updateMetrics()
    └── UTILS        ← toast, emoji sentiment, escape HTML
```

---

## Fluxul utilizatorului

```
1. User deschide index.html
2. Se loghează → primește JWT token
3. SignalR se conectează la Notification Service (/hubs/comments)
4. User invocă SubscribeToUser(userId) → intră în grupul SignalR al său
5. User selectează articol și scrie comentariu → POST /api/articles/{slug}/comments
6. Service A returnează 202 + { comment: { id, status: "pending" } }
7. Comentariul apare în UI cu badge "⏳ pending"
8. Azure Function procesează → publică în comments-processed
9. Notification Service primește evenimentul → SignalR push
10. Frontend primește "CommentProcessed" event → update UI instant
11. Badge devine "✅ processed", scor sentiment și latență afișate
```

---

## Configurare

La începutul scriptului există 2 constante de configurat:

```javascript
const API_URL = 'http://localhost:5000/api';         // Service A
const NOTIFICATION_URL = 'http://localhost:5001';    // Service C
```

Pentru producție (Azure), înlocuiește cu URL-urile App Service:
```javascript
const API_URL = 'https://pcd-realworld-api.azurewebsites.net/api';
const NOTIFICATION_URL = 'https://pcd-notification-service.azurewebsites.net';
```

---

## Cum rulezi local

Fișierul este static — nu are nevoie de server. Opțiuni:
```bash
# Opțiunea 1: direct în browser
open frontend/index.html

# Opțiunea 2: server HTTP simplu (Python)
cd frontend && python3 -m http.server 3000
# Deschide http://localhost:3000

# Opțiunea 3: VS Code Live Server extension
```

---

## Metrici capturate în UI

| Metrică | Cum se calculează |
|---|---|
| Latență end-to-end | `receivedAtMs` (din SignalR event) − `postedAtMs` (momentul POST) |
| Throughput vizual | Număr comentarii procesate / sesiune |
| Scor sentiment mediu | Media scorurilor primite prin SignalR |
| Consistency window | Vizibil direct: intervalul dintre apariția "pending" și "processed" |
