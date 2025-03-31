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

// Configure RabbitMQ
builder.Services.AddSingleton<IConnectionFactory>(sp =>
    new ConnectionFactory
    {
        HostName = builder.Configuration["RabbitMQ:HostName"] ?? "localhost",
        UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
        Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
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

// Configure DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

// Add Background Service
builder.Services.AddHostedService<BackgroundSearchService>();

var app = builder.Build();

// Habilita Swagger globalmente
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");

    // Opcional: configuración para entornos seguros
    c.OAuthClientId("swagger-client-id");
    c.OAuthAppName("Swagger Test");
    c.OAuthUseBasicAuthenticationWithAccessCodeGrant();
});

// Habilita CORS antes de autenticación/authorization
app.UseCors("AllowAll");

// Desactiva HTTPS redirection, ya que solo estamos usando HTTP
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
        // Espera a que la aplicación inicie
        Task.Delay(2000).Wait();

        // Abre el navegador en la URL de Swagger
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
