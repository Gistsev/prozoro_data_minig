# Prozorro Data Analyzing
Тестове завдання: сервіс для асинхронного збору й аналітики закупівель Prozorro по CPV `09310000-5` (електрична енергія).

Імпорт виконується у фоновому режимі через `BackgroundService`, тому HTTP API не блокується під час тривалого ETL процесу.

## Stack

- .NET 8 Minimal API
- PostgreSQL 16
- Dapper + Npgsql
- React + Vite
- Docker Compose

## Як запустити

```bash
docker compose up --build
```

Після старту:

- API: http://localhost:8080/swagger
- Dashboard: http://localhost:5173
- PostgreSQL: localhost:5432, database `prozorro_analytics`, user/password `postgres/postgres`

## Основні endpoint-и

### Запустити імпорт

### Запустити імпорт

```http
POST /api/import
```

Повертає:

```http
202 Accepted
```

та ставить задачу в чергу.

Для відстеження прогресу використовується

```http
GET /api/import/status
```

Приклад відповіді:

```json
{
  "jobId": "...",
  "status": "Running",
  "result": {
    "scanned": 1250,
    "matched": 17,
    "saved": 17
  },
  "error": null
}
```

### Отримати аналітику

```http
GET /api/analytics/summary
```

Повертає:

```json
{
  "totalSaving": 1234567.89,
  "topBuyers": [{ "name": "...", "amount": 1000000 }],
  "topSuppliers": [{ "name": "...", "amount": 1000000 }]
}
```

## Архітектура

Проєкт побудований на основі багатошарової архітектури. 
Довготривалі операції імпорту виконуються асинхронно у фоновому режимі за допомогою патерна Producer–Consumer та BackgroundService, використовуючи System.Threading.
Channels як чергу повідомлень. 
Процес імпорту реалізовано у вигляді ETL-конвеєра (Extract → Transform → Load): дані завантажуються з Prozorro API, перетворюються у доменну модель та зберігаються в PostgreSQL.


                    React Dashboard
                           │
                           ▼
                  Minimal API (.NET 8)
                    │             │
                    │             ▼
                    │      ImportJobQueue
                    │             │
                    ▼             ▼
          AnalyticsEndpoints  BackgroundService
                    │             │
                    └──────┬──────┘
                           ▼
                     ImportService
                     │     │      │
                     ▼     ▼      ▼
              Prozorro  Parser  Repository
                           │
                           ▼
                      PostgreSQL

src
├── Background
│   ├── ImportBackgroundService
│   ├── ImportJobQueue
│   ├── ImportJobState
│   └── ImportJobHandler
│
├── Endpoints
│   ├── ImportEndpoints
│   └── AnalyticsEndpoints
│
├── Integration
│   └── ProzorroClient
│
├── Parsing
│   └── TenderParser
│
├── Persistence
│   ├── Db
│   └── TenderRepository
│
├── Domain
│
└── Program.cs

## Background processing

Імпорт запускається асинхронно через `BackgroundService`.

Після виклику `POST /api/import` API одразу повертає `202 Accepted`, а сама задача потрапляє в bounded `Channel<T>`.

HTTP Request
│
▼
ImportJobQueue
│
▼
BackgroundService
│
▼
ImportService
│
▼
Prozorro API
│
▼
PostgreSQL

Переваги:

- HTTP-запит не блокується.
- Є захист від одночасного запуску декількох імпортів.
- Обмежена черга (`Channel<T>`).
- Контрольований рівень паралелізму під час завантаження деталей тендерів.

## High load
- Асинхронний ETL pipeline.
- Background queue на базі `System.Threading.Channels`.
- `BackgroundService` для виконання довготривалих задач.
- Producer/Consumer architecture.
- Bounded channel забезпечує backpressure та запобігає надмірному використанню пам'яті.
- Паралельне завантаження деталей тендерів із контрольованою concurrency.

## Чому Dapper

Для цього завдання важливо показати контроль над SQL, індексами та агрегатами. Dapper дає менше overhead, ніж EF Core, і добре підходить для ETL/аналітичних запитів.

## Схема БД

### `tenders`

Основна таблиця закупівель.

Ключові поля:

- `id` — primary key Prozorro tender id
- `status`
- `cpv_code`
- `procuring_entity_name`
- `expected_amount`
- `created_at`
- `date_modified`
- `raw_json jsonb`

### `tender_contracts`

Окрема таблиця контрактів, бо в одного тендера може бути декілька контрактів.

### `tender_suppliers`

Окрема таблиця постачальників/переможців, бо awards і suppliers — масиви.

## Індекси

```sql
CREATE INDEX ix_tenders_cpv_status_created ON tenders (cpv_code, status, created_at);
CREATE INDEX ix_tenders_procuring_entity ON tenders (procuring_entity_name);
CREATE INDEX ix_contracts_tender_id ON tender_contracts (tender_id);
CREATE INDEX ix_suppliers_name ON tender_suppliers (supplier_name);
CREATE INDEX ix_tenders_raw_json_gin ON tenders USING GIN (raw_json jsonb_path_ops);
```

## Аналітика

### Загальна економія

```sql
SELECT COALESCE(SUM(expected_amount - contracts_amount), 0)
FROM tender_contract_totals;
```

### Top-5 закупівельників

```sql
SELECT procuring_entity_name, SUM(contracts_amount) AS amount
FROM tender_contract_totals
GROUP BY procuring_entity_name
ORDER BY amount DESC
LIMIT 5;
```

### Top-5 постачальників

```sql
SELECT supplier_name, SUM(amount) AS amount
FROM tender_suppliers
GROUP BY supplier_name
ORDER BY amount DESC
LIMIT 5;
```

## High-load підхід

- `HttpClientFactory` + resilience handler.
- Паралельне завантаження деталей тендерів через bounded channel.
- `MaxParallelDetailsRequests` обмежує concurrency, щоб не DDOS-ити API.
- Upsert по `tenders.id`, щоб імпорт був ідемпотентним.
- JSONB зберігається як raw copy для audit/debug.
- Нормалізовані таблиці для контрактів і постачальників — для швидких SQL aggregation.

## Що можна покращити

- Зберігати `next_page.offset` для incremental synchronization.
- Перейти на Quartz.NET або Hangfire для запланованих імпортів.
- Винести імпорт у окремий Worker Service.
- Bulk insert через PostgreSQL COPY.
- Materialized Views для важких агрегатів.
- OpenTelemetry + Prometheus + Grafana.
- Integration tests через Testcontainers.
- Retry policy та dead-letter queue для помилкових імпортів.

## Результат

<img width="1918" height="971" alt="image" src="https://github.com/user-attachments/assets/7fce6318-1a9f-4407-a396-12e8f89adb8c" />
