using System;
using System.Text;
using System.Collections.Generic;
using System.IO;

namespace ZLR.VM
{
    public delegate bool TimedInputCallback();
    public delegate void SoundFinishedCallback();
    public delegate short CharTranslator(char ch);

    /// <summary>
    /// Indicates whether a given character can be printed and/or received as input.
    /// </summary>
    /// <seealso cref="IZMachineIO.CheckUnicode"/>
    [Flags]
    public enum UnicodeCaps
    {
        /// <summary>
        /// Indicates that the character can be printed.
        /// </summary>
        CanPrint = 1,
        /// <summary>
        /// Indicates that the character can be received as input.
        /// </summary>
        CanInput = 2
    }

    public interface IZMachineIO
    {
        // input
        string ReadLine(int time, TimedInputCallback callback, byte[] terminatingKeys, out byte terminator);
        short ReadKey(int time, TimedInputCallback callback, CharTranslator translator);
        bool ReadingCommandsFromFile { get; set; }
        bool WritingCommandsToFile { get; set; }

        // output
        void PutChar(char ch);
        void PutString(string str);
        void PutTextRectangle(string[] lines);
        bool Buffering { get; set; }

        // transcript
        bool Transcripting { get; set; }
        void PutTranscriptChar(char ch);
        void PutTranscriptString(string str);

        // saved game files
        Stream OpenSaveFile(int size);
        Stream OpenRestoreFile();
        Stream OpenAuxiliaryFile(string name, int size, bool writing);

        // visual effects
        void SetTextStyle(short style);
        void SplitWindow(short lines);
        void SelectWindow(short num);
        void EraseWindow(short num); // -1 = whole screen and unsplit, -2 = whole screen but keep split
        void EraseLine();
        void MoveCursor(short x, short y);
        void GetCursorPos(out short x, out short y);
        void SetColors(short fg, short bg);
        short SetFont(short num); // returns the previous font, or 0 if num was unrecognized

        // sound effects
        void PlaySoundSample(ushort number, short effect, byte volume, byte repeats,
            SoundFinishedCallback callback);
        void PlayBeep(bool highPitch);

        // capabilities
        bool ForceFixedPitch { get; set; }

        bool BoldAvailable { get; }
        bool ItalicAvailable { get; }
        bool FixedPitchAvailable { get; }
        bool TimedInputAvailable { get; }
        bool SoundSamplesAvailable { get; }

        byte WidthChars { get; }
        short WidthUnits { get; }
        byte HeightChars { get; }
        short HeightUnits { get; }
        byte FontHeight { get; }
        byte FontWidth { get; }

        event EventHandler SizeChanged;

        bool ColorsAvailable { get; }
        byte DefaultForeground { get; }
        byte DefaultBackground { get; }

        UnicodeCaps CheckUnicode(char ch);
    }

    partial class ZMachine
    {
        private const int DICT_WORD_SIZE = 9;

        private void PrintZSCII(short zc)
        {
            if (zc == 0)
                return;

            if (tableOutput)
            {
                List<byte> buffer = tableOutputBufferStack.Peek();
                buffer.Add((byte)zc);
            }
            else
            {
                char ch = CharFromZSCII(zc);
                if (normalOutput)
                    io.PutChar(ch);
                if (io.Transcripting)
                    io.PutTranscriptChar(ch);
            }
        }

        private void PrintUnicode(ushort uc)
        {
            if (tableOutput)
            {
                List<byte> buffer = tableOutputBufferStack.Peek();
                buffer.Add((byte)CharToZSCII((char)uc));
            }
            else
            {
                if (normalOutput)
                    io.PutChar((char)uc);
                if (io.Transcripting)
                    io.PutTranscriptChar((char)uc);
            }
        }

        private void PrintString(string str)
        {
            if (tableOutput)
            {
                List<byte> buffer = tableOutputBufferStack.Peek();
                foreach (char ch in str)
                    buffer.Add((byte)CharToZSCII(ch));
            }
            else
            {
                if (normalOutput)
                    io.PutString(str);
                if (io.Transcripting)
                    io.PutTranscriptString(str);
            }
        }

        private char CharFromZSCII(short ch)
        {
            switch (ch)
            {
                case 13:
                    return '\n';

                default:
                    if (ch >= 155 && ch < 155 + extraChars.Length)
                        return extraChars[ch - 155];
                    else
                        return (char)ch;
            }
        }

        private short CharToZSCII(char ch)
        {
            switch (ch)
            {
                case '\n':
                    return 13;

                default:
                    int idx = Array.IndexOf(extraChars, ch);
                    if (idx >= 0)
                        return (short)(155 + idx);
                    else
                        return (short)ch;
            }
        }

        private byte[] StringToZSCII(string str)
        {
            byte[] result = new byte[str.Length];
            for (int i = 0; i < str.Length; i++)
                result[i] = (byte)CharToZSCII(str[i]);
            return result;
        }

        // default alphabets (S 3.5.3)
        private static readonly char[] defaultAlphabet0 =
            { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
              'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
        private static readonly char[] defaultAlphabet1 =
            { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
              'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };
        private static readonly char[] defaultAlphabet2 =
            { ' ', '\n', '0', '1', '2', '3',  '4', '5', '6',  '7', '8', '9', '.',
              ',', '!',  '?', '_', '#', '\'', '"', '/', '\\', '-', ':', '(', ')' };

        // default Unicode translations (S 3.8.5.3)
        private static readonly char[] defaultExtraChars =
            { '\u00e4', '\u00f6', '\u00fc', '\u00c4', '\u00d6', '\u00dc', '\u00df', '\u00bb', '\u00ab', '\u00eb', // 155
              '\u00ef', '\u00ff', '\u00cb', '\u00cf', '\u00e1', '\u00e9', '\u00ed', '\u00f3', '\u00fa', '\u00fd', // 165
              '\u00c1', '\u00c9', '\u00cd', '\u00d3', '\u00da', '\u00dd', '\u00e0', '\u00e8', '\u00ec', '\u00f2', // 175
              '\u00f9', '\u00c0', '\u00c8', '\u00cc', '\u00d2', '\u00d9', '\u00e2', '\u00ea', '\u00ee', '\u00f4', // 185
              '\u00fb', '\u00c2', '\u00ca', '\u00ce', '\u00d4', '\u00db', '\u00e5', '\u00c5', '\u00f8', '\u00d8', // 195
              '\u00e3', '\u00f1', '\u00f5', '\u00c3', '\u00d1', '\u00d5', '\u00e6', '\u00c6', '\u00e7', '\u00c7', // 205
              '\u00fe', '\u00f0', '\u00de', '\u00d0', '\u00a3', '\u0153', '\u0152', '\u00a1', '\u00bf' };         // 215

        private string DecodeString(int address)
        {
            int dummy;
            return DecodeStringWithLen(address, out dummy);
        }

        private string DecodeStringWithLen(int address, out int len)
        {
            len = 0;

            int alphabet = 0;
            int abbrevMode = 0;
            short word;
            StringBuilder sb = new StringBuilder();

            do
            {
                word = GetWord(address);
                address += 2;
                len += 2;

                DecodeChar((word >> 10) & 0x1F, ref alphabet, ref abbrevMode, sb);
                DecodeChar((word >> 5) & 0x1F, ref alphabet, ref abbrevMode, sb);
                DecodeChar((word) & 0x1F, ref alphabet, ref abbrevMode, sb);
            } while ((word & 0x8000) == 0);

            return sb.ToString();
        }

        private void DecodeChar(int zchar, ref int alphabet, ref int abbrevMode, StringBuilder sb)
        {
            switch (abbrevMode)
            {
                case 1:
                case 2:
                case 3:
                    sb.Append(CharFromZSCII((short)(32 * (abbrevMode - 1) + zchar)));
                    abbrevMode = 0;
                    return;

                case 4:
                    abbrevMode = 5;
                    alphabet = zchar;
                    return;
                case 5:
                    abbrevMode = 0;
                    sb.Append(CharFromZSCII((short)((alphabet << 5) + zchar)));
                    alphabet = 0;
                    return;
            }

            switch (zchar)
            {
                case 0:
                    sb.Append(' ');
                    return;

                case 1:
                case 2:
                case 3:
                    abbrevMode = zchar;
                    return;

                case 4:
                    alphabet = 1;
                    return;
                case 5:
                    alphabet = 2;
                    return;
            }

            zchar -= 6;
            switch (alphabet)
            {
                case 0:
                    sb.Append(alphabet0[zchar]);
                    return;

                case 1:
                    sb.Append(alphabet1[zchar]);
                    alphabet = 0;
                    return;

                case 2:
                    if (zchar == 0)
                        abbrevMode = 4;
                    else
                        sb.Append(alphabet2[zchar]);
                    alphabet = 0;
                    return;
            }
        }

        private short ReadImpl(ushort buffer, ushort parse, ushort time, ushort routine)
        {
            byte max = GetByte(buffer);
            byte offset = GetByte(buffer + 1);

            byte terminator;
            string str;

            BeginExternalWait();
            try
            {
                str = io.ReadLine(time, delegate { return HandleInputTimer(routine); },
                    terminatingChars, out terminator);
            }
            finally
            {
                EndExternalWait();
            }

            byte[] chars = StringToZSCII(str);
            SetByte(buffer + 1, (byte)chars.Length);
            for (int i = 0; i < Math.Min(chars.Length, max - offset); i++)
                SetByte(buffer + 2 + offset + i, chars[i]);

            if (parse != 0)
                Tokenize(buffer, parse, 0, false);

            return terminator;
        }

        private short ReadCharImpl(ushort time, ushort routine)
        {
            BeginExternalWait();
            try
            {
                return io.ReadKey(time, delegate { return HandleInputTimer(routine); }, CharToZSCII);
            }
            finally
            {
                EndExternalWait();
            }
        }

        private bool HandleInputTimer(ushort routine)
        {
            EnterFunctionImpl((short)routine, null, 0, pc);

            JitLoop();

            short result = stack.Pop();
            return (result != 0);
        }

        private void HandleSoundFinished(ushort routine)
        {
            EnterFunctionImpl((short)routine, null, 0, pc);
            JitLoop();
        }

        private struct Token
        {
            public byte StartPos, Length;

            public Token(byte startPos, byte Length)
            {
                this.StartPos = startPos;
                this.Length = Length;
            }
        }

        private bool IsTokenSpace(byte ch)
        {
            return (ch == 9) || (ch == 32);
        }

        private List<Token> SplitTokens(byte[] buffer, ushort userDict)
        {
            List<Token> result = new List<Token>();
            byte[] seps;

            if (userDict == 0)
            {
                seps = wordSeparators;
            }
            else
            {
                byte n = GetByte(userDict);
                seps = new byte[n];
                GetBytes(userDict + 1, n, seps, 0);
            }

            int i = 0;
            do
            {
                // skip whitespace
                while (i < buffer.Length && IsTokenSpace(buffer[i]))
                    i++;

                if (i >= buffer.Length)
                    break;

                // found a separator?
                if (Array.IndexOf(seps, buffer[i]) >= 0)
                {
                    result.Add(new Token((byte)i, 1));
                    i++;
                }
                else
                {
                    byte start = (byte)i;

                    // find the end of the word
                    while (i < buffer.Length && !IsTokenSpace(buffer[i]) &&
                            Array.IndexOf(seps, buffer[i]) == -1)
                    {
                        i++;
                    }

                    // add it to the list
                    result.Add(new Token(start, (byte)(i - start)));
                }
            } while (i < buffer.Length);

            return result;
        }

        private void Tokenize(ushort buffer, ushort parse, ushort userDict, bool skipUnrecognized)
        {
            byte bufLen = GetByte(buffer + 1);
            byte max = GetByte(parse + 0);
            byte count = 0;

            byte[] myBuffer = new byte[bufLen];
            GetBytes(buffer + 2, bufLen, myBuffer, 0);
            List<Token> tokens = SplitTokens(myBuffer, userDict);

            foreach (Token tok in tokens)
            {
                ushort word = LookUpWord(userDict, myBuffer, tok.StartPos, tok.Length);
                if (word == 0 && skipUnrecognized)
                    continue;

                SetWord(parse + 2 + 4 * count, (short)word);
                SetByte(parse + 2 + 4 * count + 2, tok.Length);
                SetByte(parse + 2 + 4 * count + 3, (byte)(2 + tok.StartPos));
                count++;

                if (count == max)
                    break;
            }

            SetByte(parse + 1, count);
        }

        private ushort LookUpWord(int userDict, byte[] buffer, int pos, int length)
        {
            int dictStart;
            byte[] word;

            word = EncodeText(buffer, pos, length, DICT_WORD_SIZE);

            if (userDict != 0)
            {
                byte n = GetByte(userDict);
                dictStart = userDict + 1 + n;
            }
            else
            {
                dictStart = dictionaryTable + 1 + wordSeparators.Length;
            }

            byte entryLength = GetByte(dictStart++);

            int entries;
            if (userDict == 0)
                entries = (ushort)GetWord(dictStart);
            else
                entries = GetWord(dictStart);
            dictStart += 2;

            if (entries < 0)
            {
                // use linear search for unsorted user dictionary
                for (int i = 0; i < entries; i++)
                {
                    int addr = dictStart + i * entryLength;
                    if (CompareWords(word, addr) == 0)
                        return (ushort)addr;
                }
            }
            else
            {
                // use binary search
                int start = 0, end = entries;
                while (start < end)
                {
                    int mid = (start + end) / 2;
                    int addr = dictStart + mid * entryLength;
                    int cmp = CompareWords(word, addr);
                    if (cmp == 0)
                        return (ushort)addr;
                    else if (cmp < 0)
                        end = mid;
                    else
                        start = mid + 1;
                }
            }

            return 0;
        }

        private int CompareWords(byte[] word, int addr)
        {
            for (int i = 0; i < word.Length; i++)
            {
                int cmp = word[i] - GetByte(addr + i);
                if (cmp != 0)
                    return cmp;
            }

            return 0;
        }

        /// <summary>
        /// Encodes a section of text, optionally truncating or padding the output to a fixed size.
        /// </summary>
        /// <param name="input">The buffer containing the plain text.</param>
        /// <param name="start">The index within <paramref name="input"/> where the
        /// plain text starts.</param>
        /// <param name="length">The length of the plain text.</param>
        /// <param name="numZchars">The number of 5-bit characters that the output should be
        /// truncated or padded to, which must be a multiple of 3; or 0 to allow variable size
        /// output (padded up to a multiple of 2 bytes, if necessary).</param>
        /// <returns>The encoded text, with th.</returns>
        private byte[] EncodeText(byte[] input, int start, int length, int numZchars)
        {
            List<byte> zchars;
            if (numZchars == 0)
            {
                zchars = new List<byte>(length);
            }
            else
            {
                if (numZchars < 0 || numZchars % 3 != 0)
                    throw new ArgumentException("Output size must be a multiple of 3", "numZchars");
                zchars = new List<byte>(numZchars);
            }

            for (int i = 0; i < length; i++)
            {
                byte zc = input[start + i];
                char ch = char.ToLower(CharFromZSCII(zc));

                if (ch == ' ')
                {
                    zchars.Add(0);
                }
                else
                {
                    int alpha;
                    if ((alpha = Array.IndexOf(alphabet0, ch)) >= 0)
                    {
                        zchars.Add((byte)(alpha + 6));
                    }
                    else if ((alpha = Array.IndexOf(alphabet1, ch)) >= 0)
                    {
                        zchars.Add(4);
                        zchars.Add((byte)(alpha + 6));
                    }
                    else if ((alpha = Array.IndexOf(alphabet2, ch)) >= 0)
                    {
                        zchars.Add(5);
                        zchars.Add((byte)(alpha + 6));
                    }
                    else
                    {
                        zchars.Add(5);
                        zchars.Add(6);
                        zchars.Add((byte)(zc >> 5));
                        zchars.Add((byte)(zc & 31));
                    }
                }
            }

            int resultBytes;
            if (numZchars == 0)
            {
                // pad up to a multiple of 3
                while (zchars.Count % 3 != 0)
                    zchars.Add(5);
                resultBytes = zchars.Count * 2 / 3;
            }
            else
            {
                // pad up to the fixed size
                while (zchars.Count < numZchars)
                    zchars.Add(5);
                resultBytes = numZchars * 2 / 3;
            }

            byte[] result = new byte[resultBytes];
            int zi = 0, ri = 0;
            while (ri < resultBytes)
            {
                result[ri] = (byte)((zchars[zi] << 2) | (zchars[zi + 1] >> 3));
                result[ri + 1] = (byte)((zchars[zi + 1] << 5) | zchars[zi + 2]);
                ri += 2;
                zi += 3;
            }

            result[resultBytes - 2] |= 128;
            return result;
        }

        private void SetOutputStream(short num, ushort address)
        {
            bool enabled = true;
            if (num < 0)
            {
                num = (short)-num;
                enabled = false;
            }

            switch (num)
            {
                case 1:
                    // normal
                    normalOutput = enabled;
                    break;

                case 2:
                    // transcript
                    io.Transcripting = enabled;
                    break;

                case 3:
                    // memory (nestable up to 16 levels)
                    if (enabled)
                    {
                        if (tableOutputAddrStack.Count == 16)
                            throw new Exception("Output stream 3 nested too deeply");
                        if (address < 64 || address + 1 >= romStart)
                            throw new Exception("Output stream 3 address is out of range");

                        tableOutput = true;
                        tableOutputAddrStack.Push(address);
                        tableOutputBufferStack.Push(new List<byte>());
                    }
                    else if (tableOutput)
                    {
                        address = tableOutputAddrStack.Pop();
                        List<byte> buffer = tableOutputBufferStack.Pop();

                        int len = Math.Min(buffer.Count, romStart - address - 2);
                        SetWord(address, (short)len);
                        for (int i = 0; i < len; i++)
                            SetByte(address + 2 + i, buffer[i]);

                        if (tableOutputAddrStack.Count == 0)
                            tableOutput = false;
                    }
                    break;

                case 4:
                    // player's commands
                    io.WritingCommandsToFile = enabled;
                    break;

                default:
                    throw new Exception("Invalid output stream #" + num.ToString());
            }
        }

        private void SetInputStream(short num)
        {
            switch (num)
            {
                case 0:
                    io.ReadingCommandsFromFile = false;
                    break;

                case 1:
                    io.ReadingCommandsFromFile = true;
                    break;

                default:
                    throw new Exception("Invalid input stream #" + num.ToString());
            }
        }

        private void GetCursorPos(ushort address)
        {
            short x, y;
            io.GetCursorPos(out x, out y);
            SetWordChecked(address, y);
            SetWordChecked(address + 2, x);
        }
    }
}