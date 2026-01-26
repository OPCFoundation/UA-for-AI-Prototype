using System.Text;
using System.Text.RegularExpressions;

namespace Opc.Ua.RagUtility
{
    internal class MarkdownExporter
    {
        private class RowInfo
        {
            public int Paragraph { get; set; }

            public int Word { get; set; }

            public bool IsHeader { get; set; }

            public int ColumnCount { get; set; }
        }

        private static List<RowInfo> GetRowColumnCount(IList<Paragraph> paragraphs, int index)
        {
            List<RowInfo> rows = new();
            bool started = false;
            bool ended = false;
            bool skipping = false;
            bool isBold = false;
            RowInfo row = null;

            for (int jj = index; jj < paragraphs.Count; jj++)
            {
                var paragraph = paragraphs[jj];

                for (int ii = 0; ii < paragraph.Words.Count; ii++)
                {
                    var word = paragraph.Words[ii];

                    if (started && word == SpecialChars.TableStart)
                    {
                        skipping = true;
                        continue;
                    }

                    if (skipping && word == SpecialChars.TableEnd)
                    {
                        skipping = false;
                        continue;
                    }

                    if (!started)
                    {
                        if (word == SpecialChars.TableStart)
                        {
                            started = true;
                        }

                        continue;
                    }

                    if (word == SpecialChars.TableEnd)
                    {
                        ended = true;
                        break;
                    }

                    if (word == SpecialChars.RowStart)
                    {
                        row = new RowInfo() { Paragraph = jj, Word = ii, ColumnCount = 0 };
                        continue;
                    }

                    if (word == SpecialChars.RowEnd)
                    {
                        rows.Add(row);
                        continue;
                    }

                    if (word == SpecialChars.CellStart)
                    {
                        if (ii < paragraph.Words.Count - 1)
                        {
                            isBold = paragraph.Words[ii + 1] == SpecialChars.BoldStart;
                        }

                        row.ColumnCount++;
                        continue;
                    }

                    if (word == SpecialChars.CellEnd)
                    {
                        if (isBold && ii > 0)
                        {
                            row.IsHeader = paragraph.Words[ii - 1] == SpecialChars.BoldEnd;
                        }

                        continue;
                    }
                }

                if (!skipping && ended)
                {
                    break;
                }
            }
                            
            return rows;
        }


        public static async Task SaveAsMarkdown(Document document, string directory)
        {
            using var writer = new StreamWriter(Path.Combine(directory, "README.md"));

            string documentTitle = document.Title;
            List<string> words = new();
            List<RowInfo> rows = new();

            bool inTable = false;
            bool skippingTable = false;
            bool inTableCell = false;
            int rowCount = 0;

            for (int jj = 0; jj < document.Paragraphs.Count; jj++)
            {
                var paragraph = document.Paragraphs[jj];
          
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

                    if (inTable && word == SpecialChars.TableStart)
                    {                        
                        skippingTable = true;
                        continue;
                    }

                    if (skippingTable && word == SpecialChars.TableEnd)
                    {
                        skippingTable = false;
                        continue;
                    }

                    if (word == SpecialChars.TableStart)
                    {
                        rows = GetRowColumnCount(document.Paragraphs, jj);
                        inTable = true;
                        continue;
                    }

                    if (word == SpecialChars.CellStart)
                    {
                        if (inTable)
                        {
                            inTableCell = true;
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
                        if (skippingTable || !inTable)
                        {
                            continue;
                        }

                        if (rowCount == 0 && rows.Count > rowCount && !rows[rowCount].IsHeader)
                        {
                            for (int column = 0; column < rows[rowCount].ColumnCount; column++)
                            {
                                await writer.WriteAsync("|");
                            }

                            await writer.WriteAsync("|\n");

                            for (int column = 0; column < rows[rowCount].ColumnCount; column++)
                            {
                                await writer.WriteAsync("|---");
                            }

                            await writer.WriteAsync("|\n");
                        }

                        if (rows.Count > rowCount && rows[rowCount].IsHeader)
                        {
                            if (rowCount > 0)
                            {
                                await writer.WriteAsync("  \n");
                            }
                        }

                        await writer.WriteAsync("|");
                        continue;
                    }

                    if (word == SpecialChars.RowEnd)
                    {
                        if (skippingTable || !inTable)
                        {
                            continue;
                        }

                        await writer.WriteAsync("\n");

                        if (rows.Count > rowCount && rows[rowCount].IsHeader)
                        {
                            for (int column = 0; column < rows[rowCount].ColumnCount; column++)
                            {
                                await writer.WriteAsync("|---");
                            }

                            await writer.WriteAsync("|\n");
                        }

                        rowCount++;
                        continue;
                    }

                    if (word == SpecialChars.TableEnd)
                    {
                        if (skippingTable || !inTable)
                        {
                            skippingTable = false;
                            continue;
                        }

                        inTable = false;
                        rowCount = 0;
                        rows = null;
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

                if (!inTable)
                {
                    await writer.WriteAsync("  \n\n");
                }
                else if (inTableCell)
                {
                    await writer.WriteAsync("<br>");
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