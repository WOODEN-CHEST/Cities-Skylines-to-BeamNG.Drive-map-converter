using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CsToBeamNG.Dump.Json;

/// <summary>
/// Writes indented JSON text. The exporter uses it because System.Text.Json doesn't exist on .NET 3.5.
/// </summary>
/// <remarks>
/// <para>
/// The output follows the dump's JSON conventions (docs/dump-format.md, JSON files): two-space indentation,
/// <c>\n</c> line breaks and a final newline; numbers in the invariant culture, floating-point numbers in
/// the shortest form that parses back to the same value. NaN and infinities throw, and the writer stays
/// usable. Strings are escaped as JSON requires; unpaired UTF-16 surrogates become U+FFFD.
/// </para>
/// <para>
/// The writer checks the structure: one root value, property names only in objects and before each of
/// their values, ends that match their starts. Misuse throws <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// The <see cref="TextWriter"/> must encode UTF-8 without a byte order mark, as the one from
/// <see cref="CreateFile"/> does. Not thread-safe.
/// </para>
/// </remarks>
public sealed class JsonWriter : IDisposable
{
    private const string Indentation = "  ";
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;
    private const char ReplacementCharacter = (char)0xFFFD;

    private readonly TextWriter _writer;
    private readonly bool _leaveOpen;
    private readonly List<Scope> _scopes = [];
    private bool _rootStarted;
    private bool _disposed;

    /// <summary>Creates a writer that disposes <paramref name="writer"/> when it is disposed.</summary>
    /// <param name="writer">Where the text goes.</param>
    public JsonWriter(TextWriter writer)
        : this(writer, leaveOpen: false)
    {
    }

    /// <summary>Creates a writer.</summary>
    /// <param name="writer">Where the text goes.</param>
    /// <param name="leaveOpen">Whether to leave <paramref name="writer"/> open when this writer is disposed.</param>
    public JsonWriter(TextWriter writer, bool leaveOpen)
    {
        Guard.NotNull(writer, nameof(writer));
        _writer = writer;
        _leaveOpen = leaveOpen;
    }

    /// <summary>The number of open objects and arrays.</summary>
    public int Depth => _scopes.Count;

    /// <summary>Whether the document is complete: its root value has been written and everything is closed.</summary>
    public bool IsComplete => _rootStarted && _scopes.Count == 0;

    internal static Encoding Utf8 { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    internal bool IsDisposed => _disposed;

    private Scope? Current => _scopes.Count == 0 ? null : _scopes[_scopes.Count - 1];

    /// <summary>Creates a new UTF-8 file without a byte order mark and returns a writer for it.</summary>
    /// <param name="path">The file to create. It must not exist.</param>
    /// <exception cref="IOException">The file exists or can't be created.</exception>
    public static JsonWriter CreateFile(string path)
    {
        Guard.NotNull(path, nameof(path));
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        return new JsonWriter(new StreamWriter(stream, Utf8));
    }

    /// <summary>Starts an object.</summary>
    public void WriteStartObject()
    {
        BeforeValue();
        _writer.Write('{');
        _scopes.Add(new Scope(isObject: true));
    }

    /// <summary>Writes a property name and starts an object as its value.</summary>
    /// <param name="propertyName">The property name.</param>
    public void WriteStartObject(string propertyName)
    {
        WritePropertyName(propertyName);
        WriteStartObject();
    }

    /// <summary>Ends the current object.</summary>
    public void WriteEndObject() => EndScope(isObject: true);

    /// <summary>Starts an array.</summary>
    public void WriteStartArray()
    {
        BeforeValue();
        _writer.Write('[');
        _scopes.Add(new Scope(isObject: false));
    }

    /// <summary>Writes a property name and starts an array as its value.</summary>
    /// <param name="propertyName">The property name.</param>
    public void WriteStartArray(string propertyName)
    {
        WritePropertyName(propertyName);
        WriteStartArray();
    }

    /// <summary>Ends the current array.</summary>
    public void WriteEndArray() => EndScope(isObject: false);

    /// <summary>Writes a property name. A value must follow.</summary>
    /// <param name="propertyName">The property name.</param>
    public void WritePropertyName(string propertyName)
    {
        Guard.NotNull(propertyName, nameof(propertyName));
        Guard.NotDisposed(_disposed, this);
        Scope? scope = Current;
        if (scope is null || !scope.IsObject)
        {
            throw new InvalidOperationException("Property names can only be written inside an object.");
        }

        if (scope.HasPendingName)
        {
            throw new InvalidOperationException("The previous property name has no value yet.");
        }

        StartItem(scope);
        WriteQuoted(propertyName);
        _writer.Write(": ");
        scope.HasPendingName = true;
    }

    /// <summary>Writes a string, or <c>null</c> if <paramref name="value"/> is <see langword="null"/>.</summary>
    /// <param name="value">The string.</param>
    public void WriteStringValue(string? value)
    {
        if (value is null)
        {
            WriteNullValue();
            return;
        }

        BeforeValue();
        WriteQuoted(value);
        AfterValue();
    }

    /// <summary>Writes an integer.</summary>
    /// <param name="value">The number.</param>
    public void WriteNumberValue(int value) => WriteRaw(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Writes an integer.</summary>
    /// <param name="value">The number.</param>
    public void WriteNumberValue(uint value) => WriteRaw(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Writes an integer.</summary>
    /// <param name="value">The number.</param>
    public void WriteNumberValue(long value) => WriteRaw(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Writes an integer.</summary>
    /// <param name="value">The number.</param>
    public void WriteNumberValue(ulong value) => WriteRaw(value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Writes a 32-bit float in the shortest form that parses back to the same float.</summary>
    /// <param name="value">The number. It must be finite.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN or infinite.</exception>
    public void WriteNumberValue(float value) => WriteRaw(JsonNumberFormat.Format(value));

    /// <summary>Writes a double in the shortest form that parses back to the same double.</summary>
    /// <param name="value">The number. It must be finite.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN or infinite.</exception>
    public void WriteNumberValue(double value) => WriteRaw(JsonNumberFormat.Format(value));

    /// <summary>Writes <c>true</c> or <c>false</c>.</summary>
    /// <param name="value">The value.</param>
    public void WriteBooleanValue(bool value) => WriteRaw(value ? "true" : "false");

    /// <summary>Writes <c>null</c>.</summary>
    public void WriteNullValue() => WriteRaw("null");

    /// <summary>Writes a property with a string value, or <c>null</c> if <paramref name="value"/> is <see langword="null"/>.</summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The string.</param>
    public void WriteString(string propertyName, string? value)
    {
        WritePropertyName(propertyName);
        WriteStringValue(value);
    }

    /// <summary>Writes a property with an integer value.</summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The number.</param>
    public void WriteNumber(string propertyName, int value) =>
        WriteRawProperty(propertyName, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Writes a property with an integer value.</summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The number.</param>
    public void WriteNumber(string propertyName, uint value) =>
        WriteRawProperty(propertyName, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Writes a property with an integer value.</summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The number.</param>
    public void WriteNumber(string propertyName, long value) =>
        WriteRawProperty(propertyName, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Writes a property with an integer value.</summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The number.</param>
    public void WriteNumber(string propertyName, ulong value) =>
        WriteRawProperty(propertyName, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>Writes a property with a 32-bit float value, in the shortest form that parses back to the same float.</summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The number. It must be finite.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN or infinite. Nothing is written.</exception>
    public void WriteNumber(string propertyName, float value) => WriteRawProperty(propertyName, JsonNumberFormat.Format(value));

    /// <summary>Writes a property with a double value, in the shortest form that parses back to the same double.</summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The number. It must be finite.</param>
    /// <exception cref="ArgumentOutOfRangeException">The value is NaN or infinite. Nothing is written.</exception>
    public void WriteNumber(string propertyName, double value) => WriteRawProperty(propertyName, JsonNumberFormat.Format(value));

    /// <summary>Writes a property with the value <c>true</c> or <c>false</c>.</summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The value.</param>
    public void WriteBoolean(string propertyName, bool value) => WriteRawProperty(propertyName, value ? "true" : "false");

    /// <summary>Writes a property with the value <c>null</c>.</summary>
    /// <param name="propertyName">The property name.</param>
    public void WriteNull(string propertyName) => WriteRawProperty(propertyName, "null");

    /// <summary>Flushes the underlying writer.</summary>
    public void Flush()
    {
        Guard.NotDisposed(_disposed, this);
        _writer.Flush();
    }

    /// <summary>Flushes, and disposes the underlying writer unless it was to be left open.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_leaveOpen)
        {
            _writer.Flush();
        }
        else
        {
            _writer.Dispose();
        }
    }

    // The value text is complete before anything is written, so a failed number leaves no dangling property name.
    private void WriteRawProperty(string propertyName, string valueText)
    {
        WritePropertyName(propertyName);
        WriteRaw(valueText);
    }

    private void WriteRaw(string text)
    {
        BeforeValue();
        _writer.Write(text);
        AfterValue();
    }

    private void BeforeValue()
    {
        Guard.NotDisposed(_disposed, this);
        Scope? scope = Current;
        if (scope is null)
        {
            if (_rootStarted)
            {
                throw new InvalidOperationException("A JSON document has one root value, and it has been written.");
            }

            _rootStarted = true;
        }
        else if (scope.IsObject)
        {
            if (!scope.HasPendingName)
            {
                throw new InvalidOperationException("A value in an object needs a property name first.");
            }

            scope.HasPendingName = false;
        }
        else
        {
            StartItem(scope);
        }
    }

    private void AfterValue()
    {
        if (_scopes.Count == 0)
        {
            _writer.Write('\n');
        }
    }

    private void StartItem(Scope scope)
    {
        if (scope.ItemCount > 0)
        {
            _writer.Write(',');
        }

        _writer.Write('\n');
        WriteIndentation(_scopes.Count);
        scope.ItemCount++;
    }

    private void EndScope(bool isObject)
    {
        Guard.NotDisposed(_disposed, this);
        Scope? scope = Current;
        if (scope is null || scope.IsObject != isObject)
        {
            throw new InvalidOperationException(isObject ? "There is no open object to end." : "There is no open array to end.");
        }

        if (scope.HasPendingName)
        {
            throw new InvalidOperationException("The last property name has no value.");
        }

        _scopes.RemoveAt(_scopes.Count - 1);
        if (scope.ItemCount > 0)
        {
            _writer.Write('\n');
            WriteIndentation(_scopes.Count);
        }

        _writer.Write(isObject ? '}' : ']');
        AfterValue();
    }

    private void WriteIndentation(int depth)
    {
        for (int i = 0; i < depth; i++)
        {
            _writer.Write(Indentation);
        }
    }

    private void WriteQuoted(string value)
    {
        _writer.Write('"');
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '"':
                    _writer.Write("\\\"");
                    break;
                case '\\':
                    _writer.Write("\\\\");
                    break;
                case '\n':
                    _writer.Write("\\n");
                    break;
                case '\r':
                    _writer.Write("\\r");
                    break;
                case '\t':
                    _writer.Write("\\t");
                    break;
                case '\b':
                    _writer.Write("\\b");
                    break;
                case '\f':
                    _writer.Write("\\f");
                    break;

                // Valid in JSON, but line terminators in JavaScript.
                case LineSeparator:
                case ParagraphSeparator:
                    WriteUnicodeEscape(c);
                    break;
                default:
                    if (c < ' ')
                    {
                        WriteUnicodeEscape(c);
                    }
                    else if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                    {
                        _writer.Write(c);
                        _writer.Write(value[i + 1]);
                        i++;
                    }
                    else if (char.IsSurrogate(c))
                    {
                        // Half a surrogate pair can't be encoded as UTF-8.
                        _writer.Write(ReplacementCharacter);
                    }
                    else
                    {
                        _writer.Write(c);
                    }

                    break;
            }
        }

        _writer.Write('"');
    }

    private void WriteUnicodeEscape(char c)
    {
        _writer.Write("\\u");
        _writer.Write(((int)c).ToString("X4", CultureInfo.InvariantCulture));
    }

    private sealed class Scope
    {
        public Scope(bool isObject) => IsObject = isObject;

        public bool IsObject { get; }

        public int ItemCount { get; set; }

        public bool HasPendingName { get; set; }
    }
}
