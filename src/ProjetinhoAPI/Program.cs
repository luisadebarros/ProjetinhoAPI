using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProjetinhoAPI.Configuration;
using ProjetinhoAPI.ExceptionHandling;
using ProjetinhoAPI.Logging;
using ProjetinhoAPI.OpenApi;
using ProjetinhoAPI.Repositories;
using ProjetinhoAPI.Repositories.Interfaces;
using ProjetinhoAPI.Security;
using ProjetinhoAPI.Services;
using ProjetinhoAPI.Services.Interfaces;
using Serilog;

// Logger provisório (só console) para registrar o que acontece antes da configuração completa carregar.
Log.Logger = LoggingExtensions.CreateBootstrapLogger();

try
{
    Log.Information("Iniciando a ProjetinhoAPI...");

    var builder = WebApplication.CreateBuilder(args);

    // ---------- Logs (Serilog): console + um arquivo por nível em logs/ ----------
    builder.AddAppLogging();

    // ---------- Autenticação JWT Bearer ----------
    builder.Services
        .AddOptions<JwtOptions>()
        .BindConfiguration(JwtOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart(); // a API nem sobe se faltar a chave ou ela for curta demais

    var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

    builder.Services.AddScoped<JwtBearerLoggingEvents>();
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false; // mantém os nomes curtos das claims (sub, unique_name, role)
            options.EventsType = typeof(JwtBearerLoggingEvents); // registra no log os tokens recusados
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = AuthClaims.Username,
                RoleClaimType = AuthClaims.Role,
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<ITokenService, TokenService>();
    builder.Services.AddSingleton<IUserRepository, UserRepository>();

    // ---------- MVC / OpenAPI ----------
    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>(); // exceções não tratadas vão para o log de erros
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

    var app = builder.Build();

    // Usuário de teste: criado somente em Development, com credenciais vindas dos user-secrets.
    if (app.Environment.IsDevelopment())
    {
        var seed = app.Configuration.GetSection("SeedUser");
        var username = seed["Username"];
        var password = seed["Password"];
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            app.Services.GetRequiredService<IUserRepository>().Add(username, password, "Admin");
        }
        else
        {
            app.Logger.LogWarning("SeedUser não configurado nos user-secrets: nenhum usuário de teste foi criado.");
        }
    }

    // Configure the HTTP request pipeline.
    app.UseAppRequestLogging(); // primeiro de tudo: uma linha de log por requisição
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi().AllowAnonymous();

        // Swagger UI em /swagger, lendo o documento gerado pelo .NET em /openapi/v1.json
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "ProjetinhoAPI v1");
            options.EnablePersistAuthorization(); // mantém o token ao recarregar a página
        });
    }

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Qualquer erro que derrube a aplicação (ex.: Jwt:Key faltando) vai para o log de erros como Fatal.
    Log.Fatal(ex, "A ProjetinhoAPI parou por causa de um erro inesperado.");
    Environment.ExitCode = 1; // avisa quem iniciou o processo (terminal, Docker...) que a API caiu
}
finally
{
    Log.CloseAndFlush();
}
