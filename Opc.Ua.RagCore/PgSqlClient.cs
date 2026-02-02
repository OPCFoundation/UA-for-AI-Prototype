using Npgsql;
using Pgvector;
using System.Net.NetworkInformation;

namespace Opc.Ua.RagCore
{
    public class PgSqlClient : IVectorDbClient
    {
        private readonly NpgsqlDataSource m_dataSource;
        private bool m_disposed;
        private bool m_created;
        private int m_documentId;

        public PgSqlClient(string connectionString)
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            m_dataSource = dataSourceBuilder.Build();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_disposed)
            {
                if (disposing)
                {
                    m_dataSource?.Dispose();
                }

                m_disposed = true;
            }
        }

        const string createDocumentsTableSql = @"
            CREATE TABLE IF NOT EXISTS documents (
                id SERIAL PRIMARY KEY,
                title TEXT NOT NULL UNIQUE
            );";

        const string createTableSql = @"
            CREATE TABLE IF NOT EXISTS {0} (
                id SERIAL PRIMARY KEY,
                guid UUID UNIQUE DEFAULT gen_random_uuid(),
                document_id INTEGER REFERENCES documents(id) ON DELETE CASCADE,
                content TEXT,
                embedding vector({1}),
                metadata JSONB
            );";

        const string createIndexSql = @"
            CREATE INDEX IF NOT EXISTS idx_{0}_embedding
            ON {0}
            USING hnsw (embedding vector_cosine_ops);
            ";

        public async Task EnsureCollectionAsync(string collectionName, int vectorSize)
        {
            if (!m_created)
            {
                await using var cmd1 = m_dataSource.CreateCommand(createDocumentsTableSql);
                await cmd1.ExecuteNonQueryAsync();

                await using var cmd2 = m_dataSource.CreateCommand(String.Format(createTableSql, collectionName, vectorSize));
                await cmd2.ExecuteNonQueryAsync();

                await using var cmd3 = m_dataSource.CreateCommand(String.Format(createIndexSql, collectionName));
                await cmd3.ExecuteNonQueryAsync();

                m_created = true;
            }
        }

        const string deleteTableDataSql = @"
            TRUNCATE TABLE {0};
            ";

        public async Task DeleteCollectionAsync(string collectionName)
        {
            await using var cmd = m_dataSource.CreateCommand(String.Format(deleteTableDataSql, collectionName));
            await cmd.ExecuteNonQueryAsync();
        }

        const string beginLoadSql = @"
            -- 1. Ensure the document exists and get its ID
            WITH doc AS (
                INSERT INTO documents (title)
                VALUES ($1)
                ON CONFLICT (title) DO UPDATE SET title = EXCLUDED.title
                RETURNING id
            ),
            -- 2. Delete existing embeddings associated with that ID
            deleted AS (
                DELETE FROM {0}
                WHERE document_id = (SELECT id FROM doc)
            )
            -- 3. Return the ID to the application
            SELECT id FROM doc;
        ";

        public async Task BeginLoadDocumentAsync(string collectionName, string documentId)
        {
            await using var cmd1 = m_dataSource.CreateCommand(createDocumentsTableSql);
            await cmd1.ExecuteNonQueryAsync();

            await using var cmd2 = m_dataSource.CreateCommand(String.Format(beginLoadSql, collectionName));
            cmd2.Parameters.AddWithValue(documentId);

            m_documentId = await cmd2.ExecuteScalarAsync() as int? ?? 0;
        }

        public async Task EndLoadDocumentAsync(string collectionName, string documentId)
        {
        }

        const string upsertSql = @"
            INSERT INTO {0} (guid, document_id, content, embedding)
            VALUES ($1, $2, $3, $4)
            ON CONFLICT (guid)
            DO UPDATE SET
                document_id = EXCLUDED.document_id,
                content = EXCLUDED.content,
                embedding = EXCLUDED.embedding;";

        public async Task UpsertAsync(string collection, string documentId, string chunkId, float[] vector, string content)
        {
            await using var cmd = m_dataSource.CreateCommand(String.Format(upsertSql, collection));

            cmd.Parameters.AddWithValue(Guid.Parse(chunkId));
            cmd.Parameters.AddWithValue(m_documentId);
            cmd.Parameters.AddWithValue(content);
            var param = cmd.Parameters.AddWithValue(new Vector(vector));
            param.DataTypeName = "vector";

            await cmd.ExecuteNonQueryAsync();
        }

        const string searchSql = @"
            SELECT content
            FROM {0}
            ORDER BY embedding <=> $1 LIMIT $2";

        public async Task<List<string>> SearchAsync(string collection, float[] vector, int topK = 5)
        {
            await using var cmd = m_dataSource.CreateCommand(String.Format(searchSql, collection));

            var param = cmd.Parameters.AddWithValue(new Vector(vector));
            param.DataTypeName = "vector";
            cmd.Parameters.AddWithValue(topK);

            var results = new List<string>();

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(reader.GetString(0));
            }

            return results;
        }
    }
}
