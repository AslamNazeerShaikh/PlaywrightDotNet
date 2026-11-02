namespace JobScrape.Domain.Abstractions
{
    /// <summary>
    /// Base of every aggregate root. Collects the domain events raised by
    /// state transitions until the application layer dispatches them after
    /// persistence and clears the buffer.
    /// </summary>
    public abstract class AggregateRoot
    {
        // Per-instance buffer; aggregates are not shared across threads, so
        // no synchronization is needed.
        private readonly List<DomainEvent> _domainEvents = [];

        /// <summary>
        /// Gets the events raised since the last <see cref="ClearDomainEvents"/>,
        /// in the order they were raised.
        /// </summary>
        public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        /// <summary>
        /// Clears the buffered events. Called by the application layer after
        /// it has dispatched them.
        /// </summary>
        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }

        /// <summary>
        /// Buffers an event raised by a state transition.
        /// </summary>
        /// <param name="domainEvent">The event. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="domainEvent"/> is null.</exception>
        protected void Raise(DomainEvent domainEvent)
        {
            ArgumentNullException.ThrowIfNull(domainEvent);
            _domainEvents.Add(domainEvent);
        }
    }
}
