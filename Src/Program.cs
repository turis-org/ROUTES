using Database;
using Npgsql;

namespace Src;

public class Program
{
    public static void Main(string [] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddEnvironmentVariables();

        // Получение строки подключения
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = builder.Configuration["DB_HOST"] ?? "localhost",
            Port = int.Parse(builder.Configuration["PG_PORT"] ?? "5432"),
            Database = builder.Configuration["PG_DATABASE"],
            Username = builder.Configuration["PG_USER"],
            Password = builder.Configuration["PG_PASSWORD"],
            Pooling = bool.Parse(builder.Configuration["DB_POOLING"] ?? "true"),
            CommandTimeout = int.Parse(builder.Configuration["DB_TIMEOUT"] ?? "30")
        }.ToString();

        // Регистрация сервиса
        builder.Services.AddScoped<IRoutingService>(_ => new RoutingService(connectionString));

        ConfigureServices(builder.Services);

        var app = builder.Build();

        ConfigureMiddleware(app);

        app.Run("http://0.0.0.0:8080");
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.MapControllers();
    }
}