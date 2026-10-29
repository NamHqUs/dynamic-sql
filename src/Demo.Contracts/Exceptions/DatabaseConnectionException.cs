namespace Demo.Contracts.Exceptions;

public sealed class DatabaseConnectionException : Exception
{
    public const string UserMessage = "The database connection is unavailable. Connect to a database to continue.";

    public DatabaseConnectionException(Exception? innerException = null)
        : base(UserMessage, innerException)
    {
    }
}
