using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CsToBeamNG.Converter.Discovery;

/// <summary>
/// A small, tolerant parser for Valve's KeyValues text format ("VDF"), as used by Steam's
/// <c>libraryfolders.vdf</c> and <c>appmanifest_*.acf</c>. See docs/research/game-locations.md.
/// </summary>
/// <remarks>
/// It accepts quoted and unquoted tokens, nested sections, <c>//</c> comments, any whitespace and
/// line endings, a byte order mark, and the escapes <c>\\</c>, <c>\"</c>, <c>\n</c> and <c>\t</c>.
/// Other backslashes are kept as they are. Conditions such as <c>[$WIN32]</c> are skipped.
/// Malformed input throws <see cref="FormatException"/> with the line number.
/// </remarks>
internal static class KeyValuesParser
{
    // Real Steam files nest three or four levels deep; anything deeper is not a Steam file.
    private const int MaxDepth = 64;

    /// <summary>Parses a document.</summary>
    /// <returns>A node with an empty key whose children are the document's top-level entries.</returns>
    public static KeyValuesNode Parse(string text)
    {
        var reader = new Reader(text);
        return new KeyValuesNode(string.Empty, reader.ReadEntries(depth: 0));
    }

    private static FormatException Error(int line, string message) =>
        new(string.Create(CultureInfo.InvariantCulture, $"Invalid KeyValues (VDF) text, line {line}: {message}."));

    private enum TokenKind
    {
        Text,
        OpenBrace,
        CloseBrace,
        Condition,
        End,
    }

    private readonly record struct Token(TokenKind Kind, string Text, int Line);

    private sealed class Reader
    {
        private readonly string _text;
        private int _position;
        private int _line = 1;

        public Reader(string text) => _text = text;

        public List<KeyValuesNode> ReadEntries(int depth)
        {
            if (depth > MaxDepth)
            {
                throw Error(_line, "sections are nested too deeply");
            }

            var entries = new List<KeyValuesNode>();
            while (true)
            {
                Token key = Next();
                switch (key.Kind)
                {
                    case TokenKind.End when depth == 0:
                        return entries;
                    case TokenKind.End:
                        throw Error(key.Line, "a section is not closed (missing '}')");
                    case TokenKind.CloseBrace when depth == 0:
                        throw Error(key.Line, "unexpected '}'");
                    case TokenKind.CloseBrace:
                        return entries;
                    case TokenKind.Condition:
                        // A condition after a value, as in: "key" "value" [$WIN32]
                        continue;
                    case TokenKind.OpenBrace:
                        throw Error(key.Line, "a section has no key");
                }

                Token value = Next();
                while (value.Kind == TokenKind.Condition)
                {
                    value = Next();
                }

                if (value.Kind == TokenKind.Text)
                {
                    entries.Add(new KeyValuesNode(key.Text, value.Text));
                }
                else if (value.Kind == TokenKind.OpenBrace)
                {
                    entries.Add(new KeyValuesNode(key.Text, ReadEntries(depth + 1)));
                }
                else
                {
                    throw Error(value.Line, $"the key \"{key.Text}\" has no value");
                }
            }
        }

        private Token Next()
        {
            SkipWhitespaceAndComments();
            int line = _line;
            if (_position >= _text.Length)
            {
                return new Token(TokenKind.End, string.Empty, line);
            }

            switch (_text[_position])
            {
                case '{':
                    _position++;
                    return new Token(TokenKind.OpenBrace, "{", line);
                case '}':
                    _position++;
                    return new Token(TokenKind.CloseBrace, "}", line);
                case '"':
                    return new Token(TokenKind.Text, ReadQuoted(), line);
                case '[':
                    SkipCondition();
                    return new Token(TokenKind.Condition, string.Empty, line);
                default:
                    return new Token(TokenKind.Text, ReadUnquoted(), line);
            }
        }

        private void SkipWhitespaceAndComments()
        {
            while (_position < _text.Length)
            {
                char c = _text[_position];
                if (c == '\n')
                {
                    _line++;
                    _position++;
                }
                else if (char.IsWhiteSpace(c) || c == '﻿')
                {
                    _position++;
                }
                else if (c == '/' && _position + 1 < _text.Length && _text[_position + 1] == '/')
                {
                    while (_position < _text.Length && _text[_position] != '\n')
                    {
                        _position++;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        private string ReadQuoted()
        {
            int startLine = _line;
            _position++;
            var builder = new StringBuilder();
            while (_position < _text.Length)
            {
                char c = _text[_position++];
                if (c == '"')
                {
                    return builder.ToString();
                }

                if (c == '\n')
                {
                    _line++;
                }
                else if (c == '\\' && _position < _text.Length)
                {
                    char? unescaped = _text[_position] switch
                    {
                        '\\' => '\\',
                        '"' => '"',
                        'n' => '\n',
                        't' => '\t',
                        _ => null,
                    };

                    // An unknown escape keeps its backslash, so unescaped Windows paths survive.
                    if (unescaped is char known)
                    {
                        builder.Append(known);
                        _position++;
                        continue;
                    }
                }

                builder.Append(c);
            }

            throw Error(startLine, "a quoted string is not closed");
        }

        private string ReadUnquoted()
        {
            int start = _position;
            while (_position < _text.Length)
            {
                char c = _text[_position];
                if (char.IsWhiteSpace(c) || c is '"' or '{' or '}' or '[')
                {
                    break;
                }

                _position++;
            }

            return _text[start.._position];
        }

        private void SkipCondition()
        {
            int startLine = _line;
            while (_position < _text.Length)
            {
                char c = _text[_position++];
                if (c == ']')
                {
                    return;
                }

                if (c == '\n')
                {
                    break;
                }
            }

            throw Error(startLine, "a condition is not closed (missing ']')");
        }
    }
}
