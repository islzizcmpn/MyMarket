using FluentValidation;

namespace PcMarket.Api.Endpoints;

/// <summary>Endpoint filter that runs the FluentValidation validator for a request body argument of
/// type <typeparamref name="T"/>, returning a 400 validation problem on failure.</summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is not null)
        {
            var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                return Results.ValidationProblem(result.ToDictionary());
            }
        }

        return await next(context);
    }
}

public static class ValidationFilterExtensions
{
    /// <summary>Registers request-body validation for <typeparamref name="T"/> on this endpoint.</summary>
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidationFilter<T>>();
}
