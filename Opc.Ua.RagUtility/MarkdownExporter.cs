using System.Text;
using System.Text.RegularExpressions;

namespace Opc.Ua.RagUtility
{
    internal class MarkdownExporter
    {
        public static async Task SaveAsMarkdown(Document document, string directory)
        {
            using var writer = new StreamWriter(Path.Combine(directory, "README.md"));

            string documentTitle = document.Title;
            List<string> words = new List<string>();

            bool inTable = false;
            bool inTableCell = false;
            bool tableHeaderProcessed = false;
            int columnCount = 0;

            foreach (var paragraph in document.Paragraphs)
            {
                bool nospace = true;

                for (int ii = 0; ii < paragraph.Words.Count; ii++)
                {
                    var word= paragraph.Words[ii];

                    if (paragraph.ParagraphType == SpecialChars.SectionStart)
                    {
                        foreach (var ch in paragraph.Number)
                        {
                            if (ch == '.')
                            {
                                await writer.WriteAsync("#");
                            }
                        }

                        await writer.WriteAsync("## ");
                        await writer.WriteAsync(paragraph.Number);
                        await writer.WriteAsync(" ");
                        await writer.WriteAsync(paragraph.ToText());
                        nospace = true;
                        continue;
                    }

                    if (word == SpecialChars.TableStart)
                    {
                        inTable = true;
                        tableHeaderProcessed = false;
                        columnCount = 0;
                        continue;
                    }

                    if (word == SpecialChars.CellStart)
                    {
                        if (inTable)
                        {
                            inTableCell = true;
                            columnCount++;
                        }

                        continue;
                    }

                    if (word == SpecialChars.CellEnd)
                    {
                        if (inTable)
                        {
                            await writer.WriteAsync("|");
                            inTableCell = false;
                        }

                        continue;
                    }

                    if (word == SpecialChars.RowStart)
                    {
                        if (inTable)
                        {
                            await writer.WriteAsync("|");
                        }

                        continue;
                    }

                    if (word == SpecialChars.RowEnd)
                    {
                        if (inTable)
                        {
                            await writer.WriteAsync("\n");

                            if (!tableHeaderProcessed)
                            {
                                for (int i = 0; i < columnCount; i++)
                                {
                                    await writer.WriteAsync("|---");
                                }

                                await writer.WriteAsync("|\n");
                                tableHeaderProcessed = true;
                            }
                        }

                        continue;
                    }

                    if (word == SpecialChars.TableEnd)
                    {
                        inTable = false;
                        continue;
                    }

                    if (word == SpecialChars.Bullet)
                    {
                        await writer.WriteAsync("* ");
                        nospace = true;
                        continue;
                    }

                    if (word == SpecialChars.Numbered)
                    {
                        await writer.WriteAsync("1. ");
                        nospace = true;
                        continue;
                    }

                    if (word == SpecialChars.BoldStart)
                    {
                        await writer.WriteAsync(" **");
                        nospace = true;
                        continue;
                    }

                    if (word == SpecialChars.ItalicStart)
                    {
                        if (!nospace)
                        {
                            await writer.WriteAsync(" ");
                        }

                        await writer.WriteAsync("*");
                        nospace = true;
                        continue;
                    }

                    if (word == SpecialChars.ItalicEnd)
                    {
                        await writer.WriteAsync("*");
                        nospace = false;
                        continue;
                    }

                    if (word == SpecialChars.BoldEnd)
                    {
                        await writer.WriteAsync("** ");
                        nospace = true;
                        continue;
                    }

                    if (!nospace && !word.StartsWith('-'))
                    {
                        await writer.WriteAsync(" ");
                    }

                    if (paragraph.ParagraphType == SpecialChars.Figure)
                    {
                        await writer.WriteAsync("!");
                    }

                    await writer.WriteAsync(EscapeMarkdown(word));
                    nospace = false;
                }

                if (!inTable || inTableCell)
                {
                    await writer.WriteAsync("  \n\n");
                }
            }

            var imagesFolder = Directory.CreateDirectory(Path.Combine(directory, "images"));

            foreach (var image in document.Images)
            {
                var bytes = Convert.FromBase64String(image.Value);
                await File.WriteAllBytesAsync(Path.Combine(imagesFolder.FullName, image.Key), bytes);
            }

            await writer.FlushAsync();
            writer.Close();
        }

        private static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 1. Escape the "always dangerous" characters globally
            // These include: \ ` * _ { } [ ] ( ) # < > !
            string escaped = Regex.Replace(text, @"([\\`*_\{\}#<>!])", @"\$1");

            // 2. Escape dots (.), plus (+), and hyphens (-) ONLY if they start a line
            // or follow a number at the start of a line (for ordered lists).
            // ^[ \t]*\d+\. -> Matches "  1." at start of line
            // ^[ \t]*[-+]  -> Matches "  -" or "  +" at start of line
            escaped = Regex.Replace(escaped, @"(?m)^([ \t]*\d+)\.", @"$1\.");
            escaped = Regex.Replace(escaped, @"(?m)^([ \t]*)([-+])", @"$1\$2");
            
            return escaped;
        }
    }
}