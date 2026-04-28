# Shutdown – Oprire resurse Azure după predarea proiectului

Rulează comenzile de mai jos după ce proiectul a fost predat, ca să oprești toate resursele și să nu mai consumi credite Azure.

---

## Opțiunea 1 – Șterge tot (recomandat)

Șterge întregul Resource Group cu tot ce e în el (App Services, PostgreSQL, Service Bus, Function App):

```bash
az login
az group delete --name pcd-project-rg --yes --no-wait
```

> Asta șterge ireversibil tot: `pcd-realworld-api`, `pcd-notification-service`, `pcd-sentiment-processor`, `pcd-servicebus-ns`, `pcd-postgres-server`. Nu se poate recupera după.

---

## Opțiunea 2 – Oprire temporară (păstrezi datele)

Dacă vrei să oprești fără să ștergi (poți reporni mai târziu):

```bash
az login

# Oprire App Services
az webapp stop --name pcd-realworld-api --resource-group pcd-project-rg
az webapp stop --name pcd-notification-service --resource-group pcd-project-rg

# Oprire Azure Function
az functionapp stop --name pcd-sentiment-processor --resource-group pcd-project-rg

# Oprire PostgreSQL
az postgres flexible-server stop --name pcd-postgres-server --resource-group pcd-project-rg
```

> Service Bus nu are comandă de stop — se facturează per mesaj, deci dacă nu mai trimiți mesaje nu consumă aproape nimic.

---

## Checklist final

- [ ] `az group delete --name pcd-project-rg --yes` — sau toate comenzile de stop de mai sus
- [ ] Verifică în [portal.azure.com](https://portal.azure.com) că `pcd-project-rg` nu mai are resurse active
- [ ] Rotește/invalidează credențialele din `.env.azure` dacă repo-ul devine public
- [ ] Anunță echipa că resursele sunt oprite

---

## Repornire (dacă ai ales Opțiunea 2)

```bash
az webapp start --name pcd-realworld-api --resource-group pcd-project-rg
az webapp start --name pcd-notification-service --resource-group pcd-project-rg
az functionapp start --name pcd-sentiment-processor --resource-group pcd-project-rg
az postgres flexible-server start --name pcd-postgres-server --resource-group pcd-project-rg
```
