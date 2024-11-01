using System.Globalization;
using System.Text;

namespace DDEyC_API.Services
{
    public interface ITextNormalizationService
    {
        string NormalizeTextSelectively(string text);
        string RemoveAllDiacritics(string text);  // New method

    }

    public class TextNormalizationService : ITextNormalizationService
    {
        public string NormalizeTextSelectively(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var words = text.Split(' ');
            var normalizedWords = new List<string>();

            foreach (var word in words)
            {
                if (ContainsDiacritics(word))
                {
                    normalizedWords.Add(RemoveDiacritics(word).ToLowerInvariant());
                }
            }

            return normalizedWords.Count > 0 ? string.Join(" ", normalizedWords) : string.Empty;
        }
        public string RemoveAllDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return RemoveDiacritics(text);
        }
        private bool ContainsDiacritics(string text) =>
            text.Normalize(NormalizationForm.FormD)
                .Any(c => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark);

        private string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}