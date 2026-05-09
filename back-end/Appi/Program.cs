using System.Threading.Channels;
using Database.Chroma;
using Database.Mongo;
using Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Repositorys.Implementations;
using Repositorys.Interfaces;
using Services.Implementations;
using Services.Interfaces;
using WorkerService.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS for Frontend integration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// 2. Configure Databases
// PostgreSQL (User authentication)
var postgresConn = builder.Configuration.GetConnectionString("DefaultConnection")
                   ?? "Host=localhost;Database=mimic_ai_db;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(postgresConn));

// MongoDB (Chat History)
var mongoConn = builder.Configuration.GetConnectionString("MongoConnection")
                ?? "mongodb://localhost:27017";
var mongoDbName = builder.Configuration["MongoDatabaseName"] ?? "mimic_ai_chat_db";
builder.Services.AddSingleton(new MongoDbContext(mongoConn, mongoDbName));

// ChromaDB (Vector database)
var chromaUrl = builder.Configuration["ChromaDB:BaseUrl"] ?? "http://localhost:8000";
builder.Services.AddSingleton(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("ChromaClient");
    return new ChromaDbContext(httpClient, chromaUrl);
});

// Register standard HttpClient
builder.Services.AddHttpClient();

// 3. Register Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IChatHistoryRepository, ChatHistoryRepository>();
builder.Services.AddScoped<IVectorRepository, VectorRepository>();

// 4. Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IExternalLlmService, ExternalLlmService>();
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddScoped<IFinetuningService, FinetuningService>();
builder.Services.AddScoped<IOrchestrationService, OrchestrationService>();
builder.Services.AddScoped<IIntegrationService, IntegrationService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<ILocalLlmService, LocalLlmService>();

// 5. Register Ingestion Event Channel (Thread-safe background queue)
var queueChannel = Channel.CreateUnbounded<IngestionTask>(new UnboundedChannelOptions
{
    SingleReader = true,
    SingleWriter = false
});
builder.Services.AddSingleton(queueChannel);

// 6. Register Background Worker (Galileu AI Agent in-process hosting option)
builder.Services.AddHostedService<GalileuWorker>();

// 7. Add Controllers from the Controllers project
builder.Services.AddControllers()
    .AddApplicationPart(typeof(Controllers.AuthController).Assembly);

var app = builder.Build();

// Enable CORS
app.UseCors();

// Configure the HTTP request pipeline.

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Ensure PostgreSQL Database is created and migrations applied on startup
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}
catch (Exception ex)
{
    Console.WriteLine($"[STARTUP ERROR] Falha ao criar/verificar o banco Postgres: {ex.Message}");
}

app.Run();