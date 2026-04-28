# Cum rulezi proiectul

## Ce trebuie instalat

1. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Python 3 (verifici cu `python3 --version`)

## Pași

### 1. Clonează repo-ul
```bash
git clone <url-repo>
cd PCD-Project-2026
```

### 2. Pornește toate serviciile
```bash
./start.sh
```

Scriptul pornește automat:
- **Service A** — API RealWorld pe `http://localhost:5000`
- **Service B** — Procesare sentiment (citește din Azure Service Bus, scrie în PostgreSQL)
- **Service C** — Notificări WebSocket (SignalR) pe `http://localhost:5001`
- **Frontend** — Interfața web pe `http://localhost:3000`

Așteaptă ~20 secunde până apare mesajul `GATA!`.

### 3. Deschide browserul
```
http://localhost:3000
```

### 4. Testează
1. Înregistrează-te cu email/parolă (orice valori)
2. La câmpul **Articol (slug)** scrie: `demo-pcd-2026`
3. Scrie un comentariu și apasă **Postează**
4. Comentariul apare ca `pending` — în 1-2 secunde devine `processed` cu scorul de sentiment

## Oprire

`Ctrl+C` în terminalul unde rulează `start.sh`

## Probleme frecvente

**`./start.sh`: Permission denied**
```bash
chmod +x start.sh
./start.sh
```

**Port deja ocupat**
```bash
lsof -ti:5000,5001,3000 | xargs kill -9
./start.sh
```

**Service C — SignalR DISCONNECTED în browser**
— Așteaptă 20-30 secunde, se reconectează automat. Dacă nu, oprește și repornește `./start.sh`.
