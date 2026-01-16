using Microsoft.ML.Tokenizers;
using System.Net;
using System.Text;
using System.Xml.Linq;

namespace Opc.Ua.RagUtility
{
    internal class DataSlicer
    {
        // We use a static field so the tokenizer (which is large) 
        // is only loaded into memory once.
        private static readonly Tokenizer m_tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");

        /// <summary>
        /// Counts tokens locally without any internet or API access.
        /// </summary>
        public static int CountTokens(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            // CountTokens is highly optimized for just counting 
            // without creating the full list of token IDs.
            return m_tokenizer.CountTokens(text);
        }

        public static DocumentRagChunks Slice(Document document, int maxTokenCount)
        {
            var output = new DocumentRagChunks
            {
                Title = document.Title
            };

            var chunks = output.Chunks = new List<RagChunk>();

            Paragraph section = new();
            Paragraph caption = null;
            List<string> words = new();
            StringBuilder header = new();

            for (int ii = 0; ii < document.Paragraphs.Count; ii++)
            {
                var p = document.Paragraphs[ii];

                if (!Object.ReferenceEquals(section, p.Section) || !Object.ReferenceEquals(p.Caption, caption))
                {
                    if (words.Any())
                    {
                        FlushSectionToChunks(chunks, header.ToString(), words, maxTokenCount);
                        header.Clear();
                        words.Clear();
                    }

                    section = p.Section;
                    caption = p.Caption;

                    header.Clear();
                    header.Append($"Document: {document.Title}\n");

                    if (p.Section != null)
                    {
                        header.Append($"Section: {p?.Section.Number} {p?.Section.ToText()}\n");
                    }

                    if (p.Caption != null)
                    {
                        header.Append($"Caption: {p?.Caption.ToText()}\n");
                    }

                    header.Append("---\n");
                }

                if (p.ParagraphType == SpecialChars.SectionStart)
                {
                    words.AddRange(p.Number);
                }

                words.AddRange(p.Words);
                words.AddRange("\n");
            }

            if (words.Any())
            {
                FlushSectionToChunks(chunks, header.ToString(), words, maxTokenCount);
                words.Clear();
            }

            return output;
        }

        private static List<string> BreakLongText(string text, int maxTokenCount)
        {
            var parts = new List<string>();
            var words = text.Split([' '], StringSplitOptions.RemoveEmptyEntries);

            StringBuilder sb = new();
            int totalTokens = 0;

            for (int ii = 0; ii < words.Length; ii++)
            {
                var word = words[ii];
                var tokenCount = m_tokenizer.CountTokens(word);

                if (totalTokens + tokenCount > maxTokenCount)
                {
                    parts.Add(sb.ToString());
                    sb.Clear();
                }

                while (tokenCount > maxTokenCount)
                {
                    parts.Add(word.Substring(0, maxTokenCount));
                    word = word.Substring(maxTokenCount);
                    tokenCount = m_tokenizer.CountTokens(word);
                }

                sb.Append(word);
                totalTokens += tokenCount;
            }

            if (sb.Length > 0)
            {
                parts.Add(sb.ToString());
            }

            return parts;
        }

        private static void FlushSectionToChunks(List<RagChunk> chunks, string header, List<string> words, int maxTokenCount)
        {
            if (words.Count == 0) return;

            int headerLength = m_tokenizer.CountTokens(header);
            int currentIndex = 0;

            while (currentIndex < words.Count)
            {
                var currentChunkWords = new List<string>();
                int tokenCount = headerLength;

                // Fill chunk until max size
                while (currentIndex < words.Count)
                {
                    string word = words[currentIndex];

                    if (SpecialChars.IsSpecialChar(word))
                    {
                        currentIndex++;
                        continue;
                    }

                    var tokensInWord = m_tokenizer.CountTokens(word);

                    if (tokenCount + tokensInWord > maxTokenCount)
                    {
                        if (!currentChunkWords.Any())
                        {
                            BreakLongText(word, maxTokenCount - headerLength)
                                .ForEach(part =>
                                    chunks.Add(new RagChunk
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        Header = header,
                                        Content = part
                                    }
                                 ));

                            currentIndex++;
                        }

                        break;
                    }

                    currentChunkWords.Add(word);
                    tokenCount += tokensInWord;
                    currentIndex++;
                }

                if (currentChunkWords.Any())
                {
                    Paragraph p = new() { Words = currentChunkWords };

                    var size = m_tokenizer.CountTokens(p.ToText());

                    chunks.Add(new RagChunk
                    {
                        Id = Guid.NewGuid().ToString(),
                        Header = header,
                        Content = p.ToText()
                    });

                    // If there are more words left, backtrack 5 words for the overlap
                    int overlapWords = 5;

                    while (currentIndex < words.Count && currentIndex > 0)
                    {
                        var word = words[currentIndex - 1];

                        if (SpecialChars.IsSpecialChar(word))
                        {
                            currentIndex--;
                            continue;
                        }

                        var tokensInWord = m_tokenizer.CountTokens(word);

                        if (overlapWords - tokensInWord < 0)
                        {
                            break;
                        }

                        overlapWords -= tokensInWord;
                        currentIndex--;
                    }
                }
            }
        }
    }
}
