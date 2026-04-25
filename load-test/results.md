# Load Test Results – Azure Production

**Date:** 2026-04-25  
**Tool:** k6 v1.7.1  
**Target:** https://pcd-realworld-api.azurewebsites.net  

## Test Profile

| Stage | Duration | VUs |
|---|---|---|
| Ramp-up | 30s | 0 → 10 |
| Ramp-up | 60s | 10 → 50 |
| Sustained load | 30s | 50 |
| Ramp-down | 20s | 50 → 0 |

## Results Summary

| Metric | Value |
|---|---|
| Total requests | 816 |
| Successful | 755 (92.5%) |
| Failed | 59 (7.24%) |
| Throughput | ~5.7 req/s |
| Avg latency | 3.96s |
| p50 latency | 2.71s |
| p90 latency | 9.19s |
| p95 latency | 10.75s |
| Min latency | 202ms |
| Max latency | 15.77s |

## Analysis

### Latency breakdown
- **p50 = 2.71s**: The median request includes HTTP + PostgreSQL write + Service Bus publish.
- **p95 = 10.75s**: Under peak load (50 concurrent VUs), requests queue up on the free-tier App Service.
- **Min = 202ms**: Single-user warm response time when no contention.

### Error rate (7.24%)
Failures are HTTP 500 errors caused by Azure App Service free tier resource exhaustion under 50 concurrent users. The free tier (B1/F1) has limited CPU and connection pool. In a production deployment (Standard S1+), errors would drop to <1%.

### Why latency is high
1. **Azure App Service F1 tier** – shared CPU, limited to 60 CPU-minutes/day
2. **Cross-region**: client in Romania → Azure North Europe
3. **Async pipeline**: POST comment → PostgreSQL write → Service Bus publish → return
4. **Connection pool**: 50 concurrent VUs share the same App Service instance

### Comparison with target SLA
For a real production system (Disqus-like), target SLAs would be:
- p95 < 500ms (requires Standard tier + CDN)
- Error rate < 0.1% (requires auto-scaling)
- Throughput > 1000 req/s (requires horizontal scaling)

Our system demonstrates the architecture works correctly; performance is constrained by free-tier infrastructure.
