using JobScrape.Application.Abstractions;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="IJobPostingRepository"/>. Lookups check postings
    /// staged in this unit of work before querying, as the port requires.
    /// </summary>
    internal sealed class JobPostingRepository(JobScrapeDbContext db) : IJobPostingRepository
    {
        public async Task<JobPosting?> FindAsync(
            SourceKey source,
            ExternalJobId externalJobId,
            CancellationToken cancellationToken
        )
        {
            return db.JobPostings.Local.FirstOrDefault(posting =>
                    posting.Source == source && posting.ExternalJobId == externalJobId
                )
                ?? await db.JobPostings.FirstOrDefaultAsync(
                    posting => posting.Source == source && posting.ExternalJobId == externalJobId,
                    cancellationToken
                );
        }

        public async Task AddAsync(JobPosting posting, CancellationToken cancellationToken)
        {
            await db.JobPostings.AddAsync(posting, cancellationToken);
        }
    }
}
