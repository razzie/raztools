/*
Copyright (C) Gábor "Razzie" Görzsöny
Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace raztools
{
    public static class ObjectDeserializer
    {
        public class ParseError : Exception
        {
            public ParseError(int line, Exception inner) :
                base("Parse error at line " + line, inner)
            {
            }
        }

        private enum TokenType
        {
            Empty,
            KeyValue,
            KeyOnly,
            NestingBegin,
            NestingEnd
        }

        private class Token
        {
            public TokenType Type { get; set; }
            public string Key { get; set; }
            public string KeyType { get; set; }
            public string Value { get; set; }
            public bool HasCommaOrSemicolon { get; set; }

            public Token(string line)
            {
                string token = line.Trim();
                int equals = token.IndexOf("=");
                int colon = token.IndexOf(":");
                int semicolon = Math.Max(token.IndexOf(";"), token.IndexOf(",")); // ',' and ';' are considered the same
                int comment = token.IndexOf("#");

                HasCommaOrSemicolon = (semicolon != -1);

                if (comment != -1)
                {
                    token = token.Substring(0, comment);
                }

                if (semicolon != -1 && equals != -1)
                {
                    Type = TokenType.KeyValue;

                    if (colon != -1) // key type is known
                    {
                        Key = token.Substring(0, colon).Trim();
                        KeyType = token.Substring(colon + 1, equals - (colon + 1)).Trim();
                        Value = token.Substring(equals + 1, semicolon - (equals + 1)).Trim();
                    }
                    else // key type is unknown
                    {
                        Key = token.Substring(0, equals).Trim();
                        Value = token.Substring(equals + 1, semicolon - (equals + 1)).Trim();
                    }
                }
                else if (token.Length > 0)
                {
                    if (token.StartsWith("{"))
                    {
                        Type = TokenType.NestingBegin;
                    }
                    else if (token.StartsWith("}"))
                    {
                        Type = TokenType.NestingEnd;
                    }
                    else // key (with optional key type)
                    {
                        Type = TokenType.KeyOnly;

                        if (colon != -1) // key type is known
                        {
                            Key = token.Substring(0, colon).Trim();
                            KeyType = token.Substring(colon + 1).Trim();
                        }
                        else
                        {
                            Key = token;
                        }

                        if (semicolon != -1)
                        {
                            Key = Key.Substring(0, semicolon);
                        }
                    }
                }
            }
        }

        private class StreamWrapper
        {
            private StreamReader _stream;
            private int _line;

            public StreamWrapper(StreamReader stream)
            {
                _stream = stream;
                _line = 0;
            }

            public string ReadLine()
            {
                ++_line;
                return _stream.ReadLine();
            }

            public int LineNum
            {
                get { return _line; }
            }

            public bool EndOfStream
            {
                get { return _stream.EndOfStream; }
            }
        }

        private static object ParseSimple(Type type, string value)
        {
            if (type == typeof(int))
            {
                return int.Parse(value);
            }
            else if (type == typeof(float))
            {
                return float.Parse(value, CultureInfo.InvariantCulture);
            }

            return null;
        }

        private static IEnumerable ParseArray(Token name_token, StreamWrapper stream, Type type)
        {
            var first_token = new Token(stream.ReadLine());
            if (first_token.Type != TokenType.NestingBegin)
                throw new Exception("nested expression must start with {");

            List<object> tmp_elements = new List<object>();

            while (!stream.EndOfStream)
            {
                var token = new Token(stream.ReadLine());

                switch (token.Type)
                {
                    case TokenType.KeyOnly:
                        if (token.HasCommaOrSemicolon)
                            tmp_elements.Add(ParseSimple(type, token.Key));
                        else
                            tmp_elements.Add(ParseNested(token, stream, type));
                        break;

                    case TokenType.KeyValue:
                        throw new Exception("Key-value pairs are not supported in arrays");

                    case TokenType.NestingBegin:
                        throw new Exception("unexpected {");

                    case TokenType.NestingEnd:
                        Array o = Array.CreateInstance(type, tmp_elements.Count);
                        Array.Copy(tmp_elements.ToArray(), o as Array, tmp_elements.Count);
                        return o;
                }
            }

            throw new Exception("unexpected ond of stream");
        }

        private static object ParseNested(Token name_token, StreamWrapper stream, Type type = null)
        {
            if (type == null)
            {
                type = Type.GetType(name_token.KeyType); // namespace is important!
            }

            if (type.IsArray)
            {
                return ParseArray(name_token, stream, type.GetElementType());
            }

            var constructor = type.GetConstructor(Type.EmptyTypes);
            object o = constructor.Invoke(null);

            var first_token = new Token(stream.ReadLine());
            if (first_token.Type != TokenType.NestingBegin)
                throw new Exception("nested expression must start with {");

            while (!stream.EndOfStream)
            {
                System.Reflection.FieldInfo field;
                var token = new Token(stream.ReadLine());

                switch (token.Type)
                {
                    case TokenType.KeyOnly:
                        field = type.GetField(token.Key);
                        field.SetValue(o, ParseNested(token, stream, field.FieldType));
                        break;

                    case TokenType.KeyValue:
                        field = type.GetField(token.Key);
                        field.SetValue(o, ParseSimple(field.FieldType, token.Value));
                        break;

                    case TokenType.NestingBegin:
                        throw new Exception("unexpected {");

                    case TokenType.NestingEnd:
                        return o;
                }
            }

            throw new Exception("unexpected ond of stream");
        }

        public static void ParseStream(StreamReader streamreader, IDictionary<string, object> cache)
        {
            StreamWrapper stream = new StreamWrapper(streamreader);

            try
            {
                while (!stream.EndOfStream)
                {
                    var token = new Token(stream.ReadLine());
                    switch (token.Type)
                    {
                        case TokenType.Empty:
                            break;

                        case TokenType.KeyOnly:
                            cache.Add(token.Key, ParseNested(token, stream));
                            break;

                        case TokenType.KeyValue:
                        case TokenType.NestingBegin:
                        case TokenType.NestingEnd:
                            throw new Exception("invalid item at top level");
                    }
                }
            }
            catch (Exception e)
            {
                throw new ParseError(stream.LineNum, e);
            }
        }

        public static void ParseStream(Stream stream, IDictionary<string, object> cache)
        {
            using (var streamreader = new StreamReader(stream))
            {
                ParseStream(streamreader, cache);
            }
        }
    }
}
