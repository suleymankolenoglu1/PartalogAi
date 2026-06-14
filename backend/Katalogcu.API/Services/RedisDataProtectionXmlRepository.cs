using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using StackExchange.Redis;

namespace Katalogcu.API.Services;

public sealed class RedisDataProtectionXmlRepository : IXmlRepository
{
    private readonly string _connectionString;
    private readonly RedisKey _key;
    private readonly object _connectionLock = new();
    private ConnectionMultiplexer? _connection;

    public RedisDataProtectionXmlRepository(string connectionString, string key)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? throw new ArgumentException("Redis connection string is required.", nameof(connectionString))
            : connectionString;

        _key = string.IsNullOrWhiteSpace(key)
            ? "partalog:data-protection:keys"
            : key.Trim();
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        var database = GetDatabase();
        var values = database.HashGetAll(_key);
        return values
            .Where(entry => entry.Value.HasValue)
            .Select(entry => XElement.Parse(entry.Value!))
            .ToArray();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName))
        {
            friendlyName = Guid.NewGuid().ToString("N");
        }

        var database = GetDatabase();
        database.HashSet(_key, friendlyName, element.ToString(SaveOptions.DisableFormatting));
    }

    private IDatabase GetDatabase()
    {
        lock (_connectionLock)
        {
            _connection ??= ConnectionMultiplexer.Connect(_connectionString);
            return _connection.GetDatabase();
        }
    }
}
