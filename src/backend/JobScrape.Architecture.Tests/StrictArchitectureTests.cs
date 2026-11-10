using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace JobScrape.Architecture.Tests
{
    public sealed class StrictArchitectureTests
    {
        private static readonly Assembly Domain =
            typeof(JobScrape.Domain.DomainException).Assembly;
        private static readonly Assembly Application =
            typeof(JobScrape.Application.Results.Result).Assembly;
        private static readonly Assembly Infrastructure =
            typeof(JobScrape.Infrastructure.Persistence.JobScrapeDbContext).Assembly;
        private static readonly Assembly Api =
            typeof(JobScrape.Api.Program).Assembly;
        private static readonly Assembly Mcp =
            typeof(JobScrape.Mcp.Tools.HealthTools).Assembly;

        private static readonly Assembly[] Production =
            [Domain, Application, Infrastructure, Api, Mcp];

        // Selection is by role: a type is matched by the port it implements,
        // never by where it sits. A rogue implementer cannot pass by spelling
        // its namespace like the sanctioned one.
        private static readonly Type[] RepositoryPorts =
        [
            .. Application
                .GetTypes()
                .Where(type =>
                    type.IsInterface
                    && type.Name.EndsWith("Repository", StringComparison.Ordinal)
                ),
            typeof(JobScrape.Application.Abstractions.IIdempotencyStore),
        ];

        private static readonly Type[] StorePorts =
        [
            typeof(JobScrape.Application.Features.Runs.IRunStore),
            typeof(JobScrape.Application.Features.Sessions.ISessionStore),
            typeof(JobScrape.Application.Features.SearchProfiles.ISearchProfileStore),
        ];

        public static IEnumerable<object[]> LayerScopes =>
        [
            [new LayerScope(Infrastructure, "JobScrape.Api")],
            [new LayerScope(Infrastructure, "JobScrape.Mcp")],
            [new LayerScope(Mcp, "JobScrape.Infrastructure")],
            [new LayerScope(Mcp, "Microsoft.Playwright")],
            [new LayerScope(Mcp, "Microsoft.EntityFrameworkCore")],
            [new LayerScope(Mcp, "AngleSharp")],
            [new LayerScope(Mcp, "ClosedXML")],
            [new LayerScope(Mcp, "Dapper")],
            [new LayerScope(Api, "Microsoft.Playwright")],
            [new LayerScope(Api, "AngleSharp")],
            [new LayerScope(Api, "ClosedXML")],
            [new LayerScope(Api, "Dapper")],
            [new LayerScope(Api, "Microsoft.EntityFrameworkCore")],
        ];

        public static IEnumerable<object[]> ConventionScopes =>
        [
            [
                new ConventionScope(
                    "Handlers",
                    [Application],
                    NameSuffix: "Handler",
                    BaseType: null,
                    ImplementsAny: null,
                    RequiredNamespace: "JobScrape.Application.Features",
                    Visibility: Visibility.Public),
            ],
            [
                new ConventionScope(
                    "Controllers",
                    [Api],
                    NameSuffix: "Controller",
                    BaseType: null,
                    ImplementsAny: null,
                    RequiredNamespace: "JobScrape.Api.Controllers",
                    Visibility: Visibility.Public),
            ],
            [
                new ConventionScope(
                    "Domain events",
                    Production,
                    NameSuffix: null,
                    BaseType: typeof(JobScrape.Domain.Abstractions.DomainEvent),
                    ImplementsAny: null,
                    RequiredNamespace: "JobScrape.Domain",
                    Visibility: Visibility.Any),
            ],
            [
                new ConventionScope(
                    "Repository implementations",
                    Production,
                    NameSuffix: null,
                    BaseType: null,
                    ImplementsAny: RepositoryPorts,
                    RequiredNamespace: "JobScrape.Infrastructure.Persistence.Repositories",
                    Visibility: Visibility.Internal),
            ],
            [
                new ConventionScope(
                    "Persistence stores",
                    Production,
                    NameSuffix: null,
                    BaseType: null,
                    ImplementsAny: StorePorts,
                    RequiredNamespace: "JobScrape.Infrastructure.Persistence",
                    Visibility: Visibility.Public),
            ],
            [
                new ConventionScope(
                    "Source adapters",
                    Production,
                    NameSuffix: null,
                    BaseType: null,
                    ImplementsAny:
                    [
                        typeof(JobScrape.Application.Features.Scraping.IJobSource),
                    ],
                    RequiredNamespace: "JobScrape.Infrastructure.Sources",
                    Visibility: Visibility.Any),
            ],
            [
                new ConventionScope("Aggregate roots", [Domain], null, null, null, null, Visibility.Any,
                    RequiredBaseType: typeof(JobScrape.Domain.Abstractions.AggregateRoot),
                    ShapeFirst: true,
                    Exclude:
                    [
                        typeof(JobScrape.Domain.Identity.PasswordHash),
                        typeof(JobScrape.Domain.Access.PolitenessPolicy),
                        typeof(JobScrape.Domain.Access.RetryPolicy),
                        typeof(JobScrape.Domain.Sourcing.ScrapeRunProgress),
                    ],
                    ExpectedMatches: 9),
            ],
            [
                new ConventionScope("Aggregate constructors", Production, null,
                    typeof(JobScrape.Domain.Abstractions.AggregateRoot), null, null, Visibility.Any,
                    RequireNonPublicCtors: true,
                    ExpectedMatches: 9),
            ],
            [
                new ConventionScope(
                    "DbContext",
                    Production,
                    NameSuffix: "DbContext",
                    BaseType: null,
                    ImplementsAny: null,
                    RequiredNamespace: "JobScrape.Infrastructure.Persistence",
                    Visibility: Visibility.Any),
            ],
        ];

        [Theory]
        [MemberData(nameof(LayerScopes))]
        public void AssemblyAvoidsBannedDependency(LayerScope scope)
        {
            var result = Types
                .InAssembly(scope.Assembly)
                .ShouldNot()
                .HaveDependencyOn(scope.BannedDependency)
                .GetResult();

            Assert.True(result.IsSuccessful, Describe(result));
        }

        [Theory]
        [MemberData(nameof(ConventionScopes))]
        public void ConventionScopeHoldsShape(ConventionScope scope)
        {
            var matched = SelectMatching(scope);
            var violations = matched
                .SelectMany(type => Violations(scope, type))
                .ToList();

            Assert.True(violations.Count == 0, string.Join(
                Environment.NewLine,
                violations
            ));

            if (scope.ExpectedMatches is { } expectedMatches)
            {
                // An over-broad exemption or a silently dropped candidate
                // must fail loudly, never quietly narrow the rule.
                Assert.True(
                    matched.Length == expectedMatches,
                    $"{scope.Description}: expected {expectedMatches} matched types, got {matched.Length}: {string.Join(", ", matched.Select(type => type.FullName))}"
                );
            }
            else
            {
                // A renamed namespace or suffix must fail loudly, never vacate the rule.
                Assert.True(
                    matched.Length > 0,
                    $"{scope.Description}: scope matched no types; the rule guards nothing"
                );
            }
        }

        private static Type[] SelectMatching(ConventionScope scope)
        {
            var candidates = scope
                .Assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type =>
                    type.IsClass
                    && !type.IsAbstract
                    && !IsCompilerGenerated(type)
                );

            // Shape-first rows select concrete, non-record, non-exception
            // classes; role-selected rows keep their subjects.
            if (scope.ShapeFirst)
            {
                candidates = candidates
                    .Where(type => !typeof(Exception).IsAssignableFrom(type))
                    .Where(type => !IsRecord(type));
            }

            return candidates
                .Where(type =>
                    scope.NameSuffix is null
                    || type.Name.EndsWith(scope.NameSuffix, StringComparison.Ordinal)
                )
                .Where(type =>
                    scope.BaseType is null || scope.BaseType.IsAssignableFrom(type)
                )
                .Where(type =>
                    scope.ImplementsAny is null
                    || scope.ImplementsAny.Any(port => port.IsAssignableFrom(type))
                )
                .Where(type =>
                    scope.Exclude is null || !scope.Exclude.Contains(type)
                )
                .ToArray();
        }

        private static IEnumerable<string> Violations(ConventionScope scope, Type type)
        {
            if (!type.IsSealed)
            {
                yield return $"{type.FullName} is not sealed";
            }

            if (
                scope.RequiredBaseType is not null
                && !scope.RequiredBaseType.IsAssignableFrom(type)
            )
            {
                yield return $"{type.FullName} does not inherit {scope.RequiredBaseType}";
            }

            if (
                scope.RequireNonPublicCtors
                && type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length > 0
            )
            {
                yield return $"{type.FullName} exposes a public instance constructor";
            }

            if (
                scope.RequiredNamespace is not null
                && !NamespaceMatches(type.Namespace, scope.RequiredNamespace)
            )
            {
                yield return $"{type.FullName} lives in {type.Namespace}, expected {scope.RequiredNamespace} or a namespace under it";
            }

            switch (scope.Visibility)
            {
                case Visibility.Public when !type.IsPublic && !type.IsNestedPublic:
                    yield return $"{type.FullName} is not public";
                    break;
                case Visibility.Internal when type.IsPublic || type.IsNestedPublic:
                    yield return $"{type.FullName} is public, expected internal";
                    break;
            }
        }

        private static bool NamespaceMatches(string? ns, string prefix)
        {
            // Boundary-exact: "PersistenceLegacy" is not "Persistence".
            return ns is not null
                && (ns.Equals(prefix, StringComparison.Ordinal)
                    || ns.StartsWith(
                        prefix + ".",
                        StringComparison.Ordinal));
        }

        private static bool IsCompilerGenerated(Type type)
        {
            return type.IsDefined(
                typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute),
                inherit: false
            );
        }

        private static bool IsRecord(Type type)
        {
            // Real records carry a compiler-generated <Clone>$ method or a
            // non-public EqualityContract. A public EqualityContract property
            // is just a property; it does not make a type a record.
            const BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic;
            return type.GetMethods(flags).Any(method => method.Name == "<Clone>$")
                || type.GetProperty(
                    "EqualityContract",
                    BindingFlags.Instance | BindingFlags.NonPublic) is not null;
        }

        private static string Describe(NetArchTest.Rules.TestResult result)
        {
            return string.Join(
                Environment.NewLine,
                result.FailingTypes ?? Enumerable.Empty<Type>()
            );
        }

        public enum Visibility
        {
            Any,
            Public,
            Internal,
        }

        public sealed record LayerScope(Assembly Assembly, string BannedDependency)
        {
            public override string ToString()
            {
                return $"{Assembly.GetName().Name} must not depend on {BannedDependency}";
            }
        }

        public sealed record ConventionScope(
            string Description,
            Assembly[] Assemblies,
            string? NameSuffix,
            Type? BaseType,
            Type[]? ImplementsAny,
            string? RequiredNamespace,
            Visibility Visibility,
            Type? RequiredBaseType = null,
            Type[]? Exclude = null,
            bool RequireNonPublicCtors = false,
            int? ExpectedMatches = null,
            bool ShapeFirst = false
        )
        {
            public override string ToString()
            {
                return Description;
            }
        }
    }
}
