using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using TwoPhaseCommitDemo.Domain.Entities;

namespace TwoPhaseCommitDemo.Infrastructure.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly IMongoClient _client;
    private readonly ILogger<MongoDbContext> _logger;
    private static readonly object SerializerLock = new();
    private static bool _serializersRegistered;

    public MongoDbContext(IMongoClient client, string databaseName, ILogger<MongoDbContext> logger)
    {
        _client = client;
        _logger = logger;
        _database = _client.GetDatabase(databaseName);

        RegisterBsonSerializers();
        EnsureIndexes();
    }

    public IMongoClient Client => _client;
    public IMongoDatabase Database => _database;
    public IMongoCollection<Product> Products => _database.GetCollection<Product>("products");

    private void RegisterBsonSerializers()
    {
        if (_serializersRegistered)
        {
            return;
        }

        lock (SerializerLock)
        {
            if (_serializersRegistered)
            {
                return;
            }

            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
                BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
                _serializersRegistered = true;
            }
            catch (BsonSerializationException ex)
            {
                _logger.LogDebug(ex, "BSON Serializers já registrados anteriormente no runtime.");
                _serializersRegistered = true;
            }
        }
    }

    private void EnsureIndexes()
    {
        try
        {
            var indexKeysDefinition = Builders<Product>.IndexKeys.Ascending(p => p.Sku);
            var indexOptions = new CreateIndexOptions { Unique = true };
            var indexModel = new CreateIndexModel<Product>(indexKeysDefinition, indexOptions);
            Products.Indexes.CreateOne(indexModel);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tentativa de inicializar índices no MongoDB falhou (o servidor pode estar em inicialização ou réplica configurando).");
        }
    }
}
