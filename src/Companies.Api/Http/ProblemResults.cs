using System.Text.Json;
using Companies.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Companies.Api.Http;

/// <summary>
/// The one place where application errors become HTTP responses (RFC 7807 problem details).
/// </summary>
public static class ProblemResults
{
    public const string ValidationTitle = "The request is invalid.";

    public static IActionResult ToProblem(this ControllerBase controller, Error error) => error.Kind switch
    {
        ErrorKind.Validation => ValidationProblem(controller, error),
        ErrorKind.NotFound => controller.Problem(
            statusCode: StatusCodes.Status404NotFound, title: "Company not found.", detail: error.Message),
        ErrorKind.Conflict => controller.Problem(
            statusCode: StatusCodes.Status409Conflict, title: "Company already exists.", detail: error.Message),
        _ => controller.Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    /// <summary>
    /// Builds the 400 response for failures caught by model binding and data annotations,
    /// in the same shape as failures reported by the application layer.
    /// </summary>
    public static IActionResult FromModelState(ActionContext context)
    {
        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(
            context.HttpContext, context.ModelState, StatusCodes.Status400BadRequest, ValidationTitle);

        // Model binding reports C# member names ("WebsiteUrl"); clients know the JSON ones ("websiteUrl").
        problem.Errors = problem.Errors.ToDictionary(
            entry => JsonNamingPolicy.CamelCase.ConvertName(entry.Key), entry => entry.Value);
        problem.Detail = string.Join(" ", problem.Errors.Values.SelectMany(messages => messages));

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    private static IActionResult ValidationProblem(ControllerBase controller, Error error)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in error.FieldErrors ?? new Dictionary<string, string[]>())
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(field, message);
            }
        }

        return controller.ValidationProblem(
            detail: error.Message,
            statusCode: StatusCodes.Status400BadRequest,
            title: ValidationTitle,
            modelStateDictionary: modelState);
    }
}
