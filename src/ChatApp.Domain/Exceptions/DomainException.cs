namespace ChatApp.Domain.Exceptions;

/// <summary>
/// [OOP - Custom Exception Hierarchy]
/// Domain exception chỉ throw khi vi phạm business rules.
/// Tách biệt với infrastructure exception (network, db lỗi).
/// ExceptionMiddleware sẽ catch và map ra HTTP 400.
/// </summary>
public class DomainException(string message) : Exception(message);

/// <summary>
/// Throw khi entity không tìm thấy → HTTP 404
/// </summary>
public class NotFoundException(string name, object key)
    : Exception($"Entity '{name}' with key '{key}' was not found.");

/// <summary>
/// Throw khi vi phạm business validation → HTTP 422
/// </summary>
public class BusinessRuleException(string rule)
    : DomainException($"Business rule violated: {rule}");
