using CustomDb.Integration.Tests.Setup;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CustomDb.Integration.Tests.Setup.Models;

public partial class ProductRepository
{
    private RepositoryContext TheContext => Context as RepositoryContext
        ?? throw new InvalidOperationException("Context is not a RepositoryContext");

    public override async Task<Product?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(TheContext.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = $"{GetSelectQuery()} {CreateWhereClauseForId()}";
        command.Parameters.AddWithValue("@Id", id);

        var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            return ToModel(ReadDto(reader));

        return null;
    }

    public override async Task SaveAsync(Product entity, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(TheContext.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = GetUpsertQuery();
        AddAllParameters(entity, command);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override Task DeleteAsync(Product entity, CancellationToken cancellationToken = default)
        => DeleteByIdAsync(GetIdFromEntity(entity), cancellationToken);

    public override async Task DeleteByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(TheContext.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = $"{GetDeleteQuery()} {CreateWhereClauseForId()}";
        command.Parameters.AddWithValue("@Id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<Product>();

        using var connection = new SqlConnection(TheContext.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = GetSelectQuery();

        var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            results.Add(ToModel(ReadDto(reader)));

        return results;
    }
}
