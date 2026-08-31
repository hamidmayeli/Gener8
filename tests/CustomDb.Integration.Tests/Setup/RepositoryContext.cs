namespace CustomDb.Integration.Tests.Setup;

public class RepositoryContext(string connectionString) : Gener8.IRepositoryContext
{
    public string ConnectionString => connectionString;
}
