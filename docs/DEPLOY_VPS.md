# Развёртывание MRS на VPS (для удалённых инженеров)

Инструкция для сценария: **инженер в другом городе**, обновляет данные с **мобильного интернета**.

Приложение на телефоне подключается к **MRS.Api** по публичному адресу (`https://api.ваш-домен.ru`).  
PostgreSQL работает **только на сервере** — с телефона к нему напрямую никто не ходит.

---

## Что понадобится

| Что | Зачем |
|-----|--------|
| VPS в облаке | Постоянно включённый сервер в интернете |
| Домен (желательно) | Удобный адрес и HTTPS для телефонов |
| 1–2 часа на первую настройку | Один раз |

**Минимальные характеристики VPS:** 1 vCPU, 2 GB RAM, 20 GB SSD, Ubuntu 22.04 или 24.04.

Подойдут, например: [Timeweb Cloud](https://timeweb.cloud), [Selectel](https://selectel.ru), [Yandex Cloud](https://cloud.yandex.ru).

---

## Схема

```
Телефон инженера (LTE/4G)
        │
        ▼  HTTPS
   api.ваш-домен.ru  (Nginx + Let's Encrypt)
        │
        ▼
   MRS.Api :5080  (Docker)
        │
        ▼
   PostgreSQL  (Docker, только внутри сервера)
```

---

## Шаг 1. Создать VPS

1. Зарегистрируйтесь у провайдера.
2. Создайте сервер: **Ubuntu 24.04 LTS**, регион ближе к вам/клиентам.
3. Запишите **публичный IP** сервера (например `185.12.34.56`).

Подключение с вашего ПК (PowerShell):

```powershell
ssh root@185.12.34.56
```

(Логин может быть `root` или `ubuntu` — смотрите письмо от провайдера.)

---

## Шаг 2. Домен (рекомендуется)

Без домена можно временно использовать IP (`http://185.12.34.56`), но:

- на Android сложнее с HTTP (ограничения безопасности);
- адрес неудобно менять.

1. Купите домен (Reg.ru, Timeweb и т.п.).
2. В DNS создайте запись **A**:

| Имя | Тип | Значение |
|-----|-----|----------|
| `api` | A | `185.12.34.56` |

Проверка (с вашего ПК, через 5–30 минут):

```powershell
nslookup api.ваш-домен.ru
```

Должен показать IP вашего VPS.

---

## Шаг 3. Подготовить сервер

На VPS (под root или через `sudo`):

```bash
apt update && apt upgrade -y
apt install -y git docker.io docker-compose-v2 nginx certbot python3-certbot-nginx ufw

systemctl enable docker
systemctl start docker

ufw allow OpenSSH
ufw allow 'Nginx Full'
ufw --force enable
```

---

## Шаг 4. Загрузить проект на VPS

**Вариант А — через git** (если репозиторий на GitHub/GitLab):

```bash
cd /opt
git clone https://github.com/ВАШ_АККАУНТ/MRS.git
cd MRS
```

**Вариант Б — скопировать с вашего ПК** (если git нет):

На Windows (PowerShell):

```powershell
scp -r C:\Development\MRS root@185.12.34.56:/opt/MRS
```

На VPS:

```bash
cd /opt/MRS
```

---

## Шаг 5. Секреты и запуск

```bash
cd /opt/MRS
cp .env.example .env
nano .env
```

Задайте **свои** значения:

```env
POSTGRES_PASSWORD=длинный-случайный-пароль
JWT_KEY=случайная-строка-минимум-32-символа-для-подписи-токенов
```

Собрать и запустить:

```bash
docker compose -f docker-compose.prod.yml up -d --build
```

Проверка (на VPS):

```bash
curl http://127.0.0.1:5080/api/health
```

Ожидаемый ответ: `{"status":"ok",...}`

Логи при проблемах:

```bash
docker compose -f docker-compose.prod.yml logs -f api
```

При первом запуске API **сам создаст** таблицы PostgreSQL и демо-пользователя `demo` / `demo123`.

---

## Шаг 6. HTTPS (Nginx + бесплатный сертификат)

```bash
cp /opt/MRS/deploy/nginx-mrs-api.conf /etc/nginx/sites-available/mrs-api
nano /etc/nginx/sites-available/mrs-api
```

Замените `api.ваш-домен.ru` на ваш реальный поддомен.

```bash
ln -s /etc/nginx/sites-available/mrs-api /etc/nginx/sites-enabled/
rm -f /etc/nginx/sites-enabled/default
nginx -t && systemctl reload nginx

certbot --nginx -d api.ваш-домен.ru
```

Проверка с любого устройства в интернете:

```text
https://api.ваш-домен.ru/api/health
```

---

## Шаг 7. Прописать адрес в мобильном приложении

Файл: `src/MRS.Maui/Services/MauiSyncDefaults.cs`

```csharp
public const string ProductionServerUrl = "https://api.ваш-домен.ru";
```

Пересоберите APK (Release) и раздайте инженерам.

**Важно:** после смены URL нужна **новая сборка** приложения.

---

## Шаг 8. Проверка с телефона

1. На телефоне **отключите Wi‑Fi**, оставьте мобильный интернет.
2. В браузере откройте `https://api.ваш-домен.ru/api/health`.
3. В приложении MRS: меню → **«Обновить с сервера»**.

---

## Обслуживание

### Обновить API после изменений в коде

На VPS:

```bash
cd /opt/MRS
git pull          # или снова scp с ПК
docker compose -f docker-compose.prod.yml up -d --build
```

### Резервная копия PostgreSQL

```bash
docker exec mrs-postgres pg_dump -U mrs mrs > backup_$(date +%Y%m%d).sql
```

Восстановление:

```bash
cat backup_20260707.sql | docker exec -i mrs-postgres psql -U mrs mrs
```

### Автозапуск

Docker с `restart: unless-stopped` поднимает контейнеры после перезагрузки VPS.

---

## Безопасность (обязательно для продакшена)

1. **Смените** пароли в `.env` — не оставляйте `mrs` / dev-ключ из `appsettings.json`.
2. PostgreSQL **не открывайте** в интернет (в `docker-compose.prod.yml` порт наружу не проброшен — так и должно быть).
3. Позже замените встроенную учётку `demo`/`demo123` в приложении на отдельного технического пользователя синхронизации.
4. Регулярно делайте `apt upgrade` на VPS.

---

## Частые вопросы

### Нужен ли включённый домашний ПК?

**Нет.** После переноса на VPS сервер работает 24/7 в облаке. Домашний компьютер нужен только для разработки.

### Можно ли без домена, только по IP?

Можно для теста (`http://IP:5080`), но для телефонов лучше домен + HTTPS.

### Что если инженер без интернета?

Приложение работает **офлайн** с локальной SQLite. При появлении сети — «Обновить с сервера».

### Сколько стоит VPS?

Ориентир: от **300–700 ₽/мес** за минимальный тариф у российских провайдеров (цены меняются).

---

## Чеклист

- [ ] VPS создан, SSH работает
- [ ] DNS: `api.ваш-домен.ru` → IP сервера
- [ ] `docker compose -f docker-compose.prod.yml up -d --build`
- [ ] `curl https://api.ваш-домен.ru/api/health` — ok
- [ ] `ProductionServerUrl` в `MauiSyncDefaults.cs` обновлён
- [ ] Release APK пересобран и проверен с мобильного интернета
