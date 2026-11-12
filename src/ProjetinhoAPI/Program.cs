using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProjetinhoAPI.Configuration;
using ProjetinhoAPI.OpenApi;
using ProjetinhoAPI.Repositories;
using ProjetinhoAPI.Repositories.Interfaces;
using ProjetinhoAPI.Security;
using ProjetinhoAPI.Services;
using ProjetinhoAPI.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ---------- Autenticação JWT Bearer ----------
builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart(); // a API nem sobe se faltar a chave ou ela for curta demais

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // mantém os nomes curtos das claims (sub, unique_name, role)
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
}

// Configure the HTTP request pipeline.
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
