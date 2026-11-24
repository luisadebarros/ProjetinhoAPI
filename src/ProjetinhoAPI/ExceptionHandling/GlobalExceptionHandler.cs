using Microsoft.AspNetCore.Diagnostics;

namespace ProjetinhoAPI.ExceptionHandling;

/// <summary>
/// Captura qualquer exceção que não foi tratada no código: registra no log de erros
/// e devolve um 500 padronizado (ProblemDetails) sem expor detalhes internos ao cliente.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Erro não tratado em {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Ocorreu um erro inesperado.",
                Detail = "Tente novamente mais tarde. Se o problema continuar, informe o traceId ao suporte.",
            },
        });
    }
}
