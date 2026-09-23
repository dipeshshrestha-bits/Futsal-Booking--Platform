using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Backend.Common;

public static class DbErrors
{
    /// <summary>True when PostgreSQL rejected the write because of a unique index (SQL state 23505).</summary>
    public static bool IsUniqueViolation(DbUpdateException exception, out string? constraintName)
    {
        constraintName = null;
        if (exception.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            constraintName = pg.ConstraintName;
            return true;
        }
        return false;
    }
}