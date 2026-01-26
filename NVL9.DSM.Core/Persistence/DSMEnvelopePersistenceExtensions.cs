using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NVL9.DSM.Core.Persistence
{
    /// <summary>
    /// Extension methods for DSMEnvelope to enable automatic persistence
    /// </summary>
    public static class DSMEnvelopePersistenceExtensions
    {
        private static IDSMEnvelopeRepository? _repository;

        /// <summary>
        /// Configure the repository for automatic persistence
        /// Call this during application startup
        /// </summary>
        public static void ConfigureRepository(IDSMEnvelopeRepository repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Persist this envelope to the configured repository
        /// </summary>
        public static async Task<DSMEnvelopeDocument> PersistAsync<T>(
            this IDSMEnvelope envelope, 
            T? payload = default, 
            bool includePayload = false)
        {
            if (_repository == null)
            {
                throw new InvalidOperationException(
                    "Repository not configured. Call ConfigureRepository() during startup.");
            }

            string? payloadJson = null;
            if (includePayload && payload != null)
            {
                payloadJson = JsonConvert.SerializeObject(payload);
            }

            var document = DSMEnvelopeDocument.FromEnvelope<T>(envelope, payloadJson);
            
            // Calculate depth based on parent chain
            document.Depth = await CalculateDepthAsync(document);

            return await _repository.SaveAsync(document);
        }

        /// <summary>
        /// Persist this envelope and automatically call Success()
        /// Convenience method for successful operations
        /// </summary>
        public static async Task<DSMEnvelopeDocument> SuccessAndPersistAsync<T>(
            this DSMEnvelope<T> envelope, 
            T value, 
            bool includePayload = true,
            bool outputEnvelop = true)
        {
            envelope.Success(value, outputEnvelop);
            return await PersistAsync(envelope, value, includePayload);
        }

        /// <summary>
        /// Capture exception, persist, and return the document
        /// Convenience method for error operations
        /// </summary>
        public static async Task<DSMEnvelopeDocument> CaptureExceptionAndPersistAsync<T>(
            this DSMEnvelope<T> envelope, 
            Exception exception)
        {
            envelope.CaptureException(exception);
            return await PersistAsync<T>(envelope);
        }

        /// <summary>
        /// Calculate the depth of this envelope in the call chain
        /// </summary>
        private static async Task<int> CalculateDepthAsync(DSMEnvelopeDocument document)
        {
            if (_repository == null || string.IsNullOrEmpty(document.ParentEnvelopID))
                return 0;

            try
            {
                var parent = await _repository.GetByIdAsync(document.ParentEnvelopID, document.RootEnvelopID);
                if (parent != null)
                {
                    return parent.Depth + 1;
                }
            }
            catch
            {
                // If parent lookup fails, default to 0
            }

            return 0;
        }
    }
}
