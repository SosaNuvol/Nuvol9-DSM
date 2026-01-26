using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace NVL9.DSM.Core.Persistence.CosmosDB
{
    /// <summary>
    /// Azure Cosmos DB implementation of IDSMEnvelopeRepository.
    /// Uses hierarchical partition keys for efficient querying by RootEnvelopID.
    /// </summary>
    public class CosmosDBEnvelopeRepository : IDSMEnvelopeRepository
    {
        private readonly Container _container;
        private readonly string _databaseName;
        private readonly string _containerName;

        public CosmosDBEnvelopeRepository(CosmosClient cosmosClient, string databaseName, string containerName)
        {
            _databaseName = databaseName;
            _containerName = containerName;
            _container = cosmosClient.GetContainer(databaseName, containerName);
        }

        /// <summary>
        /// Save a single envelope document
        /// </summary>
        public async Task<DSMEnvelopeDocument> SaveAsync(DSMEnvelopeDocument document)
        {
            var response = await _container.CreateItemAsync(
                document,
                new PartitionKey(document.PartitionKey)
            );

            return response.Resource;
        }

        /// <summary>
        /// Batch save multiple envelopes (optimized for bulk inserts)
        /// Uses Cosmos DB bulk operations for better throughput
        /// </summary>
        public async Task<int> SaveBatchAsync(IEnumerable<DSMEnvelopeDocument> documents)
        {
            var tasks = documents.Select(doc => 
                _container.CreateItemAsync(doc, new PartitionKey(doc.PartitionKey))
            );

            var results = await Task.WhenAll(tasks);
            return results.Length;
        }

        /// <summary>
        /// Get a single envelope by its UniqueIEID
        /// </summary>
        public async Task<DSMEnvelopeDocument?> GetByIdAsync(string uniqueIEID, string rootEnvelopID)
        {
            try
            {
                var id = uniqueIEID.Replace("|", "_");
                var response = await _container.ReadItemAsync<DSMEnvelopeDocument>(
                    id,
                    new PartitionKey(rootEnvelopID)
                );

                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <summary>
        /// Get all envelopes for a specific request (by RootEnvelopID)
        /// Efficient query using partition key
        /// </summary>
        public async Task<List<DSMEnvelopeDocument>> GetByRootEnvelopIDAsync(string rootEnvelopID)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.rootEnvelopID = @rootEnvelopID ORDER BY c.startTime ASC"
            ).WithParameter("@rootEnvelopID", rootEnvelopID);

            return await ExecuteQueryAsync(query, rootEnvelopID);
        }

        /// <summary>
        /// Get all child envelopes for a specific parent
        /// </summary>
        public async Task<List<DSMEnvelopeDocument>> GetChildrenAsync(string parentEnvelopID, string rootEnvelopID)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.parentEnvelopID = @parentEnvelopID ORDER BY c.startTime ASC"
            ).WithParameter("@parentEnvelopID", parentEnvelopID);

            return await ExecuteQueryAsync(query, rootEnvelopID);
        }

        /// <summary>
        /// Get the full execution chain from root to a specific envelope
        /// </summary>
        public async Task<List<DSMEnvelopeDocument>> GetExecutionChainAsync(string uniqueIEID, string rootEnvelopID)
        {
            var chain = new List<DSMEnvelopeDocument>();
            var current = await GetByIdAsync(uniqueIEID, rootEnvelopID);

            while (current != null)
            {
                chain.Insert(0, current); // Add to beginning to maintain order

                if (string.IsNullOrEmpty(current.ParentEnvelopID))
                    break;

                current = await GetByIdAsync(current.ParentEnvelopID, rootEnvelopID);
            }

            return chain;
        }

        /// <summary>
        /// Find all errors within a request
        /// </summary>
        public async Task<List<DSMEnvelopeDocument>> GetErrorsAsync(string rootEnvelopID)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.rootEnvelopID = @rootEnvelopID AND c.isError = true ORDER BY c.startTime ASC"
            ).WithParameter("@rootEnvelopID", rootEnvelopID);

            return await ExecuteQueryAsync(query, rootEnvelopID);
        }

        /// <summary>
        /// Get envelopes by method name (across all requests)
        /// Cross-partition query
        /// </summary>
        public async Task<List<DSMEnvelopeDocument>> GetByMethodNameAsync(string methodName, int limit = 100)
        {
            var query = new QueryDefinition(
                $"SELECT TOP @limit * FROM c WHERE c.methodName = @methodName ORDER BY c.startTime DESC"
            )
            .WithParameter("@methodName", methodName)
            .WithParameter("@limit", limit);

            return await ExecuteQueryAsync(query);
        }

        /// <summary>
        /// Get envelopes by class name (across all requests)
        /// Cross-partition query
        /// </summary>
        public async Task<List<DSMEnvelopeDocument>> GetByClassNameAsync(string className, int limit = 100)
        {
            var query = new QueryDefinition(
                $"SELECT TOP @limit * FROM c WHERE c.className = @className ORDER BY c.startTime DESC"
            )
            .WithParameter("@className", className)
            .WithParameter("@limit", limit);

            return await ExecuteQueryAsync(query);
        }

        /// <summary>
        /// Find slow executions (execution time > threshold)
        /// Cross-partition query
        /// </summary>
        public async Task<List<DSMEnvelopeDocument>> GetSlowExecutionsAsync(long thresholdMs, int limit = 100)
        {
            var query = new QueryDefinition(
                $"SELECT TOP @limit * FROM c WHERE c.executionTimeMs > @threshold ORDER BY c.executionTimeMs DESC"
            )
            .WithParameter("@threshold", thresholdMs)
            .WithParameter("@limit", limit);

            return await ExecuteQueryAsync(query);
        }

        /// <summary>
        /// Get envelopes within a time range
        /// Cross-partition query
        /// </summary>
        public async Task<List<DSMEnvelopeDocument>> GetByTimeRangeAsync(
            DateTimeOffset startTime, 
            DateTimeOffset endTime, 
            int limit = 1000)
        {
            var query = new QueryDefinition(
                $"SELECT TOP @limit * FROM c WHERE c.startTime >= @startTime AND c.startTime <= @endTime ORDER BY c.startTime ASC"
            )
            .WithParameter("@startTime", startTime.ToString("o"))
            .WithParameter("@endTime", endTime.ToString("o"))
            .WithParameter("@limit", limit);

            return await ExecuteQueryAsync(query);
        }

        /// <summary>
        /// Get envelopes by HTTP trace ID
        /// Cross-partition query for distributed tracing
        /// </summary>
        public async Task<List<DSMEnvelopeDocument>> GetByApiTraceIdAsync(string apiTraceId)
        {
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.apiTraceId = @apiTraceId ORDER BY c.startTime ASC"
            ).WithParameter("@apiTraceId", apiTraceId);

            return await ExecuteQueryAsync(query);
        }

        /// <summary>
        /// Delete envelopes older than a specific date (for data retention)
        /// </summary>
        public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffDate)
        {
            var query = new QueryDefinition(
                "SELECT c.id, c.partitionKey FROM c WHERE c.createdAt < @cutoffDate"
            ).WithParameter("@cutoffDate", cutoffDate.ToString("o"));

            var items = await ExecuteQueryAsync(query);
            var deleteCount = 0;

            foreach (var item in items)
            {
                try
                {
                    await _container.DeleteItemAsync<DSMEnvelopeDocument>(
                        item.Id,
                        new PartitionKey(item.PartitionKey)
                    );
                    deleteCount++;
                }
                catch (CosmosException)
                {
                    // Continue on error
                }
            }

            return deleteCount;
        }

        /// <summary>
        /// Execute a query with optional partition key for efficient queries
        /// </summary>
        private async Task<List<DSMEnvelopeDocument>> ExecuteQueryAsync(
            QueryDefinition query, 
            string? partitionKey = null)
        {
            var results = new List<DSMEnvelopeDocument>();
            
            var queryRequestOptions = partitionKey != null 
                ? new QueryRequestOptions { PartitionKey = new PartitionKey(partitionKey) }
                : new QueryRequestOptions { MaxItemCount = 100 };

            using var iterator = _container.GetItemQueryIterator<DSMEnvelopeDocument>(
                query,
                requestOptions: queryRequestOptions
            );

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response);
            }

            return results;
        }

        /// <summary>
        /// Initialize the Cosmos DB database and container with optimal configuration
        /// Call this during application startup
        /// </summary>
        public static async Task<CosmosDBEnvelopeRepository> CreateAsync(
            string connectionString,
            string databaseName = "DSMEnvelopeDB",
            string containerName = "Envelopes",
            int throughput = 400)
        {
            var client = new CosmosClient(connectionString);

            // Create database if not exists
            var databaseResponse = await client.CreateDatabaseIfNotExistsAsync(
                databaseName,
                throughput
            );

            // Create container with hierarchical partition key on RootEnvelopID
            var containerProperties = new ContainerProperties
            {
                Id = containerName,
                PartitionKeyPath = "/partitionKey",
                IndexingPolicy = new IndexingPolicy
                {
                    Automatic = true,
                    IndexingMode = IndexingMode.Consistent,
                    IncludedPaths =
                    {
                        new IncludedPath { Path = "/*" }
                    },
                    CompositeIndexes =
                    {
                        // Optimize for time-range queries
                        new System.Collections.ObjectModel.Collection<CompositePath>
                        {
                            new CompositePath { Path = "/startTime", Order = CompositePathSortOrder.Ascending },
                            new CompositePath { Path = "/executionTimeMs", Order = CompositePathSortOrder.Descending }
                        },
                        // Optimize for method analysis
                        new System.Collections.ObjectModel.Collection<CompositePath>
                        {
                            new CompositePath { Path = "/methodName", Order = CompositePathSortOrder.Ascending },
                            new CompositePath { Path = "/startTime", Order = CompositePathSortOrder.Descending }
                        }
                    }
                }
            };

            await databaseResponse.Database.CreateContainerIfNotExistsAsync(containerProperties);

            return new CosmosDBEnvelopeRepository(client, databaseName, containerName);
        }
    }
}
