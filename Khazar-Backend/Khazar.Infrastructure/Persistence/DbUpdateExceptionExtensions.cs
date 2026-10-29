using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Khazar.Infrastructure.Persistence;

internal static class DbUpdateExceptionExtensions
{
    // SQL Server error numbers for duplicate keys on a unique index / unique constraint.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    // SQL Server error number for a statement that conflicts with a foreign key (or other) constraint.
    private const int ConstraintViolation = 547;

    /// <summary>True when the save failed because a row with the same key already exists in the named unique index.</summary>
    public static bool IsUniqueIndexViolation(this DbUpdateException exception, string indexName) =>
        exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation } sqlException
        && sqlException.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the save failed because the named constraint, e.g. a foreign key, still references the row.</summary>
    public static bool IsConstraintViolation(this DbUpdateException exception, string constraintName) =>
        exception.InnerException is SqlException { Number: ConstraintViolation } sqlException
        && sqlException.Message.Contains(constraintName, StringComparison.OrdinalIgnoreCase);
}
