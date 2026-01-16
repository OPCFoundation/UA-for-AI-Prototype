using System.CommandLine;
using System.Text.Json;
using Opc.Ua.RagUtility;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Provides a suite of utility functions which help with Retrieval-Augmented Generation (RAG) for OPC UA specifications.");
        rootCommand.Subcommands.Add(GenerateMarkdown());
        rootCommand.Subcommands.Add(DescribeImages());
        rootCommand.Subcommands.Add(GenerateRagChunks());
        rootCommand.Subcommands.Add(EmbedSpecification());
        rootCommand.Subcommands.Add(PromptModel());

        //var result = rootCommand.Parse([
        //    "markdown",
        //    "--input", @"D:\Work\OPC\OPC-UA-for-AI\data\OPC 10000-6 - UA Specification Part 6 - Mappings 1.05.06.xml",
        //    "--output", @"D:\Work\OPC\OPC-UA-for-AI\data\Part6"
        //]);

        //var result = rootCommand.Parse([
        //    "describe-images",
        //    "--input", @"D:\Work\OPC\OPC-UA-for-AI\data\OPC 10000-6 - UA Specification Part 6 - Mappings 1.05.06.xml",
        //    "--output", @"D:\Work\OPC\OPC-UA-for-AI\data\Part6\image-descriptions.json"
        //]);

        //var result = rootCommand.Parse([
        //    "generate-chunks",
        //    "--input", @"D:\Work\OPC\OPC-UA-for-AI\data\OPC 10000-6 - UA Specification Part 6 - Mappings 1.05.06.xml",
        //    "--output", @"D:\Work\OPC\OPC-UA-for-AI\data\Part6\rag-chunks.json",
        //    "--images", @"D:\Work\OPC\OPC-UA-for-AI\data\Part6\image-descriptions.json"
        //]);

        //var result = rootCommand.Parse([
        //    "embed",
        //    "--input", @"D:\Work\OPC\UA-for-AI-Prototype\specifications\Core\Part1\rag-chunks.json"
        //]);

        //var result = rootCommand.Parse([
        //    "prompt",
        //    "--input", @"D:\Work\OPC\OPC-UA-for-AI\data\Part1\queries.json"
        //]);

        var result = rootCommand.Parse(args);
        return await result.InvokeAsync();
    }

    static Command GenerateMarkdown()
    {
        var command = new Command(
            "markdown",
            "Converts a OPC UA specification XML dump to a markdown file."
        );

        var inputOption = new Option<string>("--input", ["-i"])
        {
            Required = false,
            Description = "The XML dump for the OPC UA specification."
        };

        inputOption.Validators.Add(result =>
        {
            var input = result.GetValueOrDefault<string>();
            if (!File.Exists(input))
            {
                result.AddError("The input file does not exist.");
            }
        });

        var outputOption = new Option<string>("--output", ["-o"])
        {
            Required = false,
            Description = "The output directory (writes to README.md with images in 'images' subdir)."
        };

        command.Options.Add(inputOption);
        command.Options.Add(outputOption);

        command.SetAction(async result =>
        {
            try
            {
                var input = result.GetValue(inputOption);
                var output = result.GetValue(outputOption);

                if (!Directory.Exists(output))
                {
                    Directory.CreateDirectory(output);
                }

                var document = DocumentImporter.Parse(input);
                await MarkdownExporter.SaveAsMarkdown(document, output);

                Console.WriteLine($"Converted XML from {input} to Markdown at {output}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: [{e.GetType().Name}] {e.Message}");
            }
        });

        return command;
    }

    static Command DescribeImages()
    {
        var command = new Command(
            "describe-images",
            "Describes the images in a OPC UA specification."
        );

        var inputOption = new Option<string>("--input", ["-i"])
        {
            Required = false,
            Description = "The XML dump for the OPC UA specification."
        };

        inputOption.Validators.Add(result =>
        {
            var input = result.GetValueOrDefault<string>();
            if (!File.Exists(input))
            {
                result.AddError("The input file does not exist.");
            }
        });

        var ollamaUrlOption = GetUrlOption(
            "The base URL for the Ollama agent.",
            "http://localhost:11434",
            "--agent", ["-a"]);

        var imageDescriptionModelOption = new Option<string>("--model", ["-m"])
        {
            Required = false,
            Description = "The model to use to describe images (default: llava).",
            DefaultValueFactory = (result) => "llava"
        };

        var timeoutOption = new Option<int>("--timeout", ["-t"])
        {
            Required = false,
            Description = "The HTTP timeout in seconds (must be long enough to handle resouce intensive AI queries).",
            DefaultValueFactory = (result) => 300
        };

        var outputOption = new Option<string>("--output", ["-o"])
        {
            Required = false,
            Description = "A JSON file containing the images and their descriptions."
        };

        command.Options.Add(inputOption);
        command.Options.Add(ollamaUrlOption);
        command.Options.Add(imageDescriptionModelOption);
        command.Options.Add(timeoutOption);
        command.Options.Add(outputOption);

        command.SetAction((Func<ParseResult, Task>)(async result =>
        {
            try
            {
                var input = result.GetValue(inputOption);
                var ollamaUrl = result.GetValue(ollamaUrlOption);
                var imageDescriptionModel = result.GetValue(imageDescriptionModelOption);
                var timeout = result.GetValue(timeoutOption);
                var output = result.GetValue(outputOption);

                using var ollama = new OllamaClient(new Uri(ollamaUrl), new TimeSpan(0, timeout, 0));
                var document = DocumentImporter.Parse(input);

                var images = new DocumentImageDescriptions()
                {
                    Title = document.Title,
                    Images = new List<ImageDescription>()
                };

                for (int ii = 0; ii < document.Paragraphs.Count; ii++)
                {
                    var paragraph = document.Paragraphs[ii];

                    if (paragraph.ParagraphType == SpecialChars.Figure)
                    {
                        Paragraph caption = paragraph.Caption;

                        var image = document.Images.ContainsKey(paragraph.Number) ? document.Images[paragraph.Number] : null;

                        if (image != null)
                        {
                            var description = await ollama.DescribeImageAsync(
                                document.Title,
                                (paragraph.Section != null) ? $"{paragraph.Section.Number}: {paragraph.Section.ToText()}" : null,
                                caption?.ToText(),
                                image,
                                imageDescriptionModel).ConfigureAwait(false);

                            paragraph.Caption = caption;
                            Console.WriteLine($"Described figure {paragraph.Number}: {description.Substring(0, Math.Min(50, description.Length))}...");

                            var text = Paragraph.NormalizeText(description)
                                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .ToList();

                            images.Images.Add(new ImageDescription()
                            {
                                Name = paragraph.Number,
                                Caption = caption?.ToText(),
                                Image = image,
                                Description = text
                            });
                        }
                    }
                }

                using var ostrm = File.Create(output);

                await JsonSerializer.SerializeAsync(ostrm, images, new JsonSerializerOptions()
                {
                    WriteIndented = true

                }).ConfigureAwait(false);

                Console.WriteLine($"Saved Images from '{document.Title}' to '{output}'");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: [{e.GetType().Name}] {e.Message}");
            }
        }));

        return command;
    }

    static Command GenerateRagChunks()
    {
        var command = new Command(
            "generate-chunks",
            "Generates chunks suitable for RAG embedding in a model."
        );

        var inputOption = new Option<string>("--input", ["-i"])
        {
            Required = false,
            Description = "The XML dump for the OPC UA specification."
        };

        inputOption.Validators.Add(result =>
        {
            var input = result.GetValueOrDefault<string>();
            if (!File.Exists(input))
            {
                result.AddError("The input file does not exist.");
            }
        });

        var maxTokenCountOption = new Option<uint>("--tokens", ["-t"])
        {
            Required = false,
            Description = "The chunk size in tokens (default: 400).",
            DefaultValueFactory = (result) => 400u
        };

        var imagesOption = new Option<string>("--images", ["-m"])
        {
            Required = false,
            Description = "The file containing the previously generated image descriptions."
        };

        var outputOption = new Option<string>("--output", ["-o"])
        {
            Required = false,
            Description = "A JSON file containing the chunks."
        };

        command.Options.Add(inputOption);
        command.Options.Add(maxTokenCountOption);
        command.Options.Add(imagesOption);
        command.Options.Add(outputOption);

        command.SetAction((Func<ParseResult, Task>)(async result =>
        {
            try
            {
                var input = result.GetValue(inputOption);
                var output = result.GetValue(outputOption);
                var imageFilePath = result.GetValue(imagesOption);
                var maxTokenCount = (int)result.GetValue(maxTokenCountOption);

                DocumentImageDescriptions images = null;

                if (File.Exists(imageFilePath))
                {
                    using var istrm = File.OpenRead(imageFilePath);
                    images = await JsonSerializer.DeserializeAsync<DocumentImageDescriptions>(istrm).ConfigureAwait(false);
                }

                var document = DocumentImporter.Parse(input);

                if (images != null)
                {
                    for (int ii = 0; ii < document.Paragraphs.Count; ii++)
                    {
                        var paragraph = document.Paragraphs[ii];

                        if (paragraph.ParagraphType == SpecialChars.Figure)
                        {
                            var description = images.Images.Find(img => img.Name == paragraph.Number);

                            if (description?.Description != null)
                            {
                                paragraph.Words.Clear();
                                paragraph.Words.AddRange(description.Description);
                            }
                        }
                    }
                }

                var chunks = DataSlicer.Slice(document, maxTokenCount);
                using var ostrm = File.Create(output);

                await JsonSerializer.SerializeAsync(ostrm, chunks, new JsonSerializerOptions()
                {
                    WriteIndented = true

                }).ConfigureAwait(false);

                Console.WriteLine($"Saved RAG Chunks from '{document.Title}' to '{output}'");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: [{e.GetType().Name}] {e.Message}");
            }
        }));

        return command;
    }

    static Command EmbedSpecification()
    {
        var command = new Command(
            "embed",
            "Embeds an OPC UA specification using an Ollama model and stores the result in Qdrant DB."
        );

        var inputOption = new Option<string>("--input", ["-i"])
        {
            Required = false,
            Description = "A JSON file with the chunks generated from an OPC UA specification."
        };

        inputOption.Validators.Add(result =>
        {
            var input = result.GetValueOrDefault<string>();
            if (!File.Exists(input))
            {
                result.AddError("The input file does not exist.");
            }
        });

        var ollamaUrlOption = GetUrlOption(
            "The base URL for the Ollama agent.",
            "http://localhost:11434",
            "--agent", ["-a"]);

        var embeddingModelOption = new Option<string>("--embed", ["-em"])
        {
            Required = false,
            Description = "The model to use to embedded the chunks (default: mxbai-embed-large).",
            DefaultValueFactory = (result) => "mxbai-embed-large"
        };

        Option<string> qdrantUrlOption = GetUrlOption(
            "The base URL for the Quadrant DB.",
            "http://localhost:6333",
            "--db", ["-d"]);

        var timeoutOption = new Option<int>("--timeout", ["-t"])
        {
            Required = false,
            Description = "The HTTP timeout in seconds (must be long enough to handle resouce intensive AI queries).",
            DefaultValueFactory = (result) => 300
        };

        var collectionNameOption = new Option<string>("--collection", ["-n"])
        {
            Required = false,
            Description = "The name of the vector collection in the Qdrant database.",
            DefaultValueFactory = (result) => "opcua-specifications"
        };

        var deletedExistingOption = new Option<bool>("--delete", ["-d"])
        {
            Arity = ArgumentArity.Zero,
            Required = false,
            DefaultValueFactory = (result) => false
        };

        command.Options.Add(inputOption);
        command.Options.Add(ollamaUrlOption);
        command.Options.Add(embeddingModelOption);
        command.Options.Add(qdrantUrlOption);
        command.Options.Add(timeoutOption);
        command.Options.Add(collectionNameOption);
        command.Options.Add(deletedExistingOption);

        command.SetAction((Func<ParseResult, Task>)(async result =>
        {
            try
            {
                var input = result.GetValue(inputOption);
                var ollamaUrl = result.GetValue(ollamaUrlOption);
                var embeddingModel = result.GetValue(embeddingModelOption);
                var qdrantUrl = result.GetValue(qdrantUrlOption);
                var timeout = result.GetValue(timeoutOption);
                var collectionName = result.GetValue(collectionNameOption);

                using var istrm = File.OpenRead(input);
                var document = await JsonSerializer.DeserializeAsync<DocumentRagChunks>(istrm).ConfigureAwait(false);

                var ollama = new OllamaClient(new Uri(ollamaUrl), new TimeSpan(0, timeout, 0));
                var qdrant = new QdrantLocalClient(new Uri(qdrantUrl), new TimeSpan(0, timeout, 0));

                using var rag = new RagService(
                    ollama, 
                    qdrant, 
                    collectionName,
                    embeddingModel);

                int count = 0;

                foreach (var chunk in document.Chunks)
                {
                    await rag.IndexDocumentAsync(
                        chunk.Id,
                        chunk.Header + chunk.Content).ConfigureAwait(false);

                    if ((count++ % 16) == 0)
                    {
                        Console.WriteLine($"Embedded {count}/{document.Chunks.Count} chunks.");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: [{e.GetType().Name}] {e.Message}");
            }
        }));

        return command;
    }

    static Command PromptModel()
    {
        var command = new Command(
            "prompt",
            "Provides prompts in a file to the Ollama model and saves the response."
        );

        var inputOption = new Option<string>("--input", ["-i"])
        {
            Required = false,
            Description = "A JSON file with the prompts to issue and expected answer."
        };

        inputOption.Validators.Add(result =>
        {
            var input = result.GetValueOrDefault<string>();
            if (!File.Exists(input))
            {
                result.AddError("The input file does not exist.");
            }
        });

        var ollamaUrlOption = GetUrlOption(
            "The base URL for the Ollama agent.",
            "http://localhost:11434",
            "--agent", ["-a"]);

        var embeddingModelOption = new Option<string>("--model", ["-m"])
        {
            Required = false,
            Description = "The model to use to embedded the chunks (default: mxbai-embed-large).",
            DefaultValueFactory = (result) => "mxbai-embed-large"
        };

        var queryModelOption = new Option<string>("--query", ["-qm"])
        {
            Required = false,
            Description = "The model to use to answer queries (default: gpt-oss:120b-cloud).",
            DefaultValueFactory = (result) => "gpt-oss:120b-cloud"
        };

        Option<string> qdrantUrlOption = GetUrlOption(
            "The base URL for the Quadrant DB.",
            "http://localhost:6333",
            "--db", ["-d"]);

        var timeoutOption = new Option<int>("--timeout", ["-t"])
        {
            Required = false,
            Description = "The HTTP timeout in seconds (must be long enough to handle resouce intensive AI queries).",
            DefaultValueFactory = (result) => 300
        };

        var collectionNameOption = new Option<string>("--collection", ["-n"])
        {
            Required = false,
            Description = "The name of the vector collection in the Qdrant database.",
            DefaultValueFactory = (result) => "opcua-specifications"
        };

        var deletedExistingOption = new Option<bool>("--delete", ["-d"])
        {
            Arity = ArgumentArity.Zero,
            Required = false,
            DefaultValueFactory = (result) => false
        };

        command.Options.Add(inputOption);
        command.Options.Add(ollamaUrlOption);
        command.Options.Add(embeddingModelOption);
        command.Options.Add(queryModelOption);
        command.Options.Add(qdrantUrlOption);
        command.Options.Add(timeoutOption);
        command.Options.Add(collectionNameOption);
        command.Options.Add(deletedExistingOption);

        command.SetAction((Func<ParseResult, Task>)(async result =>
        {
            try
            {
                var input = result.GetValue(inputOption);
                var ollamaUrl = result.GetValue(ollamaUrlOption);
                var embeddingModel = result.GetValue(embeddingModelOption);
                var queryModel = result.GetValue(queryModelOption);
                var qdrantUrl = result.GetValue(qdrantUrlOption);
                var timeout = result.GetValue(timeoutOption);
                var collectionName = result.GetValue(collectionNameOption);

                using var istrm = File.OpenRead(input);
                var document = await JsonSerializer.DeserializeAsync<DocumentQuerySet>(istrm).ConfigureAwait(false);
                istrm.Close();

                var ollama = new OllamaClient(new Uri(ollamaUrl), new TimeSpan(0, timeout, 0));
                var qdrant = new QdrantLocalClient(new Uri(qdrantUrl), new TimeSpan(0, timeout, 0));

                using var rag = new RagService(
                    ollama, 
                    qdrant, 
                    collectionName,
                    embeddingModel,
                    queryModel);

                foreach (var query in document.Queries)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"{query.Prompt}");
                    
                    query.Actual = await rag.AskAsync(query.Prompt).ConfigureAwait(false);

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"Expected: {query.Actual}");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Actual: {query.Actual}");
                }

                using var ostrm = File.OpenWrite(input);

                await JsonSerializer.SerializeAsync(ostrm, document, new JsonSerializerOptions()
                {
                    WriteIndented = true

                }).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: [{e.GetType().Name}] {e.Message}");
            }
        }));

        return command;
    }

    private static Option<string> GetUrlOption(
        string description,
        string defaultUrl,
        string name,
        params string[] aliases)
    {
        var urlOption = new Option<string>(name, aliases)
        {
            Required = false,
            Description = description,
            DefaultValueFactory = (result) => defaultUrl
        };

        urlOption.CustomParser = result =>
        {
            var input = result.Tokens.FirstOrDefault()?.Value;

            if (String.IsNullOrEmpty(input))
            {
                return defaultUrl;
            }

            if (!Uri.IsWellFormedUriString(input, UriKind.Absolute) || !input.StartsWith("http", StringComparison.Ordinal))
            {
                result.AddError("The endpoint is not a valid URL.");
            }

            return input.TrimEnd('/');
        };

        return urlOption;
    }
}