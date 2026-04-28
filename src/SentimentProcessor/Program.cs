using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Singleton data source — pre-warms connection pool on startup,
        // eliminates per-invocation SSL handshake cold start penalty
        var pgConnStr = Environment.GetEnvironmentVariable("PostgresConnection")!;
        services.AddSingleton(_ => NpgsqlDataSource.Create(pgConnStr));
    })
    .Build();

host.Run();
