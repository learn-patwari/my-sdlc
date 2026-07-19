namespace SprintForge.Domain.Common;

/// <summary>Base class for all domain-level exceptions (expected business rule violations).</summary>
public class DomainException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>Thrown when an audit record cannot be written or flushed before an operation proceeds.</summary>
public sealed class AuditUnavailableException(string message, Exception? inner = null)
    : DomainException(message, inner);

/// <summary>Thrown when a required secret handle cannot be resolved from the secret store.</summary>
public sealed class SecretNotFoundException(string handle)
    : DomainException($"Secret '{handle}' not found. Re-enter credentials.");

/// <summary>Thrown when an operation is attempted without a prior approval decision.</summary>
public sealed class ApprovalRequiredException(string operationDescription)
    : DomainException($"Operation requires approval: {operationDescription}");
