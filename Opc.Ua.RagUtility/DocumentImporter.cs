using Microsoft.ML.Tokenizers;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace Opc.Ua.RagUtility
{
    internal class DocumentImporter
    {
        public const string UaDocumentNamespace = "http://opcfoundation.org/UA/schemas/UADocument.xsd";

        // We use a static field so the tokenizer (which is large) 
        // is only loaded into memory once.
        private static readonly Tokenizer m_tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");

        /// <summary>
        /// Counts tokens locally without any internet or API access.
        /// </summary>
        public static int Count(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            // CountTokens is highly optimized for just counting 
            // without creating the full list of token IDs.
            return m_tokenizer.CountTokens(text);
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

        private static string ToSimpleText(XElement paragraph)
        {
            StringBuilder sb = new();

            var spans = paragraph.Descendants(XName.Get($"{{{UaDocumentNamespace}}}Span"));

            foreach (var span in spans)
            {
                sb.Append(NormalizeText(span.Value));
            }

            return sb.ToString();
        }

        [Flags]
        public enum CellMasks
        {
            TableStart = 1,
            RowStart = 2,
            CellStart = 4,
            CellEnd = 8,
            RowEnd = 16,
            TableEnd = 32,
            CellMiddle = 64
        }

        private static void ProcessParagraph(Document document, Paragraph section, XElement paragraph)
        {
            var spans = paragraph.Descendants(XName.Get($"{{{UaDocumentNamespace}}}Span"));
            List<string> words = new();

            string cellMaskText = paragraph.Attribute("CellMask")?.Value;
            int cellMask = 0;

            if (cellMaskText != null && Int32.TryParse(cellMaskText, out cellMask))
            {
                if ((cellMask & (int)(CellMasks.TableStart)) != 0)
                {
                    words.Add(SpecialChars.TableStart);
                }

                if ((cellMask & (int)(CellMasks.RowStart)) != 0)
                {
                    words.Add(SpecialChars.RowStart);
                }

                if ((cellMask & (int)(CellMasks.CellStart)) != 0)
                {
                    words.Add(SpecialChars.CellStart);
                }
            }

            string listType = paragraph.Attribute("ListType")?.Value;

            if (listType != null)
            {
                if (listType == "Bullet")
                {
                    words.Add(SpecialChars.Bullet);
                }

                if (listType == "SimpleNumber")
                {
                    words.Add(SpecialChars.Numbered);
                }
            }

            bool bold = false;
            bool italic = false;
            string title = null;
            string paragraphType = null;

            foreach (var span in spans)
            {
                var newBold = span.Attribute("IsBold")?.Value == "true";
                var newItalic = span.Attribute("IsItalic")?.Value == "true";

                if (!bold && newBold)
                {
                    bold = true;
                    words.Add(SpecialChars.BoldStart);
                }

                if (!italic && newItalic)
                {
                    italic = true;
                    words.Add(SpecialChars.ItalicStart);
                }

                if (italic && !newItalic)
                {
                    words.Add(SpecialChars.ItalicEnd);
                    italic = false;
                }

                if (bold && !newBold)
                {
                    words.Add(SpecialChars.BoldEnd);
                    bold = false;
                    continue;
                }

                string imageName = span.Attribute("ImageName")?.Value;

                if (imageName != null)
                {
                    title = imageName;
                    words.Add($"[{imageName}](images/{imageName})");
                    paragraphType = SpecialChars.Figure;
                    continue;
                }

                string fieldType = span.Attribute("FieldType")?.Value;

                if (fieldType != null)
                {
                    string fieldCode = span.Attribute("FieldCode")?.Value;

                    if (fieldType == "Ref")
                    {
                        words.Add($"[{NormalizeText(span.Value)?.Trim()}]({SpecialChars.SectionStart}{fieldCode})");
                        continue;
                    }

                    if (fieldType == "Hyperlink" && fieldCode != null)
                    {
                        var target = span.Value;
                        int index = target.IndexOf("MERGEFORMAT");

                        if (index != -1)
                        {
                            target = target.Substring(index + "MERGEFORMAT".Length + 1).Trim();
                        }

                        words.Add($"[{NormalizeText(target)}]({fieldCode})");
                        continue;
                    }
                }

                var text = NormalizeText(span.Value);

                if (!String.IsNullOrEmpty(text))
                {
                    words.Add(text.Trim());
                }
            }

            if (italic)
            {
                words.Add(SpecialChars.ItalicEnd);
                italic = false;
            }

            if (bold)
            {
                words.Add(SpecialChars.BoldEnd);
                bold = false;
            }

            if (cellMaskText != null)
            {
                if ((cellMask & (int)(CellMasks.CellEnd)) != 0)
                {
                    words.Add(SpecialChars.CellEnd);
                }

                if ((cellMask & (int)(CellMasks.RowEnd)) != 0)
                {
                    words.Add(SpecialChars.RowEnd);
                }

                if ((cellMask & (int)(CellMasks.TableEnd)) != 0)
                {
                    words.Add(SpecialChars.TableEnd);
                }
            }

            var newParagraph = new Paragraph
            {
                Section = section,
                Number = title,
                ParagraphType = paragraphType,
                Words = words
            };

            string style = paragraph.Attribute("Style")?.Value;

            if (style == "FIGURE-title")
            {
                string name = null;

                StringBuilder sb = new();

                foreach (var word in words)
                {
                    if (!SpecialChars.IsSpecialChar(word))
                    {
                        var text = word.TrimStart();

                        if (name == null && text[0] == '-')
                        {
                            name = sb.ToString().Trim();
                            sb.Clear();
                            sb.Append(name);
                            sb.Append(" ");
                            sb.Append(word.TrimStart());
                            continue;
                        }

                        if (sb.Length > 0)
                        {
                            sb.Append(" ");
                        }

                        sb.Append(word);
                    }
                }

                string caption = sb.ToString().Trim();

                words.Clear();
                words.Add(caption);

                document.Links[name] = caption;
                newParagraph.Number = name;
                newParagraph.ParagraphType = SpecialChars.FigureTitle;

                if (document.Paragraphs.Count > 0)
                {
                    var last = document.Paragraphs[^1];
                    document.Links[last.Number] = $"{name} - {caption}";
                    last.Caption = newParagraph;
                }
            }

            document.Paragraphs.Add(newParagraph);
        }

        public static Document Parse(string xmlPath)
        {
            XDocument doc = XDocument.Load(xmlPath);
            var root = doc.Elements(XName.Get($"{{{UaDocumentNamespace}}}UADocument"));
            var main = root.Elements(XName.Get($"{{{UaDocumentNamespace}}}Paragraphs"));
            var paragraphs = main.Elements(XName.Get($"{{{UaDocumentNamespace}}}Paragraph"));

            Document document = new Document()
            {
                Title = GetDocumentTitle(xmlPath),
                Images = new(),
                Links = new(),
                Paragraphs = new()
            };

            Dictionary<string, string> images = new Dictionary<string, string>();

            var imagesInDoc = doc.Descendants(XName.Get($"{{{UaDocumentNamespace}}}Image"));

            foreach (var image in imagesInDoc)
            {
                string imageName = image.Attribute("Name")?.Value;
                string imageData = image.Value;
                if (imageName != null && imageData != null)
                {
                    document.Images[imageName] = imageData;
                }
            }

            var enumerator = paragraphs.GetEnumerator();
            Paragraph section = null;
            Dictionary<string, Paragraph> sectionMap = new();

            while (enumerator.MoveNext())
            {
                var paragraph = enumerator.Current;
                string number = paragraph.Attribute("Number")?.Value;

                if (number != null)
                {
                    var p = AddSection(document, paragraph, number);

                    // some times the section title is in the next paragraph
                    if (p.Words.Count == 0)
                    {
                        if (enumerator.MoveNext())
                        {
                            paragraph = enumerator.Current;
                            p.Words.Add(ToSimpleText(paragraph)?.Trim());
                        }
                    }

                    int index = p.Number.LastIndexOf('.');

                    if (index != -1 && sectionMap.TryGetValue(p.Number.Substring(0, index), out var parent))
                    {
                        p.Section = parent;
                    }

                    sectionMap[p.Number] = p;
                    section = p;
                    continue;
                }

                ProcessParagraph(document, section, paragraph);
            }

            return document;
        }

        private static Paragraph AddSection(Document document, XElement paragraph, string number)
        {
            var title = ToSimpleText(paragraph)?.Trim();
            document.Links[$"{SpecialChars.SectionStart}{number}"] = title;

            var p = new Paragraph
            {
                Number = number,
                ParagraphType = SpecialChars.SectionStart
            };

            if (!String.IsNullOrWhiteSpace(title))
            {
                p.Words.Add(title);
            }

            document.Paragraphs.Add(p);
            return p;
        }

        private static string GetDocumentTitle(string xmlPath)
        {
            var fileName = Path.GetFileName(xmlPath);
            int index = fileName.IndexOf("Part");

            if (index != -1)
            {
                fileName = fileName.Substring(index);
            }

            index = fileName.LastIndexOf(" ");

            if (index != -1)
            {
                fileName = fileName.Substring(0, index);
            }

            return fileName;
        }
    }
}
