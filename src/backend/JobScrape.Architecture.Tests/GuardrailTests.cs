using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace JobScrape.Architecture.Tests
{
    public sealed class GuardrailTests
    {
        private static readonly Assembly Domain = typeof(JobScrape.Domain.DomainException).Assembly;

        private static readonly Assembly Application =
            typeof(JobScrape.Application.Results.Result).Assembly;

        private static readonly Assembly[] Production =
        [
            Domain,
            Application,
            typeof(JobScrape.Infrastructure.SearchProfiles.InMemorySearchProfileStore).Assembly,
            typeof(JobScrape.Api.Program).Assembly,
        ];

        // docs/14-application-patterns.md §2.2: mediator/CQRS shapes are
        // banned even when hand-rolled under the same names.
        private static readonly string[] BannedTypeNames =
        [
            "IMediator",
            "ISender",
            "IPublisher",
            "IRequest",
            "IRequestHandler",
            "INotification",
            "INotificationHandler",
            "ICommand",
            "IQuery",
            "ICommandHandler",
            "IQueryHandler",
            "IPipelineBehavior",
        ];

        [Fact]
        public void DomainReferencesOnlyTheBaseClassLibrary()
        {
            var foreign = Domain
                .GetReferencedAssemblies()
                .Select(name => name.Name!)
                .Where(name => !name.StartsWith("System", StringComparison.Ordinal))
                .Where(name => name is not ("netstandard" or "mscorlib"))
                .ToArray();

            Assert.Empty(foreign);
        }

        [Theory]
        [InlineData("Microsoft.Playwright")]
        [InlineData("Microsoft.EntityFrameworkCore")]
        [InlineData("AngleSharp")]
        [InlineData("System.Net.Http")]
        [InlineData("Microsoft.Extensions")]
        public void DomainDoesNotUseInfrastructureLibraries(string dependency)
        {
            var result = Types
                .InAssembly(Domain)
                .ShouldNot()
                .HaveDependencyOn(dependency)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }

        [Theory]
        [InlineData("Microsoft.Playwright")]
        [InlineData("Microsoft.EntityFrameworkCore")]
        [InlineData("AngleSharp")]
        [InlineData("ClosedXML")]
        public void ApplicationDoesNotUseInfrastructureLibraries(string dependency)
        {
            var result = Types
                .InAssembly(Application)
                .ShouldNot()
                .HaveDependencyOn(dependency)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }

        [Fact]
        public void NoProductionAssemblyReferencesMediatR()
        {
            var offenders = Production
                .SelectMany(assembly => assembly.GetReferencedAssemblies())
                .Select(name => name.Name!)
                .Where(name => name.StartsWith("MediatR", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.Empty(offenders);
        }

        [Fact]
        public void NoProductionTypeIsMediatorShaped()
        {
            var offenders = Production
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => BannedTypeNames.Contains(StripArity(type.Name)))
                .Select(type => type.FullName)
                .ToArray();

            Assert.Empty(offenders);
        }

        [Fact]
        public void DomainHasNoStaticMutableState()
        {
            // Lambda and method-group caches live in compiler-generated
            // nested types (<>c, <>O); they are not authored state.
            var offenders = Domain
                .GetTypes()
                .Where(type => !IsCompilerGenerated(type))
                .SelectMany(type =>
                    type.GetFields(
                        BindingFlags.Static
                            | BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.DeclaredOnly
                    )
                )
                .Where(field => !field.IsInitOnly && !field.IsLiteral)
                .Where(field => !IsCompilerGenerated(field))
                .Select(field => $"{field.DeclaringType!.FullName}.{field.Name}")
                .ToArray();

            Assert.Empty(offenders);
        }

        private static bool IsCompilerGenerated(MemberInfo member)
        {
            return member.IsDefined(
                typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute)
            );
        }

        private static string StripArity(string name)
        {
            var tick = name.IndexOf('`', StringComparison.Ordinal);
            return tick < 0 ? name : name[..tick];
        }

        private static string Describe(NetArchTest.Rules.TestResult result)
        {
            return string.Join(
                Environment.NewLine,
                result.FailingTypes ?? Enumerable.Empty<Type>()
            );
        }
    }
}
