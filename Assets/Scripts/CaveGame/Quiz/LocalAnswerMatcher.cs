using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CaveGame.Quiz
{
    public static class LocalAnswerMatcher
    {
        public static bool IsMatch(string transcript, QuizQuestion question)
        {
            string value = Normalize(transcript);
            if (value.Length == 0 || question == null) return false;
            foreach (string answer in question.AllAcceptedAnswers())
            {
                string expected = Normalize(answer);
                if (expected.Length > 0 && (value == expected || value.Contains(expected) || expected.Contains(value)))
                    return true;
            }
            return false;
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) builder.Append(c);
                else if (char.IsWhiteSpace(c)) builder.Append(' ');
            }
            return Regex.Replace(builder.ToString(), "\\s+", " ").Trim();
        }
    }
}
