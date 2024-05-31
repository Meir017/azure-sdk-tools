using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Azure.Sdk.Tools.AI.Helper.KnowledgeBase;

public class DocumentIndexer
{
    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;
    private readonly OpenAIClient _openAi;
    private readonly ILogger<DocumentIndexer>? _logger;
    private readonly string _indexName;
    private readonly string _embeddingsModel;
    private readonly string _repoName;

    public DocumentIndexer(SearchConfig searchConfig, OpenAiConfig openAiConfig, string repoName, ILogger<DocumentIndexer>? logger)
    {
        _indexName = searchConfig.IndexName
            ?? throw new ArgumentNullException("IndexName");
        _repoName = repoName ?? throw new ArgumentNullException(nameof(repoName));
        _logger = logger;

        var endpointUrl = new Uri(searchConfig.Endpoint ?? throw new ArgumentNullException("Endpoint"));

        var searchOption = new SearchClientOptions();
        searchOption.Diagnostics.IsLoggingContentEnabled = true;
        _indexClient = new SearchIndexClient(endpointUrl, new AzureKeyCredential(searchConfig.Key ?? throw new ArgumentNullException("Key")), searchOption);
        _searchClient = _indexClient.GetSearchClient(_indexName);

        var openAiEndpoint = new Uri(openAiConfig?.Endpoint ?? throw new ArgumentNullException("OpenAI Endpoint is null"));
        _openAi = new OpenAIClient(openAiEndpoint, new AzureKeyCredential(openAiConfig.Key ?? throw new ArgumentNullException("OpenAI key is null")));
        _embeddingsModel = openAiConfig?.EmbeddingModel ?? throw new ArgumentNullException("OpenAI EmbeddingsModel is null");
    }

    public async Task CreateIndexIfNotExists()
    {
        // GetIndex(name) throws if there is no index
        await foreach(var indexName in _indexClient.GetIndexNamesAsync())
        {
            if (indexName == _indexName) {
                return;
            }
        }

        await CreateIndex();
    }

    public async Task CreateIndex()
    {
        string vectorSearchConfigName = "my-vector-config";
        string vectorSearchHnswConfig = "my-hsnw-vector-config";
        var index = new SearchIndex(_indexName)
        {
            Fields =
            {
                new SimpleField("Id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                new SearchableField("Content") { IsFilterable = true },
                new VectorSearchField("ContentVector", 1536, vectorSearchConfigName),
                new SearchableField("Source") { IsFilterable = true, IsSortable = true, IsFacetable = true},
                new SearchableField("Title") { IsFilterable = true, IsSortable = true, IsFacetable = true},
                new SimpleField("Timestamp", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true }
            },
                VectorSearch = new()
                {
                    Profiles =
                {
                    new VectorSearchProfile(vectorSearchConfigName, vectorSearchHnswConfig)
                },
                    Algorithms =
                {
                    new HnswAlgorithmConfiguration(vectorSearchHnswConfig)
                }
                },
                ScoringProfiles =
                {
                    // newlyAddedBoost scoring profile is added to boost the score of newer documents
                    new ScoringProfile("newlyAddedBoost")
                    {
                        TextWeights = new TextWeights(new Dictionary<string, double>
                        {
                            { "Timestamp", 1.5 }
                        }),
                        Functions =
                        {
                            new FreshnessScoringFunction("Timestamp", 1, new FreshnessScoringParameters(TimeSpan.FromDays(30)))
                            {
                                Interpolation = ScoringFunctionInterpolation.Linear
                            }
                        }
                    }
                }
        };

        await _indexClient.CreateIndexAsync(index);
    }


    public async Task DeleteIndex()
    {
        await _indexClient.DeleteIndexAsync(_indexName);
    }

    public async Task Index(IEnumerable<Issue> issues)
    {
        var documents = issues
            .Select(async i => await DocumentsFromIssue(i))
            .Select(t => t.Result)
            .SelectMany(d => d)
            .Where(d => d != null)
            .Cast<Document>();

        using SearchIndexingBufferedSender<Document> sender = new(_searchClient);
        await sender.MergeOrUploadDocumentsAsync(documents);
    }

    public async Task Index(IEnumerable<string> mdFiles, string azSdkRepoPath)
    {
        // TODO: fix path, currently peopr links are generated only if
        // we pass path/to/azure-sdk-for-*/
        // but we don't care about ./eng, so we want to index ./sdk only
        // but generated links are not right - https://github.com/Azure/azure-sdk-for-net/tree/main/monitor\Azure.Monitor.OpenTelemetry.Exporter\README.md
        var documents = mdFiles
            .Select(async f => await FromMdFile(f, Path.GetRelativePath(azSdkRepoPath, f)))
            .Select(t => t.Result)
            .SelectMany(d => d)
            .Where(d => d != null)
            .Cast<Document>();

        using SearchIndexingBufferedSender<Document> sender = new(_searchClient);
        await sender.MergeOrUploadDocumentsAsync(documents);
    }

    private async Task<IReadOnlyList<float>> Vectorize(string text)
    {
        text = text.Replace('\n', ' ').Replace('\r', ' ');

        var embeddings = await _openAi.GetEmbeddingsAsync(_embeddingsModel, new EmbeddingsOptions(new[] { text }));

        return embeddings.Value.Data[0].Embedding;
    }

    private async Task<IEnumerable<Document?>> DocumentsFromIssue(Issue issue)
    {
        var content = Document.GetFullContent(issue);

        var docs = new List<Document>();
        var chunks = Chunk(content);

        for (var i = 0; i < chunks.Count(); i++)
        {
            docs.Add(new Document()
            {
                Id = string.Concat(_repoName, "_", issue.IssueNumber + "_" + i),
                Source = string.Concat(Document.RepoUrl, _repoName, "/issues/", issue.IssueNumber),
                Title = issue.Title,
                Content = chunks[i],
                ContentVector = await Vectorize(chunks[i]),
                // should this be the timestamp of the last comment, if not closed?
                Timestamp = issue.ClosedAt ?? DateTime.UtcNow
            });
        }

        return docs;
    }

    public async Task<IEnumerable<Document?>> FromMdFile(string absolutePath, string relativePath)
    {
        var text = File.ReadAllText(absolutePath);
        var firstLineEnd = text.Trim().IndexOfAny(Document.EndOfLine);

        if (firstLineEnd <= 0)
        {
            return Enumerable.Empty<Document?>();
        }

        var firstLine = text[..firstLineEnd];

        var docs = new List<Document>();
        var chunks = Chunk(text);
        for (var i = 0; i < chunks.Count(); i ++)
        {
            docs.Add(new Document()
            {
                Id = string.Concat(_repoName, "_", Document.GetId(relativePath) + "_" + i),
                Source = string.Concat(Document.RepoUrl, _repoName, "/tree/main/", relativePath),
                Title = firstLine,
                Content = chunks[i],
                ContentVector = await Vectorize(chunks[i]),
                Timestamp = File.GetLastWriteTime(absolutePath)
            });
        }

        return docs;
    }


    private List<string> Chunk(string text)
    {
        var chunks = new List<string>();
        for (int i = 0; i < text.Length; i += 8000)
        {
            if (i + 8000 >= text.Length)
            {
                chunks.Add(text.Substring(i));
            }
            else
            {
                chunks.Add(text.Substring(i, 8000));
            }
        }
        return chunks;
    }

    public class Document
    {
        internal static readonly string RepoUrl = $"https://github.com/Azure/";
        internal static readonly char[] EndOfLine = new[] { '\n', '\r' };

        internal static string GetId(string url)
        {
            // todo optimize
            var sb = new StringBuilder(url.Length);
            for (int i = 0; i < url.Length; i ++)
            {
                if ((url[i] >= 'a' && url[i] <= 'z')
                    || (url[i] >= 'A' && url[i] <= 'Z')
                    || (url[i] >= '0' && url[i] <= '9')
                    || url[i] == '-')
                {
                    sb.Append(url[i]);
                }
                else
                {
                    sb.Append('_');
                }
            }

            return sb.ToString();
        }
        public string? Id { get; set; }

        public string? Source { get; set; }
        public string? Title { get; set; }

        public string? Content { get; set; }

        public IReadOnlyList<float>? ContentVector { get; set; }
        public DateTimeOffset? Timestamp { get; set; }

        internal static string GetFullContent(Issue issue)
        {
            string description = issue.Description ?? throw new ArgumentException("issue description is null");
            int envStart = description.IndexOf("### Environment");
            if (envStart > 0)
            {
                description = description.Substring(0, envStart);
            }

            var content = new StringBuilder()
                .AppendLine(issue.Title)
                .AppendFormat("Customer: {0} ", description.Trim())
                .Append("\n");

            if (issue.Comments != null)
            {
                foreach (IssueComment comment in issue.Comments)
                {
                    if (comment.AuthorIsInAzureOrg == true)
                    {
                        content.Append("Agent: ");
                    }
                    else
                    {
                        content.Append("Customer: ");
                    }

                    content.Append(comment.Comment?.Trim()).Append("\n");
                }
            }

            return content.ToString();
        }
    }
}
