using SprintForge.Domain.Common;

namespace SprintForge.Application.Audit;

/// <summary>
///   All write operations MUST implement this interface and be executed via
///   AuditedOperationRunner — never called directly.
///   The runner pre-records an audit entry and blocks execution if that fails.
/// </summary>
public interface IAuditedOperation<TResult>
{
    AuditContext BuildAuditContext(string correlationId, string userName, string machineName);
    Task<Result<TResult>> ExecuteAsync(AuditContext ctx, CancellationToken ct = default);
}

/// <summary>Write operation with no meaningful return value.</summary>
public interface IAuditedOperation : IAuditedOperation<bool>;
