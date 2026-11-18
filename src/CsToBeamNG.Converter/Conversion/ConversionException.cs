using System;

namespace CsToBeamNG.Converter.Conversion;

/// <summary>A city can't be converted, for example because its dump has no terrain. The message says why.</summary>
public sealed class ConversionException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ConversionException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong and what to do about it.</param>
    public ConversionException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong and what to do about it.</param>
    /// <param name="innerException">The cause.</param>
    public ConversionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
