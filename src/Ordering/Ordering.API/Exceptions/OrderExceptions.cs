namespace Ordering.API.Exceptions;
public sealed class OrderNotFoundException(string id) : NotFoundException("Order", id);
public sealed class OrderConflictException(string message) : ConflictException(message);
