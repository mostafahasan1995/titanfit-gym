using Gym.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gym.Api;

/// <summary>
/// Turns business errors into RFC 7807 problem details with a stable <c>code</c>:
/// 400 rule broken / bad input, 403 not allowed, 404 not found, 409 conflict (duplicates, already done).
/// </summary>
internal sealed class GymExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is DbUpdateException update && DbErrors.IsUniqueViolation(update, out var constraint))
        {
            exception = DbErrors.ToConflict(constraint);
        }

        int status;
        string code;
        IReadOnlyDictionary<string, object?>? details = null;
        switch (exception)
        {
            case NotFoundException notFound:
                (status, code) = (StatusCodes.Status404NotFound, notFound.Code);
                break;
            case ConflictException conflict:
                (status, code, details) = (StatusCodes.Status409Conflict, conflict.Code, conflict.Details);
                break;
            case ForbiddenException forbidden:
                (status, code) = (StatusCodes.Status403Forbidden, forbidden.Code);
                break;
            case DomainException domain:
                (status, code, details) = (StatusCodes.Status400BadRequest, domain.Code, domain.Details);
                break;
            case BadHttpRequestException badRequest:
                (status, code) = (badRequest.StatusCode, "bad_request");
                break;
            default:
                return false;
        }

        var problem = new ProblemDetails { Status = status, Title = exception.Message };
        problem.Extensions["code"] = code;
        if (details is not null)
        {
            foreach (var (key, value) in details)
            {
                problem.Extensions[key] = value;
            }
        }

        httpContext.Response.StatusCode = status;
        var written = await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        }

        return true;
    }
}
