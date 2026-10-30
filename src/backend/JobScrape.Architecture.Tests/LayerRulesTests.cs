using NetArchTest.Rules;
using Xunit;

namespace JobScrape.Architecture.Tests
{
    public sealed class LayerRulesTests
    {
        [Fact]
        public void DomainDoesNotDependOnOuterLayers()
        {
            var domain = Types.InAssembly(typeof(JobScrape.Domain.DomainException).Assembly);

            var application = domain.ShouldNot().HaveDependencyOn("JobScrape.Application").GetResult();
            var infrastructure = domain.ShouldNot().HaveDependencyOn("JobScrape.Infrastructure").GetResult();
            var api = domain.ShouldNot().HaveDependencyOn("JobScrape.Api").GetResult();

            Assert.True(application.IsSuccessful, Describe(application));
            Assert.True(infrastructure.IsSuccessful, Describe(infrastructure));
            Assert.True(api.IsSuccessful, Describe(api));
        }

        [Fact]
        public void ApplicationDoesNotDependOnInfrastructureOrApi()
        {
            var application = Types.InAssembly(typeof(JobScrape.Application.Results.Result).Assembly);

            var infrastructure = application.ShouldNot().HaveDependencyOn("JobScrape.Infrastructure").GetResult();
            var api = application.ShouldNot().HaveDependencyOn("JobScrape.Api").GetResult();

            Assert.True(infrastructure.IsSuccessful, Describe(infrastructure));
            Assert.True(api.IsSuccessful, Describe(api));
        }

        private static string Describe(NetArchTest.Rules.TestResult result)
        {
            return string.Join(Environment.NewLine, result.FailingTypes ?? Enumerable.Empty<Type>());
        }
    }
}
