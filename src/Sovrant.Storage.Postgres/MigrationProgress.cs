namespace Sovrant.Storage.Postgres;

public record MigrationProgress(string Stage, int Done, int Total, string Message = "");

public record MigrationResult(int SessionsCopied, int EntriesCopied, int CredentialsCopied);
