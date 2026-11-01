# Performance

## Methodology

The risk engine is measured in-process by `Sentinel.PerformanceTests`. That test records P50 / P95 / P99 for 250 evaluations of the documented example transaction on the local development machine. It does not include Kafka, Redis or PostgreSQL.

End-to-end load is generated with:

```bash
dotnet run --project tools/Sentinel.TransactionGenerator -- --mode mixed --rate 10
dotnet run --project tools/Sentinel.TransactionGenerator -- --mode mixed --rate 50
dotnet run --project tools/Sentinel.TransactionGenerator -- --mode mixed --rate 100
```

Record observed ingestion HTTP accept rate and decision persist latency from logs / OpenTelemetry. Do not quote a throughput number that was not measured on the machine under test.

## Local in-process result

Recorded when tests last ran on the development workstation. Replace this line after `dotnet test tests/Sentinel.PerformanceTests`:

- Engine-only: `Sentinel.PerformanceTests` passed on this workstation with P99 under 250 ms for 250 evaluations of the documented example transaction.

Re-run on 1 November 2026 (Linux, .NET SDK 10): the test passed and printed `In-process engine P50=0ms P95=0ms P99=0ms`, meaning every evaluation finished in under a millisecond when rounded. End-to-end throughput is not measured yet.
