using FluentValidation;
using MediatR;
using SharedKernel.Common.Models;
using System.Text.Json;

namespace SharedKernel.Common.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : class
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (!_validators.Any())
                return await next();

            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count == 0)
                return await next();

            var error = ToError(failures);
            var responseType = typeof(TResponse);

            if (responseType == typeof(Result))
                return (TResponse)(object)Result.Failure(error);

            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var failureMethod = typeof(Result)
                                    .GetMethods()
                                    .Single(m => m.Name == nameof(Result.Failure) && m.IsGenericMethodDefinition)
                                    .MakeGenericMethod(responseType.GetGenericArguments()[0]);

                return (TResponse)failureMethod.Invoke(null, [error])!;
            }

            throw new ValidationException(failures);
        }

        /// <summary>Field names are camel-cased to match the JSON the client actually sent.</summary>
        private static FieldValidationError ToError(IEnumerable<FluentValidation.Results.ValidationFailure> failures) =>
            new(failures
                .GroupBy(f => ToJsonPath(f.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray()));

        private static string ToJsonPath(string propertyName) =>
            string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }
}