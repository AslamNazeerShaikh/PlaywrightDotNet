using JobScrape.Domain.Sourcing;
using Xunit;

namespace JobScrape.Domain.Tests.Sourcing
{
    public sealed class PostingTextValueTests
    {
        [Fact]
        public void ExternalJobIdTrimsAndKeepsValue()
        {
            var id = ExternalJobId.Create("  123456789 ");

            Assert.Equal("123456789", id.Value);
            Assert.Equal("123456789", id.ToString());
            Assert.Equal(ExternalJobId.Create("123456789"), id);
        }

        [Fact]
        public void ExternalJobIdAcceptsMaxLength()
        {
            var raw = new string('a', ExternalJobId.MaxLength);

            Assert.Equal(raw, ExternalJobId.Create(raw).Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        [InlineData("12 34")]
        [InlineData("12\u000134")]
        public void ExternalJobIdRejectsBlankOrMalformed(string? raw)
        {
            Assert.Throws<DomainException>(() => ExternalJobId.Create(raw));
        }

        [Fact]
        public void ExternalJobIdRejectsOverMaxLength()
        {
            Assert.Throws<DomainException>(() =>
                ExternalJobId.Create(new string('a', ExternalJobId.MaxLength + 1))
            );
        }

        [Fact]
        public void JobTitleCollapsesWhitespace()
        {
            var title = JobTitle.Create("  Senior \n .NET   Developer ");

            Assert.Equal("Senior .NET Developer", title.Value);
            Assert.Equal(title.Value, title.ToString());
        }

        [Fact]
        public void JobTitleAcceptsMaxLength()
        {
            var raw = new string('t', JobTitle.MaxLength);

            Assert.Equal(raw, JobTitle.Create(raw).Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("bad\u0000title")]
        public void JobTitleRejectsBlankOrControlCharacters(string? raw)
        {
            Assert.Throws<DomainException>(() => JobTitle.Create(raw));
        }

        [Fact]
        public void JobTitleRejectsOverMaxLength()
        {
            Assert.Throws<DomainException>(() =>
                JobTitle.Create(new string('t', JobTitle.MaxLength + 1))
            );
        }

        [Fact]
        public void CompanyNameCollapsesWhitespace()
        {
            var company = CompanyName.Create(" Contoso   Ltd ");

            Assert.Equal("Contoso Ltd", company.Value);
            Assert.Equal(company.Value, company.ToString());
        }

        [Fact]
        public void CompanyNameAcceptsMaxLength()
        {
            var raw = new string('c', CompanyName.MaxLength);

            Assert.Equal(raw, CompanyName.Create(raw).Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Con\u0007toso")]
        public void CompanyNameRejectsBlankOrControlCharacters(string? raw)
        {
            Assert.Throws<DomainException>(() => CompanyName.Create(raw));
        }

        [Fact]
        public void CompanyNameRejectsOverMaxLength()
        {
            Assert.Throws<DomainException>(() =>
                CompanyName.Create(new string('c', CompanyName.MaxLength + 1))
            );
        }

        [Fact]
        public void JobSkillKeepsDisplayNameAndLowercaseKey()
        {
            var skill = JobSkill.Create("  ASP.NET   Core ");

            Assert.Equal("ASP.NET Core", skill.Name);
            Assert.Equal("asp.net core", skill.NormalizedName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(" ")]
        [InlineData("C\u0000#")]
        public void JobSkillRejectsBlankOrControlCharacters(string? raw)
        {
            Assert.Throws<DomainException>(() => JobSkill.Create(raw));
        }
    }
}
