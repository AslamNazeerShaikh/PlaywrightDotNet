using JobScrape.Application.Abstractions;

namespace JobScrape.Application.Features.Quotas
{
    /// <summary>
    /// Lists usage quota definitions for the admin surface.
    /// </summary>
    public sealed class ListUsageQuotasHandler
    {
        private readonly IUsageQuotaRepository _quotas;

        /// <summary>
        /// Initializes a new instance of the <see cref="ListUsageQuotasHandler"/> class.
        /// </summary>
        /// <param name="quotas">The quota definition port. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="quotas"/> is null.</exception>
        public ListUsageQuotasHandler(IUsageQuotaRepository quotas)
        {
            ArgumentNullException.ThrowIfNull(quotas);
            _quotas = quotas;
        }

        /// <summary>
        /// Lists every stored definition.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>All definitions, in scope order.</returns>
        public async Task<QuotaListResponse> HandleAsync(CancellationToken cancellationToken)
        {
            var quotas = await _quotas.ListAsync(cancellationToken);
            return new QuotaListResponse(
                quotas
                    .Select(quota => new QuotaResponse(
                        quota.Scope.ToString(),
                        quota.Limit,
                        quota.Window
                    ))
                    .ToList()
            );
        }
    }
}
