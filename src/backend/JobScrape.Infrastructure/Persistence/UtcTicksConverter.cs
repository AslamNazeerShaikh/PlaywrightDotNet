using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// Stores <see cref="DateTimeOffset"/> as UTC ticks. SQLite has no
    /// native timestamp type and EF Core cannot compare or order the default
    /// text form in SQL, which date specifications need. Values come back in
    /// UTC; the instant is preserved exactly.
    /// </summary>
    public sealed class UtcTicksConverter : ValueConverter<DateTimeOffset, long>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UtcTicksConverter"/> class.
        /// </summary>
        public UtcTicksConverter()
            : base(value => value.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero)) { }
    }
}
