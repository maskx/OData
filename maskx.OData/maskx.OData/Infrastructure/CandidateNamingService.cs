using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace maskx.OData.Infrastructure
{
    /// <summary>
    /// <see cref="efcore\src\EFCore.Design\Scaffolding\Internal\CandidateNamingService.cs"/>
    /// </summary>
    public class CandidateNamingService
    {
        private static string GenerateCandidateIdentifier(string originalIdentifier)
        {
            var candidateStringBuilder = new StringBuilder();
            var previousLetterCharInWordIsLowerCase = false;
            var isFirstCharacterInWord = true;
            foreach (var c in originalIdentifier)
            {
                var isNotLetterOrDigit = !char.IsLetterOrDigit(c);
                if (isNotLetterOrDigit
                    || (previousLetterCharInWordIsLowerCase && char.IsUpper(c)))
                {
                    isFirstCharacterInWord = true;
                    previousLetterCharInWordIsLowerCase = false;
                    if (isNotLetterOrDigit)
                    {
                        continue;
                    }
                }

                candidateStringBuilder.Append(
                    isFirstCharacterInWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                isFirstCharacterInWord = false;
                if (char.IsLower(c))
                {
                    previousLetterCharInWordIsLowerCase = true;
                }
            }

            return candidateStringBuilder.ToString();
        }

        private static string FindCommonPrefix(string firstName, IEnumerable<string> propertyNames)
        {
            var prefixLength = 0;
            foreach (var c in firstName)
            {
                foreach (var s in propertyNames)
                {
                    if (s.Length <= prefixLength
                        || s[prefixLength] != c)
                    {
                        return firstName.Substring(0, prefixLength);
                    }
                }

                prefixLength++;
            }

            return firstName.Substring(0, prefixLength);
        }

        private static string StripId(string commonPrefix)
        {
            if (commonPrefix.Length < 3
                || !commonPrefix.EndsWith("id", StringComparison.OrdinalIgnoreCase))
            {
                return commonPrefix;
            }

            int i;
            for (i = commonPrefix.Length - 3; i >= 0; i--)
            {
                if (char.IsLetterOrDigit(commonPrefix[i]))
                {
                    break;
                }
            }

            return i != 0
                ? commonPrefix.Substring(0, i + 1)
                : commonPrefix;
        }

        public static string GetDependentEndCandidateNavigationPropertyName(ForeignKey foreignKey)
        {
            var candidateName = FindCandidateNavigationName(foreignKey.DeclaringProperties);

            return !string.IsNullOrEmpty(candidateName) ? candidateName : foreignKey.PrincipalEntityType.Name;
        }
        public static string GetPrincipalEndCandidateNavigationPropertyName(
           ForeignKey foreignKey,
           string dependentEndNavigationPropertyName)
        {
            var allForeignKeysBetweenDependentAndPrincipal =
                foreignKey.PrincipalEntityType?
                    .GetForeignKeys()
                    .Where(fk => foreignKey.DeclaringEntityType == fk.DeclaringEntityType);

            return allForeignKeysBetweenDependentAndPrincipal?.Count() > 1
                ? foreignKey.DeclaringEntityType.Name
                + dependentEndNavigationPropertyName
                : foreignKey.DeclaringEntityType.Name;
        }
        private static string FindCandidateNavigationName(IEnumerable<Property> properties)
        {
            if (!properties.Any())
            {
                return string.Empty;
            }

            var candidateName = string.Empty;
            var firstProperty = properties.First();
            if (properties.Count() == 1)
            {
                candidateName = firstProperty.Name;
            }
            else
            {
                candidateName = FindCommonPrefix(firstProperty.Name, properties.Select(p => p.Name));
            }

            return StripId(candidateName);
        }
    }
}
