using System.Text;
using System.Text.RegularExpressions;

namespace Opc.Ua.RagUtility
{
    internal class Document
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public List<Paragraph> Paragraphs { get; set; } = new();
        public Dictionary<string, string> Links { get; set; } = new();
        public Dictionary<string, string> Images { get; set; } = new();
    }
    internal class Paragraph
    {
        public string Number { get; set; }
        public string ParagraphType { get; set; }
        public List<string> Words { get; set; } = new();
        public Paragraph Section { get; set; }
        public Paragraph Caption { get; set; }

        public string ToText(bool includeSpecials = false)
        {
            StringBuilder sb = new();
            bool trailingWhitespace = false;

            foreach (var word in Words)
            {
                if (!includeSpecials && Regex.IsMatch(word, @"^[§‡†¡¿»«¦·¤•¢¥®]$"))
                {
                    continue;
                }

                if (!trailingWhitespace && word.Length > 0 && !Char.IsPunctuation(word[0]) && !Char.IsWhiteSpace(word[0]))
                {
                    sb.Append(' ');
                }

                sb.Append(word);

                if (word.Length > 0)
                {
                    trailingWhitespace = Char.IsWhiteSpace(word[word.Length - 1]);
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Converts Word/Unicode special chars to safe ASCII to save space and reduce noise.
        /// </summary>
        public static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var sb = new StringBuilder(text);

            for (int ii = 0; ii < sb.Length; ii++)
            {
                switch (sb[ii])
                {
                    case '→':
                    case '\u00A0':
                    case '\u2000':
                    case '\u2001':
                    case '\u2002':
                    case '\u2003':
                    case '\u2004':
                    case '\u2005':
                    case '\u2006':
                    case '\u2007':
                    case '\u2008':
                    case '\u2009':
                    case '\u200A':
                    case '\u200B':
                    case '\u200C':
                    case '\u200D':
                    case '\u202F':
                    case '\u205F':
                    case '\u2060':
                        sb[ii] = ' ';
                        break;

                    case '\u000B':
                    case '\u000C':
                    case '\u000D':
                    case '\u0085':
                    case '\u2028':
                    case '\u2029':
                        sb[ii] = '\n';
                        break;

                    case '\u201c':
                    case '\u201d':
                        sb[ii] = '"';
                        break;

                    case '\u2018':
                    case '\u2019':
                        sb[ii] = '\'';
                        break;

                    case '\u2010': // Hyphen
                    case '\u2011': // Non-breaking hyphen
                    case '\u2012': // Figure dash
                    case '\u2013': // Figure dash
                    case '\u2015': // Horizontal bar
                        sb[ii] = '-';
                        break;

                    case '\u2022':
                        sb[ii] = '*';
                        break;
                }
            }

            return sb.ToString();
        }

        public override string ToString()
        {
            return $"{Number}";
        }
    }

    internal class ImageDescription
    {
        public string Name { get; set; }
        public string Caption { get; set; }
        public string Image { get; set; }
        public List<string> Description { get; set; }
    }

    internal class DocumentImageDescriptions
    {
        public string Title { get; set; }
        public List<ImageDescription> Images { get; set; } = new();
    }

    public class RagChunk
    {
        public string Id { get; set; }
        public string Header { get; set; }
        public string Content { get; set; }
    }

    internal class DocumentRagChunks
    {
        public string Title { get; set; }
        public List<RagChunk> Chunks { get; set; } = new();
    }

    public class DocumentQuery
    {
        public string Prompt { get; set; }
        public string Expected { get; set; }
        public string Actual { get; set; }
    }

    public class DocumentQuerySet
    {
        public string Title { get; set; }
        public List<DocumentQuery> Queries { get; set; } = new();
    }
}