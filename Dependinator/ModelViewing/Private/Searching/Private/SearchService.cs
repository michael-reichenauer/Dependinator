using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.ModelViewing.Private.ModelHandling.Private;
using Dependinator.Utils.Dependencies;


namespace Dependinator.ModelViewing.Private.Searching.Private
{
    [SingleInstance]
    internal class SearchService : ISearchService
    {
        private readonly IModelService modelService;

        private readonly Dictionary<string, Regex> regexCache = new Dictionary<string, Regex>();


        public SearchService(IModelService modelService)
        {
            this.modelService = modelService;
        }


        public IEnumerable<NodeName> Search(string text)
        {
            text = text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return Enumerable.Empty<NodeName>();
            }

            if (text.All(c => c != '.' && (!char.IsLetter(c) || char.IsLower(c))))
            {
                return SimpleSearch(text);
            }
            else
            {
                // Handle Camel case search and '.' as start of word 
                return RegexSearch(text);
            }
        }


        private IEnumerable<NodeName> SimpleSearch(string text) =>
            modelService.AllNodes
                .Where(node => IsSimpleMatch(text, node))
                .Select(node => node.Name);


        private IEnumerable<NodeName> RegexSearch(string text)
        {
            Regex regex = GetRegex(text);

            return modelService.AllNodes
                .Where(node => IsRegExMatch(regex, node))
                .Select(node => node.Name);
        }


        private static bool IsSimpleMatch(string text, Node node)
        {
            string name = node.Name.DisplayLongName;
            int index = name.IndexOf("(");
            if (index > -1)
            {
                // Do not search in parameters
                return -1 != name.IndexOf(text, 0, index, StringComparison.OrdinalIgnoreCase);
            }

            return -1 != name.IndexOf(text, StringComparison.OrdinalIgnoreCase);
        }


        private static bool IsRegExMatch(Regex regex, Node node)
        {
            string name = node.Name.DisplayLongName;

            int index = name.IndexOf("(");
            if (index > -1)
            {
                // Do not search in parameters
                name = name.Substring(0, index - 1);
            }

            return regex.IsMatch(name);
        }


        private Regex GetRegex(string text)
        {
            if (regexCache.TryGetValue(text, out Regex regex))
            {
                return regex;
            }

            string escapedText = EscapeSpecialChars(text);

            string pattern = "";
            foreach (char c in escapedText)
            {
                if (char.IsLetter(c))
                {
                    if (char.IsUpper(c))
                    {
                        // Uppercase is treated as start of word
                        pattern += $"[^A-Z\\.]*{c}";
                    }
                    else
                    {
                        //lower case case match both up and lower chars
                        pattern += $"[{c}{char.ToUpper(c)}]";
                    }
                }
                else if (c == '.')
                {
                    // A '.' marks begin of next word
                    // in case of a '.' the previous char was a '\', which needs to be removed
                    pattern = pattern.Substring(0, pattern.Length - 1) + "[^\\.]*\\.";
                }
                else
                {
                    pattern += c;
                }
            }

            regex = new Regex(pattern, RegexOptions.Compiled);

            regexCache[text] = regex;

            return regex;
        }


        private static string EscapeSpecialChars(string text) =>
            text
                .Replace("\\", "\\\\")
                .Replace(".", "\\.")
                .Replace("$", "\\$")
                .Replace("{", "\\{")
                .Replace("[", "\\[")
                .Replace("(", "\\(")
                .Replace("|", "\\|")
                .Replace(")", "\\)")
                .Replace("*", "\\*")
                .Replace("+", "\\+")
                .Replace("?", "\\?");
    }
}
