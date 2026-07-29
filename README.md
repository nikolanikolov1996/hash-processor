# Hash Processor

This is a small .NET 10 application that generates SHA1 hashes, sends them through RabbitMQ and stores them in MariaDB.

The solution has two applications:

- `HashProcessor.Api` exposes the HTTP endpoints and publishes hashes.
- `HashProcessor.Worker` reads messages from RabbitMQ and saves them.

The remaining projects contain the shared contracts, database code, RabbitMQ setup and tests.

## How it works

Calling `POST /hashes` generates 40,000 random SHA1 hashes. They are published as persistent JSON messages and the API waits for RabbitMQ publisher confirmations before returning `202 Accepted`.

The worker has four consumers. Each consumer processes one message at a time and acknowledges it only after the database transaction succeeds.

For every new hash, the worker:

1. inserts it into `hashes`;
2. updates the count for that day in `hash_counts_by_date`;
3. commits both changes in the same transaction.

The SHA1 column is unique, so redelivering the same message does not create another row or increase the count twice.

`GET /hashes` reads the stored daily counts. It does not run `COUNT(*)` over the complete hashes table each time.

One detail worth mentioning: `202 Accepted` means RabbitMQ accepted all 40,000 messages. The worker may still be writing them to MariaDB, so the GET result can continue increasing for a while after the POST finishes.

## Running it locally

You need Visual Studio with .NET 10 support and Docker Desktop.

Open `hash-processor.slnx`, then start MariaDB and RabbitMQ from the Visual Studio terminal:

```powershell
docker compose up -d
docker compose ps
```

Wait until both containers are healthy.

The API and worker each have their own User Secrets file. Right-click each project, choose **Manage User Secrets**, and add:

```json
{
  "ConnectionStrings": {
    "MariaDb": "Server=127.0.0.1;Port=3307;Database=hash_processor;User ID=hash_processor;Password=hash_processor_db_dev",
    "RabbitMq": "amqp://hash_processor:hash_processor_mq_dev@127.0.0.1:5672/"
  }
}
```

After that, right-click the solution and choose **Configure Startup Projects**. Select **Multiple startup projects** and set both `HashProcessor.Api` and `HashProcessor.Worker` to **Start**.

Press `F5`. The API uses:

```text
https://localhost:7178
http://localhost:5221
```

The requests are already available in `HashProcessor.Api/HashProcessor.Api.http`.

## API

Generate and publish 40,000 hashes:

```http
POST https://localhost:7178/hashes
```

A successful request returns:

```text
202 Accepted
```

Only one generation request is allowed at a time. An overlapping request returns `429 Too Many Requests`.

Read the stored daily counts:

```http
GET https://localhost:7178/hashes
```

Example:

```json
{
  "hashes": [
    {
      "date": "2026-07-29",
      "count": 40000
    }
  ]
}
```

## RabbitMQ

The management page is available at:

```text
http://localhost:15672
```

Local login:

```text
Username: hash_processor
Password: hash_processor_mq_dev
```

The main queue is `hashes`.

Invalid messages are sent directly to `hashes.failed`. Other failures are retried after 1, 2 and 4 seconds. If the third retry also fails, the message is moved to the failed queue instead of being retried forever.

## MariaDB

For DBeaver or another database client, use:

```text
Host: 127.0.0.1
Port: 3307
Database: hash_processor
Username: hash_processor
Password: hash_processor_db_dev
```

The schema is created from the SQL files in `HashProcessor.Database/Scripts` when Docker creates the MariaDB volume for the first time.

Some useful queries:

```sql
SELECT COUNT(*) FROM hashes;

SELECT *
FROM hash_counts_by_date
ORDER BY `date`;
```

To stop the containers without deleting the data:

```powershell
docker compose down
```

To delete the local database, RabbitMQ data and start again from empty volumes:

```powershell
docker compose down --volumes --remove-orphans
docker compose up -d
```

The first command permanently deletes the local Docker data.

## Tests

The test project contains unit tests and integration tests. The integration tests start temporary MariaDB and RabbitMQ containers, so Docker Desktop must be running.

Run them from Test Explorer or from the terminal:

```powershell
dotnet test HashProcessor.Tests\HashProcessor.Tests.csproj --configuration Release
```

There are currently 26 tests covering hash generation, validation, duplicate messages, database transactions, daily counts, RabbitMQ publishing, retries and dead-letter handling.

## CI

`.github/workflows/ci.yml` builds the solution, runs the tests and checks NuGet packages for known vulnerabilities on every pull request and every push to `main`.

