using System.Text;
using System.Diagnostics; // Para lanzar el navegador
using RabbitMQ.Client;
using Microsoft.EntityFrameworkCore;
using ServicesLayer;
using DataLayer.Models;
using SearchDaemon.RabbitMQ;
using SearchDaemon.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Agrega el generador de Swagger

// Configurar la cadena de conexión según el entorno
if (builder.Environment.IsDevelopment())
{
    builder.Configuration["ConnectionStrings:PostgresConnection"] = "Host=localhost;Port=5432;Username=postgres;Password=REEMPLAZAR;Database=grup";
}
else
{
    builder.Configuration["ConnectionStrings:PostgresConnection"] = "Host=postgres-svc;Port=5432;Username=admin;Password=REEMPLAZAR;Database=atrapo";
}

// Configure PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

// Configure RabbitMQ
builder.Services.AddSingleton<IConnectionFactory>(sp =>
{
    var config = builder.Configuration;
    var isDevelopment = builder.Environment.IsDevelopment();
    return new ConnectionFactory
    {
        HostName = isDevelopment ? "localhost" : config["RABBITMQ_HOST"] ?? "rabbitmq-svc",
        Port = int.Parse(config["RABBITMQ_PORT"] ?? "5672"),
        UserName = config["RABBITMQ_USER"] ?? "admin",
        Password = config["RABBITMQ_PASSWORD"] ?? "REEMPLAZAR"
    };
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Register Services
builder.Services.AddScoped<IRabbitMQService, RabbitMQService>();
builder.Services.AddScoped<IWebMixerService, WebMixerService>();
builder.Services.AddHttpClient();

// Add Background Service
builder.Services.AddHostedService<BackgroundSearchService>();

var app = builder.Build();

// Habilita Swagger globalmente
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
    c.OAuthClientId("swagger-client-id");
    c.OAuthAppName("Swagger Test");
    c.OAuthUseBasicAuthenticationWithAccessCodeGrant();
});

// Habilita CORS antes de autenticación/authorization
app.UseCors("AllowAll");

// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Configure Kestrel para escuchar en todas las interfaces
var url = "http://localhost:7128";
app.Urls.Add(url); // Configura la URL principal

// Lanza el navegador automáticamente
Task.Run(() =>
{
    try
    {
        Task.Delay(2000).Wait();
        var psi = new ProcessStartInfo
        {
            FileName = url + "/swagger",
            UseShellExecute = true
        };
        Process.Start(psi);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"No se pudo abrir el navegador: {ex.Message}");
    }
});

app.Run();