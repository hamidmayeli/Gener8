using Microsoft.Data.SqlClient;

namespace Gener8;

public class RepositoryContext(string connectionString) : IRepositoryContext
{
    public string ConnectionString => connectionString;
}

partial class RepositoryBase<TModel, TDto>
{
    private RepositoryContext TheContext => Context as RepositoryContext
        ?? throw new InvalidOperationException("Context is not a RepositoryContext");

    public async Task<TModel?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(TheContext.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();

        command.CommandText = $"{GetSelectQuery()} {CreateWhereClauseForId()}";
        AddIdAsParameter(id, command);

        var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            return ToModel(ToDto(reader));

        return null;
    }

    public async Task SaveAsync(TModel entity, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(TheContext.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();

        command.CommandText = GetUpsertQuery();
        AddAllParameters(entity, command);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task DeleteAsync(TModel entity, CancellationToken cancellationToken = default)
        => DeleteByIdAsync(GetIdFromEntity(entity), cancellationToken);

    public async Task DeleteByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(TheContext.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();

        command.CommandText = $"{GetDeleteQuery()} {CreateWhereClauseForId()}";
        AddIdAsParameter(id, command);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IEnumerable<TModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<TModel>();

        using var connection = new SqlConnection(TheContext.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();

        command.CommandText = GetSelectQuery();

        var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            results.Add(ToModel(ToDto(reader)));

        return results;
    }

    abstract protected string GetSelectQuery();
    abstract protected string GetUpsertQuery();
    abstract protected string GetDeleteQuery();
    abstract protected string CreateWhereClauseForId();
    abstract protected TDto ToDto(SqlDataReader reader);
    abstract protected object GetIdFromEntity(TModel entity);
    protected abstract void AddAllParameters(TModel entity, SqlCommand command);

    private static void AddIdAsParameter(object id, SqlCommand command)
        => command.Parameters.AddWithValue("@Id", id);
}
