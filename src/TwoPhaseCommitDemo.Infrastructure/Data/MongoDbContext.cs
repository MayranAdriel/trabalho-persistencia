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

    static MongoDbContext()
    {
        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }
        catch (BsonSerializationException)
        {
            // Already registered
        }

        try
        {
            BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
        }
        catch (BsonSerializationException)
        {
            // Already registered
        }
    }

    public MongoDbContext(IMongoClient client, string databaseName)
    {
        _client = client;
        _database = _client.GetDatabase(databaseName);
        EnsureIndexes();
    }

    public IMongoClient Client => _client;
    public IMongoDatabase Database => _database;
    public IMongoCollection<Product> Products => _database.GetCollection<Product>("products");

    private void EnsureIndexes()
    {
        try
        {
            var indexKeysDefinition = Builders<Product>.IndexKeys.Ascending(p => p.Sku);
            var indexOptions = new CreateIndexOptions { Unique = true };
            var indexModel = new CreateIndexModel<Product>(indexKeysDefinition, indexOptions);
            Products.Indexes.CreateOne(indexModel);
        }
        catch
        {
            // Ignore during initial bootstrapping if server is connecting
        }
    }
}

