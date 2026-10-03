using Microsoft.AspNetCore.Http;
using SharedKernel.Common.Models;

namespace BuildingBlocks.Web
{
    public static class ResultExtensions
    {
        public static IResult Match(this Result result, Func<IResult> onSuccess) =>
            result.IsSuccess ? onSuccess() : CustomResults.Problem(result);

        public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
            result.IsSuccess ? onSuccess(result.Value) : CustomResults.Problem(result);

        public static IResult ToOk<T, TResponse>(this Result<T> result, Func<T, TResponse> toResponse) =>
            result.Match(value => Results.Ok(toResponse(value)));

        public static IResult ToNoContent(this Result result) =>
            result.Match(Results.NoContent);
    }
}