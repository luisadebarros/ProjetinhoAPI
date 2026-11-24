using Serilog;
using Serilog.Events;

namespace ProjetinhoAPI.Logging;

/// <summary>
/// Configuração dos logs da aplicação (Serilog).
/// Console: mostra tudo. Arquivos: um por nível, em logs/info, logs/warning e logs/error,
/// com um arquivo novo por dia e os últimos 30 dias guardados.
/// Os níveis mínimos (o que é registrado) ficam na seção "Serilog" do appsettings.
/// </summary>
public static class LoggingExtensions
{
    private const string AppName = "ProjetinhoAPI";
    private const string LogsFolder = "logs";
    private const int RetainedDays = 30;

    private const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    private const string FileTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    // No arquivo de erros também vai o traceId, o mesmo que a API devolve no corpo da resposta 500.
    private const string ErrorFileTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj} (traceId: {TraceId}){NewLine}{Exception}";

    /// <summary>
    /// Logger provisório, só para o console, usado enquanto a aplicação sobe.
    /// Depois ele é substituído pela configuração completa de <see cref="AddAppLogging"/>.
    /// </summary>
    public static Serilog.ILogger CreateBootstrapLogger() =>
        new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.WithProperty("SourceContext", AppName)
            .WriteTo.Console(outputTemplate: ConsoleTemplate)
            .CreateBootstrapLogger();

    /// <summary>Troca o logging padrão do .NET pelo Serilog, com os logs separados por nível.</summary>
    public static WebApplicationBuilder AddAppLogging(this WebApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, config) => config
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("SourceContext", AppName) // origem padrão para os logs feitos direto pelo Log estático
            .WriteTo.Console(outputTemplate: ConsoleTemplate)
            .WriteToLevelFile("info", FileTemplate, e => e.Level == LogEventLevel.Information)
            .WriteToLevelFile("warning", FileTemplate, e => e.Level == LogEventLevel.Warning)
            .WriteToLevelFile("error", ErrorFileTemplate, e => e.Level >= LogEventLevel.Error));

        return builder;
    }

    /// <summary>
    /// Registra uma linha por requisição HTTP, classificada pelo status da resposta.
    /// Deve ser o primeiro middleware, para enxergar o status final de todas as requisições.
    /// </summary>
    public static WebApplication UseAppRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0} ms (usuário: {UserName}, IP: {ClientIp})";
            options.GetLevel = GetRequestLogLevel;
            options.EnrichDiagnosticContext = (diagnostic, http) =>
            {
                diagnostic.Set("UserName", http.User.Identity?.Name ?? "anônimo");
                diagnostic.Set("ClientIp", http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido");
            };
        });

        return app;
    }

    // Erro no servidor (5xx ou exceção) = Error | erro do cliente (4xx) = Warning | sucesso = Information.
    // Swagger e OpenAPI ficam em Debug para não encher o log de info a cada página carregada.
    private static LogEventLevel GetRequestLogLevel(HttpContext http, double elapsedMs, Exception? exception)
    {
        var status = http.Response.StatusCode;

        if (exception is not null || status >= StatusCodes.Status500InternalServerError)
            return LogEventLevel.Error;

        if (status >= StatusCodes.Status400BadRequest)
            return LogEventLevel.Warning;

        if (http.Request.Path.StartsWithSegments("/swagger") || http.Request.Path.StartsWithSegments("/openapi"))
            return LogEventLevel.Debug;

        return LogEventLevel.Information;
    }

    // Cria um "sub-logger" que só deixa passar os eventos do nível indicado e grava num arquivo próprio.
    private static LoggerConfiguration WriteToLevelFile(
        this LoggerConfiguration config, string level, string template, Func<LogEvent, bool> belongsToLevel) =>
        config.WriteTo.Logger(sub => sub
            .Filter.ByIncludingOnly(belongsToLevel)
            .WriteTo.File(
                path: Path.Combine(LogsFolder, level, $"{level}-.log"),
                outputTemplate: template,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedDays));
}
