using FluentValidation;
using MediatR;
using SharedKernel.Common.Models;

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

            var responseType = typeof(TResponse);
            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var error = Error.Validation(string.Join("; ", failures.Select(f => f.ErrorMessage)));

                var failureMethod = typeof(Result)
                                    .GetMethods()
                                    .Single(m => m.Name == nameof(Result.Failure) && m.IsGenericMethodDefinition)
                                    .MakeGenericMethod(responseType.GetGenericArguments()[0]);

                return (TResponse)failureMethod.Invoke(null, [error])!;
            }

            throw new ValidationException(failures);
        }
    }
}
