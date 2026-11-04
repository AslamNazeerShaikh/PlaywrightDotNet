using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Decides whether a posting's skills satisfy a profile's required key
    /// skills. The rule is deliberately narrow: an empty required list
    /// passes everything, otherwise at least one required skill must equal
    /// one posting skill ignoring case. Equality (not substring) keeps short
    /// names like <c>C</c> from matching <c>CSS</c>.
    /// </summary>
    public static class SkillMatcher
    {
        /// <summary>
        /// Tests a posting's skills against the required list.
        /// </summary>
        /// <param name="postingSkills">The posting's skills; null means none observed.</param>
        /// <param name="requiredSkills">The profile's required skills; null or empty means no filter.</param>
        /// <returns>True when no filter applies or any required skill equals any posting skill.</returns>
        public static bool Matches(
            IReadOnlyList<JobSkill>? postingSkills,
            IReadOnlyList<string?>? requiredSkills
        )
        {
            return Matches(
                postingSkills?.Select(skill => skill.Name),
                requiredSkills
            );
        }

        /// <summary>
        /// Tests raw skill names against the required list. Blank names on
        /// either side are ignored because adapters emit empties.
        /// </summary>
        /// <param name="postingSkillNames">The posting's skill names; null means none observed.</param>
        /// <param name="requiredSkills">The profile's required skills; null or empty means no filter.</param>
        /// <returns>True when no filter applies or any required skill equals any posting skill.</returns>
        public static bool Matches(
            IEnumerable<string?>? postingSkillNames,
            IEnumerable<string?>? requiredSkills
        )
        {
            var required = (requiredSkills ?? [])
                .Select(Text.Collapse)
                .Where(skill => skill.Length > 0)
                .ToArray();
            if (required.Length == 0)
            {
                return true;
            }

            var posted = (postingSkillNames ?? [])
                .Select(Text.Collapse)
                .Where(skill => skill.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return required.Any(posted.Contains);
        }
    }
}
