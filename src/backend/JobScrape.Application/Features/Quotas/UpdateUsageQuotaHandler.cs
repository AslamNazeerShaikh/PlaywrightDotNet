using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Access;

namespace JobScrape.Application.Features.Quotas
{
    /// <summary>
    /// Changes one scope's limit and window. Updating a scope with no
    /// stored definition creates it, so the admin surface can repair a
    /// missing row without a migration.
    /// </summary>
    public sealed class UpdateUsageQuotaHandler
    {
        private readonly IUsageQuotaRepository _quotas;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateUsageQuotaHandler"/> class.
        /// </summary>
        /// <param name="quotas">The quota definition port. Must not be null.</param>
        /// <param name="unitOfWork">Commits the change. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public UpdateUsageQuotaHandler(IUsageQuotaRepository quotas, IUnitOfWork unitOfWork)
        {
            ArgumentNullException.ThrowIfNull(quotas);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            _quotas = quotas;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        /// Updates the scope's definition.
        /// </summary>
        /// <param name="scope">The raw scope name, e.g. <c>Api</c>.</param>
        /// <param name="limit">Requests one user may make per window.</param>
        /// <param name="window">The fixed window.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the updated definition; <c>UNKNOWN_QUOTA_SCOPE</c>
        /// for an unknown scope, or <c>VALIDATION_FAILED</c> for an
        /// out-of-range limit or window.
        /// </returns>
        public async Task<Result<QuotaResponse>> HandleAsync(
            string? scope,
            int limit,
            TimeSpan window,
            CancellationToken cancellationToken
        )
        {
            if (
                !Enum.TryParse<QuotaScope>(scope, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed)
            )
            {
                return Result.Fail<QuotaResponse>(Errors.UnknownQuotaScope(scope));
            }

            var quota = await _quotas.GetAsync(parsed, cancellationToken);
            if (quota is null)
            {
                UsageQuota created;
                try
                {
                    created = UsageQuota.Create(parsed, limit, window);
                }
                catch (DomainException ex)
                {
                    return Result.Fail<QuotaResponse>(
                        Errors.ValidationFailed(
                            ex.Message,
                            new Dictionary<string, string[]> { ["limit"] = [ex.Message] }
                        )
                    );
                }

                await _quotas.AddAsync(created, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Ok(
                    new QuotaResponse(parsed.ToString(), created.Limit, created.Window)
                );
            }

            try
            {
                quota.SetLimit(limit);
                quota.SetWindow(window);
            }
            catch (DomainException ex)
            {
                return Result.Fail<QuotaResponse>(
                    Errors.ValidationFailed(
                        ex.Message,
                        new Dictionary<string, string[]> { ["limit"] = [ex.Message] }
                    )
                );
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok(new QuotaResponse(parsed.ToString(), quota.Limit, quota.Window));
        }
    }
}
