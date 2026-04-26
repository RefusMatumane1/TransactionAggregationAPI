using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregation.Application.Queries.Customer.GetCustomer
{
    internal sealed class GetAllCustomersQueryHandler(IApplicationDbContext _context,
        ILogger<GetAllCustomersQueryHandler> logger)
        : IQueryHandler<GetAllCustomersQuery, PagedResult<CustomerDto>>
    {
        public async Task<Result<PagedResult<CustomerDto>>> Handle(
            GetAllCustomersQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Handling GetAllCustomersQuery: Page {Page}, PageSize {PageSize}, SearchTerm {SearchTerm}",
                    request.Page, request.PageSize, request.SearchTerm);

                var query = _context.Customers.AsQueryable();

                if (!string.IsNullOrWhiteSpace(request.SearchTerm))
                {
                    query = query.Where(c =>
                        c.Name.Contains(request.SearchTerm) ||
                        c.Email.Contains(request.SearchTerm));
                }

                var totalCount = await query.CountAsync(cancellationToken);

                var customers = await query
                    .Skip((request.Page - 1) * request.PageSize)
                    .Take(request.PageSize)
                    .ToListAsync(cancellationToken);

                var customerDtos = customers.Select(c => new CustomerDto(
                    c.Id.Value,
                    c.Email,
                    c.Name,
                    c.CreatedAt,
                    c.UpdatedAt));

                var result = new PagedResult<CustomerDto>(
                    customerDtos,
                    totalCount,
                    request.Page,
                    request.PageSize);

                logger.LogInformation("Successfully retrieved {Count} customers for Page {Page} with PageSize {PageSize}",
                    customerDtos.Count(), request.Page, request.PageSize);

                return Result<PagedResult<CustomerDto>>.Success(result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while handling GetAllCustomersQuery: Page {Page}, PageSize {PageSize}, SearchTerm {SearchTerm}",
                    request.Page, request.PageSize, request.SearchTerm);
                return Result.Failure<PagedResult<CustomerDto>>(Error.Failure("500","An error occurred while retrieving customers."));
            }
        }
    }
}
