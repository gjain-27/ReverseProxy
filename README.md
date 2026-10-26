# Reverse Proxy & Load Balancer

A high-performance HTTP reverse proxy and load balancer written from scratch in C# on raw TCP sockets. It terminates TLS, balances traffic across backend servers, rate limits clients and caches responses, and was deployed and benchmarked on Microsoft Azure.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Git
- Visual Studio 2022+ (optional)

### Clone

```bash
git clone https://github.com/gjain-27/ReverseProxy.git
cd ReverseProxy
```

### Configure

Copy the example settings file and edit it if needed:

```bash
cp .env.example .env
```

| Setting | Default | Description |
|---|---|---|
| `PROXY_BACKENDS` | `localhost:5001,localhost:5002,localhost:5003` | Comma-separated `host:port` list of backend servers |
| `PROXY_BALANCER` | `least-connections` | `least-connections` or `round-robin` |
| `PROXY_HTTP_PORT` | `8080` | HTTP listen port |
| `PROXY_HTTPS_PORT` | `8443` | HTTPS listen port |
| `PROXY_CERT_PATH` | | Path to a `.pfx` certificate (HTTPS is disabled without one) |
| `PROXY_CERT_PASSWORD` | | Certificate password |
| `PROXY_RATE_LIMIT_CAPACITY` | `10` | Token bucket size per client IP |
| `PROXY_RATE_LIMIT_PER_SECOND` | `5` | Token refill rate per client IP |
| `PROXY_CACHE_MB` | `10` | Response cache size in MB |
| `PROXY_LOG_REQUESTS` | `true` | Log every request to the console |

### HTTPS (optional)

Generate a development certificate, using the same password as `PROXY_CERT_PASSWORD`:

```bash
dotnet dev-certs https -ep proxy-cert.pfx -p change-me
```

### Build

```bash
dotnet build -c Release
```

### Run

Start the test backends (ports 5001, 5002 and 5003):

```powershell
.\start-backends.ps1 -Configuration Release
```

Start the proxy:

```bash
dotnet run -c Release --project Proxy
```

Send requests through it:

```bash
curl http://localhost:8080/
curl -k https://localhost:8443/
curl -i http://localhost:8080/time
```

### Tests

```bash
dotnet test
```

## Features

- **Asynchronous TCP proxy:** handles thousands of concurrent connections with `async`/`await`, with HTTP keep-alive on both the client and backend side.
- **Hardened HTTP/1.1 parser:** strict parsing that rejects request smuggling attempts (conflicting `Content-Length`, `Content-Length` with `Transfer-Encoding`, bare CR/LF, malformed headers).
- **Streaming bodies:** supports `Content-Length`, chunked transfer encoding (with extensions and trailers) and read-until-close responses without buffering whole bodies in memory.
- **Response ordering:** pipelined requests on one connection are answered in order using a per-connection channel.
- **TLS termination:** HTTPS on a separate port using `SslStream`, with a handshake timeout.
- **Load balancing:** round-robin and least-connections strategies behind a common interface.
- **Health checks:** active checks probe every backend every 5 seconds, and passive checks mark a backend unhealthy as soon as a connection to it fails.
- **Rate limiting:** per-client token bucket with lazy refill, returning `429 Too Many Requests` when exceeded, with idle buckets cleaned up automatically.
- **LRU response cache:** thread-safe, size-limited by bytes, with TTL expiry from `Cache-Control: max-age`. Only safe responses are cached (`GET`, `200`, no `private`/`no-store`/`no-cache`, no `Set-Cookie` or `Vary`). Responses include `X-Cache: HIT/MISS` and `Age` headers.
- **Forwarded headers:** adds `X-Forwarded-For` and `X-Forwarded-Proto`, ignoring values spoofed by the client.
- **Unit tests:** xUnit tests for the parser, reader, response forwarding, load balancers, health checks, rate limiter and cache.

## Deployment

Deployed on **Microsoft Azure** across 3 Ubuntu 24.04 VMs in a private virtual network:

| VM | Size | Role |
|---|---|---|
| `rp-proxy` | Standard_D2s_v4 | Reverse proxy (only VM with a public IP) |
| `rp-loadgen` | Standard_D2s_v4 | Load generator (bombardier) |
| `rp-backend` | Standard_D2ds_v4 | 2 ASP.NET Core (Kestrel) backend services |

## Benchmarks

Measured on Azure with [bombardier](https://github.com/codesenberg/bombardier) (100 connections, 30 seconds):

| Test | Requests/sec | p50 latency | p99 latency |
|---|---|---|---|
| Backend direct (no proxy) | 71,775 | 1.24 ms | 3.84 ms |
| Through proxy (cache miss) | 28,371 | 3.16 ms | 8.14 ms |
| Through proxy (cache hit) | 59,404 | 1.62 ms | 3.25 ms |

Over 2.6 million requests were served through the proxy with zero errors.

## Planned Features

- HTTP/2 support
- Connection pooling to backends
- Weighted load balancing
- Metrics endpoint
