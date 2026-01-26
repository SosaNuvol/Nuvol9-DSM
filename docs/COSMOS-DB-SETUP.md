# Azure Cosmos DB Setup for DSMEnvelope Analytics

## Quick Start Guide

### Step 1: Create Cosmos DB Account in Azure

#### Option A: Using Azure Portal
1. Go to [Azure Portal](https://portal.azure.com)
2. Click "Create a resource" → "Databases" → "Azure Cosmos DB"
3. Select **"Azure Cosmos DB for NoSQL"**
4. Configure:
   - **Subscription**: Your Azure subscription
   - **Resource Group**: Create new or use existing
   - **Account Name**: `dsm-envelopes-db` (or your choice)
   - **Location**: Choose nearest region
   - **Capacity mode**: 
     - **Serverless** (recommended for dev/test, pay-per-request)
     - **Provisioned throughput** (for production, predictable costs)
5. Click "Review + create" → "Create"

#### Option B: Using Azure CLI
```bash
# Login to Azure
az login

# Create resource group
az group create --name DSMEnvelopeRG --location eastus

# Create Cosmos DB account (Serverless)
az cosmosdb create \
  --name dsm-envelopes-db \
  --resource-group DSMEnvelopeRG \
  --default-consistency-level Session \
  --locations regionName=eastus failoverPriority=0 \
  --capabilities EnableServerless

# Get connection string
az cosmosdb keys list \
  --name dsm-envelopes-db \
  --resource-group DSMEnvelopeRG \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" \
  --output tsv
```

### Step 2: Get Connection String

From Azure Portal:
1. Navigate to your Cosmos DB account
2. Go to "Keys" section (left menu)
3. Copy the **"Primary Connection String"**

Format: `AccountEndpoint=https://your-account.documents.azure.com:443/;AccountKey=your-key==;`

### Step 3: Configure Application

#### appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "CosmosDB": {
    "ConnectionString": "AccountEndpoint=https://dsm-envelopes-db.documents.azure.com:443/;AccountKey=your-key-here==;",
    "DatabaseName": "DSMEnvelopeDB",
    "ContainerName": "Envelopes"
  },
  "AllowedHosts": "*"
}
```

#### appsettings.Development.json (for local development)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "CosmosDB": {
    "ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;",
    "DatabaseName": "DSMEnvelopeDB",
    "ContainerName": "Envelopes"
  }
}
```
> **Note**: The connection string above is for the **Cosmos DB Emulator** (localhost).

### Step 4: Update Program.cs

```csharp
using NVL9.DSM.Core.Persistence;
using NVL9.DSM.Core.Persistence.CosmosDB;
using NVL9.DSM.Core.Analytics;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Cosmos DB for DSMEnvelope persistence
var cosmosConnectionString = builder.Configuration["CosmosDB:ConnectionString"];
var databaseName = builder.Configuration["CosmosDB:DatabaseName"] ?? "DSMEnvelopeDB";
var containerName = builder.Configuration["CosmosDB:ContainerName"] ?? "Envelopes";

if (!string.IsNullOrEmpty(cosmosConnectionString))
{
    // Initialize Cosmos DB repository
    var repository = await CosmosDBEnvelopeRepository.CreateAsync(
        cosmosConnectionString,
        databaseName,
        containerName,
        throughput: 400  // Minimum RU/s (only for provisioned throughput mode)
    );

    // Register as singleton
    builder.Services.AddSingleton<IDSMEnvelopeRepository>(repository);

    // Configure extension methods to use this repository
    DSMEnvelopePersistenceExtensions.ConfigureRepository(repository);

    Console.WriteLine("✅ Cosmos DB configured successfully");
    Console.WriteLine($"   Database: {databaseName}");
    Console.WriteLine($"   Container: {containerName}");
}
else
{
    Console.WriteLine("⚠️  Cosmos DB not configured - envelope persistence disabled");
}

// Register analytics service
builder.Services.AddSingleton<DSMEnvelopeAnalytics>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Step 5: Use Cosmos DB Emulator (Optional for Local Dev)

#### Install Emulator
1. Download from: https://aka.ms/cosmosdb-emulator
2. Install and run
3. Access UI at: https://localhost:8081/_explorer/index.html

#### Docker Alternative
```bash
docker pull mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
docker run -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254 \
  -m 3g --cpus=2.0 \
  --name=cosmos-emulator \
  -e AZURE_COSMOS_EMULATOR_PARTITION_COUNT=10 \
  -e AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=true \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```

### Step 6: Verify Setup

Run your application and call the test endpoint:

```bash
# Create a sample envelope
curl -X GET "https://localhost:7000/api/SampleAnalytics/create-sample"

# The response will include the RootEnvelopID, then analyze it:
curl -X GET "https://localhost:7000/api/SampleAnalytics/analyze/{rootEnvelopID}"
```

## Cost Considerations

### Serverless (Recommended for Dev/Test)
- **No minimum charge**
- Pay only for RUs consumed
- ~$0.25 per million RUs
- ~$0.25/GB per month for storage
- **Best for**: Development, testing, variable workloads

### Provisioned Throughput
- **Minimum**: 400 RU/s = ~$24/month
- **Production**: 1000 RU/s = ~$60/month
- Autoscale available (10% higher cost)
- **Best for**: Production, predictable workloads

### Storage Costs
- ~$0.25/GB per month
- 1 million envelopes ≈ 500 MB (without payloads)
- 1 million envelopes ≈ 2-5 GB (with payloads)

## Container Schema

The repository automatically creates this schema:

**Partition Key**: `/partitionKey` (RootEnvelopID)
**Indexes**:
- All properties (automatic)
- Composite: `startTime` + `executionTimeMs`
- Composite: `methodName` + `startTime`

## Monitoring & Optimization

### View Metrics in Azure Portal
1. Navigate to Cosmos DB account
2. Click "Metrics" → Select:
   - **Total Request Units**: Monitor RU consumption
   - **Total Requests**: Request count
   - **Data Usage**: Storage size

### Optimize Costs
1. **Use selective persistence**: Only persist Controller/Service envelopes
2. **Disable payload storage**: Set `includePayload: false` for high-volume methods
3. **Implement retention**: Delete old envelopes with `DeleteOlderThanAsync()`
4. **Use Serverless**: For dev/test and variable workloads
5. **Index tuning**: Exclude unused properties from indexing

## Troubleshooting

### Connection Issues
```csharp
// Test connection
try
{
    var testEnvelope = DSMEnvelope<string>.InitWithCaller("TestConnection");
    await testEnvelope.SuccessAndPersistAsync("test");
    Console.WriteLine("✅ Connection successful");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Connection failed: {ex.Message}");
}
```

### Common Errors

**"Resource Not Found"**
- Container doesn't exist → Run `CreateAsync()` to auto-create

**"Request rate too large (429)"**
- Increase RU/s allocation or switch to autoscale
- Implement retry logic with exponential backoff

**"Partition key mismatch"**
- Ensure `RootEnvelopID` is consistent across envelopes
- Verify `PartitionKey` property is set correctly

## Security Best Practices

1. **Use Managed Identity** (recommended for Azure hosting):
```csharp
var credential = new DefaultAzureCredential();
var cosmosClient = new CosmosClient(
    "https://your-account.documents.azure.com:443/",
    credential
);
```

2. **Store connection strings in Azure Key Vault**:
```json
"CosmosDB": {
  "ConnectionString": "@Microsoft.KeyVault(SecretUri=https://your-vault.vault.azure.net/secrets/CosmosConnectionString/)"
}
```

3. **Use read-only keys** for analytics dashboards
4. **Enable firewall rules** to restrict access
5. **Rotate keys regularly** (every 90 days)

## Next Steps

1. ✅ Set up Cosmos DB account
2. ✅ Configure connection string
3. ✅ Update Program.cs
4. ✅ Test with sample endpoints
5. 📊 Build analytics dashboards
6. 🔍 Set up monitoring alerts
7. 📈 Optimize based on usage patterns

## Support & Resources

- **Cosmos DB Docs**: https://learn.microsoft.com/azure/cosmos-db/
- **Pricing Calculator**: https://azure.microsoft.com/pricing/calculator/
- **Best Practices**: https://learn.microsoft.com/azure/cosmos-db/nosql/best-practice-dotnet
- **DSM Persistence Guide**: See `PERSISTENCE-GUIDE.md`
