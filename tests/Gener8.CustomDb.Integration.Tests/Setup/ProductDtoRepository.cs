using Microsoft.Data.SqlClient;

namespace Gener8.CustomDb.Integration.Tests.Setup;

public partial class ProductRepository
{
    protected override string CreateWhereClauseForId() => "WHERE Id = @Id";

    protected override string GetDeleteQuery() => "DELETE FROM Products";

    protected override object GetIdFromEntity(Product entity)
        => entity.Id;

    protected override string GetSelectQuery() => "SELECT Id, Name, Description, CategoryName FROM Products";

    protected override string GetUpsertQuery()
        => """
        MERGE INTO Products AS target
        USING (SELECT @Id AS Id, @Name AS Name, @Description AS Description, @CategoryName AS CategoryName) AS source
        ON target.Id = source.Id
        WHEN MATCHED THEN 
            UPDATE SET Name = source.Name, Description = source.Description, CategoryName = source.CategoryName
        WHEN NOT MATCHED THEN
            INSERT (Id, Name, Description, CategoryName) 
            VALUES (source.Id, source.Name, source.Description, source.CategoryName);
        """;

    protected override ProductDto ToDto(SqlDataReader reader)
        => new()
        {
            Id = reader.GetGuid(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            CategoryName = reader.IsDBNull(reader.GetOrdinal("CategoryName")) ? null : reader.GetString(reader.GetOrdinal("CategoryName"))
        };

    protected override void AddAllParameters(Product entity, SqlCommand command)
    {
        command.Parameters.AddWithValue("@Id", entity.Id);
        command.Parameters.AddWithValue("@Name", entity.Name);
        command.Parameters.AddWithValue("@Description", (object?)entity.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("@CategoryName", (object?)entity.Category?.Name ?? DBNull.Value);
    }
}
