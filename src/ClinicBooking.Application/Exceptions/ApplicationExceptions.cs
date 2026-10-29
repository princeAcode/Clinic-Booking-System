namespace ClinicBooking.Application.Exceptions;

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.");

/// <summary>The user is signed in but not allowed to perform this action.</summary>
public sealed class ForbiddenAccessException(string message) : Exception(message);

/// <summary>Sign-in failed or the account could not be created.</summary>
public sealed class AuthenticationFailedException(string message) : Exception(message);
