using System.Threading.Channels;
using Database.Chroma;
using Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Repositorys.Implementations;
using Repositorys.Interfaces;
using Services.Implementations;
using Services.Interfaces;
using WorkerService.BackgroundServices;

var builder = Host.CreateApplicationBuilder(args);

// Configure Databases for Background Worker
// PostgreSQL
var postgresConn = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=mimic_ai_db;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(postgresConn));

// ChromaDB
var chromaUrl = builder.Configuration["ChromaDB:BaseUrl"] ?? "http://localhost:8000";
builder.Services.AddSingleton(sp => 
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ChromaClient");
    return new ChromaDbContext(httpClient, chromaUrl);
});

// Register standard HttpClient
builder.Services.AddHttpClient();

// Register repositories needed by the background AI Agent (Galileu)
builder.Services.AddScoped<IVectorRepository, VectorRepository>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();

// Register Singleton Channels
var queueChannel = Channel.CreateUnbounded<IngestionTask>(new UnboundedChannelOptions
{
    SingleReader = true,
    SingleWriter = false
});
builder.Services.AddSingleton(queueChannel);

// Register Background Worker
builder.Services.AddHostedService<GalileuWorker>();

var host = builder.Build();
host.Run();