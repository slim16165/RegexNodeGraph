using System;
using Neo4j.Driver;

namespace RegexNodeGraph.Connection;

public class Neo4jConnection : IDisposable
{
    private readonly IDriver _driver;

    public IDriver Driver => _driver;

    public Neo4jConnection(string uri, string user, string password)
    {
        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
    }

    public void Dispose()
    {
        _driver?.Dispose();
    }
}