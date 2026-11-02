using JobScrape.Domain.Abstractions;
using Xunit;

namespace JobScrape.Domain.Tests.Abstractions
{
    public sealed class AggregateRootTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

        [Fact]
        public void BuffersEventsInOrderUntilCleared()
        {
            var root = new TestRoot();

            root.Emit(new TestEvent(Now));
            root.Emit(new TestEvent(Now.AddSeconds(1)));

            Assert.Equal([new TestEvent(Now), new TestEvent(Now.AddSeconds(1))], root.DomainEvents);

            root.ClearDomainEvents();

            Assert.Empty(root.DomainEvents);
        }

        [Fact]
        public void RaiseRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new TestRoot().Emit(null!));
        }

        [Fact]
        public void ExposedEventsCannotBeMutatedByCallers()
        {
            var root = new TestRoot();
            root.Emit(new TestEvent(Now));

            var events = Assert.IsAssignableFrom<IList<DomainEvent>>(root.DomainEvents);

            Assert.True(events.IsReadOnly);
        }

        private sealed record TestEvent(DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

        private sealed class TestRoot : AggregateRoot
        {
            public void Emit(DomainEvent domainEvent)
            {
                Raise(domainEvent);
            }
        }
    }
}
