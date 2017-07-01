﻿//
// EucJpEncoding.cs
//
// Author:
//       Benito Palacios Sánchez <benito356@gmail.com>
//
// Copyright (c) 2017 Benito Palacios Sánchez
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
namespace Libgame.IO.Encodings
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Reflection;

    /// <summary>
    /// EUC-JP encoding.
    /// Implemented standard from: https://encoding.spec.whatwg.org/
    /// </summary>
    public class EucJpEncoding : Encoding
    {
        static Dictionary<int, int> idx2CodePointJs212;
        static Dictionary<int, int> idx2CodePointJs208;
        static Dictionary<int, int> codePoint2IdxJs208;

        static EucJpEncoding()
        {
            Assembly myAssembly = Assembly.GetExecutingAssembly();

            idx2CodePointJs208 = new Dictionary<int, int>();
            codePoint2IdxJs208 = new Dictionary<int, int>();
            FillCodecTable(
                myAssembly.GetManifestResourceStream("Libgame.IO.Encodings.index-jis0208.txt"),
                idx2CodePointJs208,
                codePoint2IdxJs208);
            
            idx2CodePointJs212 = new Dictionary<int, int>();
            FillCodecTable(
                myAssembly.GetManifestResourceStream("Libgame.IO.Encodings.index-jis0212.txt"),
                idx2CodePointJs212);
        }

        public EucJpEncoding()
        {
            //DecoderFallback = new DecoderExceptionFallback();
            //EncoderFallback = new EncoderExceptionFallback();
        }

        public override int GetByteCount(char[] chars, int index, int count)
        {
            int length = 0;
            string text = new string(chars, index, count);
            EncodeText(text, (str, b) => length++);
            return length;
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            using (MemoryStream stream = new MemoryStream(bytes, byteIndex, bytes.Length)) {
                string text = new string(chars, charIndex, charCount);
                EncodeText(text, (str, b) => stream.WriteByte(b));
                return (int)stream.Length;
            }
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            int chars = 0;
            using (MemoryStream stream = new MemoryStream(bytes, index, count))
                DecodeText(stream, (str, ch) => chars += ch.Length);
            return chars;
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            StringBuilder text = new StringBuilder();
            using (MemoryStream stream = new MemoryStream(bytes, byteIndex, byteCount))
                DecodeText(stream, (str, ch) => text.Append(ch));

            text.CopyTo(0, chars, charIndex, text.Length);
            return text.Length;
        }

        public override int GetMaxByteCount(int charCount)
        {
            return charCount * 3;
        }

        public override int GetMaxCharCount(int byteCount)
        {
            return byteCount;
        }

        static void FillCodecTable(Stream file, Dictionary<int, int> idx2CodePoint, Dictionary<int, int> codePoint2Idx = null)
        {
            StreamReader reader = new StreamReader(file);
            while (!reader.EndOfStream) {
                string line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (line[0] == '#')
                    continue;

                string[] fields = line.Split('\t');
                int index = System.Convert.ToInt32(fields[0].TrimStart(' '));
                int codePoint = System.Convert.ToInt32(fields[1].Substring(2), 16);

                idx2CodePoint[index] = codePoint;
                if (codePoint2Idx != null)
                    codePoint2Idx[codePoint] = index;
            }
        }

        protected void EncodeText(string text, Action<Stream, byte> onByte)
        {
            MemoryStream stream = new MemoryStream(UTF32.GetBytes(text));

            // 1
            while (stream.Position < stream.Length) {
                byte[] buffer = new byte[4];
                stream.Read(buffer, 0, 4);
                int codePoint = BitConverter.ToInt32(buffer, 0);

                if (codePoint <= 0x7F) {
                    // 2
                    onByte(stream, (byte)(codePoint & 0xFF));
                } else if (codePoint == 0xA5) {
                    // 3
                    onByte(stream, 0x5C);
                } else if (codePoint == 0x203E) {
                    // 4
                    onByte(stream, 0x7E);
                } else if (IsInRange(codePoint, 0xFF61, 0xFF9F)) {
                    // 5
                    onByte(stream, 0x8E);
                    onByte(stream, (byte)(codePoint - 0xFF61 + 0xA1));
                } else {
                    // 6
                    if (codePoint == 0x2212)
                        codePoint = 0xFF0D;

                    // 8
                    if (!codePoint2IdxJs208.ContainsKey(codePoint)) {
                        EncoderFallbackBuffer fallback = EncoderFallback.CreateFallbackBuffer();
                        string ch = char.ConvertFromUtf32(codePoint);
                        if (ch.Length == 1)
                            fallback.Fallback(ch[0], 0);
                        else
                            fallback.Fallback(ch[0], ch[1], 0);

                        while (fallback.Remaining > 0)
                            onByte(stream, (byte)fallback.GetNextChar());
                    }

                    // 7
                    int pointer = codePoint2IdxJs208[codePoint];
                    onByte(stream, (byte)(pointer / 94 + 0xA1)); // 9, 11
                    onByte(stream, (byte)(pointer % 94 + 0xA1)); // 10, 11
                }
            }
        }

        protected void DecodeText(Stream stream, Action<Stream, string> onText)
        {
            DecoderFallbackBuffer fallback = DecoderFallback.CreateFallbackBuffer();

            byte lead = 0;
            bool jis0212 = false;
            while (stream.Position < stream.Length) {
                byte current = (byte)stream.ReadByte();
                if (lead == 0x8E && IsInRange(current, 0xA1, 0xDF)) {
                    // 3
                    lead = 0;
                    onText(stream, char.ConvertFromUtf32(0xFF61 - 0xA1 + current));
                } else if (lead == 0x8F && IsInRange(current, 0xA1, 0xFE)) {
                    // 4
                    jis0212 = true;
                    lead = current;
                } else if (lead != 0x00) {
                    // 5
                    if (IsInRange(lead, 0xA1, 0xFE) && IsInRange(current, 0xA1, 0xFE)) {
                        int tblIdx = (lead - 0xA1) * 94 + current - 0xA1;
                        int codePoint = jis0212 ? idx2CodePointJs212[tblIdx] : idx2CodePointJs208[tblIdx];
                        onText(stream, char.ConvertFromUtf32(codePoint));

                        lead = 0x00;
                        jis0212 = false;
                    } else {
                        bool result = fallback.Fallback(new byte[] { lead, current }, 0);
                        while (result && fallback.Remaining > 0)
                            onText(stream, fallback.GetNextChar().ToString());
                    }
                } else if (current <= 0x7F) {
                    // 6
                    onText(stream, char.ConvertFromUtf32(current));
                } else if (IsInRange(current, 0x8E, 0x8F) || IsInRange(current, 0xA1, 0xFE)) {
                    // 7
                    lead = current;
                } else {
                    // 8
                    bool result = fallback.Fallback(new byte[] { current }, 0);
                    while (result && fallback.Remaining > 0)
                        onText(stream, fallback.GetNextChar().ToString());
                }
            }

            // 1
            if (lead != 0x00) {
                bool result = fallback.Fallback(new byte[] { lead }, 0);
                while (result && fallback.Remaining > 0)
                    onText(stream, fallback.GetNextChar().ToString());
            }
        }

        static bool IsInRange(int val, int min, int max)
        {
            return val >= min && val <= max;
        }
    }
}