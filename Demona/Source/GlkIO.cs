using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ZLR.VM;

namespace ZLR.Interfaces.Demona
{
    class GlkIO : IZMachineIO, IDisposable
    {
        private winid_t upperWin, lowerWin;
        private winid_t currentWin;
        private bool forceFixed;
        private int xpos, ypos; // in Glk coordinates (i.e. counting from 0)
        private uint screenWidth, screenHeight;
        private TextStyle lastStyle;

        private frefid_t transcriptFile;
        private strid_t transcriptStream;
        private schanid_t soundChannel;
        private SoundFinishedCallback soundCallback;

        private IntPtr argv;
        private IntPtr[] argvStrings;

        public GlkIO(string[] args, string storyName)
        {
            // initialize Glk
            // first, add the application's path to the beginning of the arg list
            string[] newArgs = new string[args.Length + 1];
            newArgs[0] = Application.ExecutablePath;
            Array.Copy(args, 0, newArgs, 1, args.Length);
            args = newArgs;

            // now, GarGlk keeps pointers into argv, so we have to copy the args into unmanaged memory
            argv = Marshal.AllocHGlobal(4 * (args.Length + 1));
            argvStrings = new IntPtr[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                IntPtr str = Marshal.StringToHGlobalAnsi(args[i]);
                argvStrings[i] = str;
                Marshal.WriteIntPtr(argv, 4 * i, str);
            }
            Marshal.WriteIntPtr(argv, 4 * args.Length, IntPtr.Zero);
            Glk.gli_startup(args.Length, argv);

            Glk.garglk_set_program_name("Demona");
            Glk.garglk_set_program_info("Demona by Jesse McGrew\nA Glk interface for ZLR\nVersion " + ZMachine.ZLR_VERSION);
            Glk.garglk_set_story_name(storyName);

            // set style hints
            Glk.glk_stylehint_set(WinType.AllTypes, Style.User1, StyleHint.ReverseColor, 1);

            Glk.glk_stylehint_set(WinType.AllTypes, Style.User2, StyleHint.Weight, 1);
            Glk.glk_stylehint_set(WinType.AllTypes, Style.User2, StyleHint.Proportional, 0);

            // figure out how big the screen is
            winid_t tempWin = Glk.glk_window_open(winid_t.Null, 0, 0, WinType.TextGrid, 0);
            if (tempWin.IsNull)
            {
                screenWidth = 80;
                screenHeight = 25;
            }
            else
            {
                Glk.glk_window_get_size(tempWin, out screenWidth, out screenHeight);
                stream_result_t dummy;
                Glk.glk_window_close(tempWin, out dummy);
            }

            // open the lower window
            lowerWin = Glk.glk_window_open(winid_t.Null, 0, 0, WinType.TextBuffer, 0);
            if (lowerWin.IsNull)
                throw new Exception("glk_window_open failed");

            Glk.glk_set_window(lowerWin);
            currentWin = lowerWin;

            xpos = 0;
            ypos = 0;
        }

        public event EventHandler SizeChanged;

        private void UpdateScreenSize()
        {
            if (!upperWin.IsNull)
            {
                uint width, height;
                Glk.glk_window_get_size(upperWin, out width, out height);
                screenWidth = width;

                if (SizeChanged != null)
                    SizeChanged(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~GlkIO()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
                Glk.glk_exit();

            if (argvStrings != null)
            {
                foreach (IntPtr str in argvStrings)
                    Marshal.FreeHGlobal(str);
                argvStrings = null;
            }

            if (argv != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(argv);
                argv = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }

        #region IZMachineIO Members

        string IZMachineIO.ReadLine(int time, TimedInputCallback callback, byte[] terminatingKeys, out byte terminator)
        {
            const int BUFSIZE = 256;

            IntPtr buf = Marshal.AllocHGlobal(BUFSIZE);
            try
            {
                Glk.glk_request_line_event(currentWin, buf, BUFSIZE, 0);
                Glk.glk_request_timer_events((uint)(time * 100));

                terminator = 0;

                event_t ev;
                bool done = false;
                do
                {
                    Glk.glk_select(out ev);

                    switch (ev.type)
                    {
                        case EvType.LineInput:
                            if (ev.win == currentWin)
                            {
                                done = true;
                                terminator = 13;
                            }
                            break;

                        case EvType.Timer:
                            if (callback() == true)
                            {
                                Glk.glk_cancel_line_event(currentWin, out ev);
                                done = true;
                            }
                            break;

                        case EvType.Arrange:
                            UpdateScreenSize();
                            break;

                        case EvType.SoundNotify:
                            SoundNotify();
                            break;
                    }
                }
                while (!done);

                Glk.glk_request_timer_events(0);

                // convert the string from Latin-1
                int length = (int)ev.val1;
                byte[] bytes = new byte[length];
                Marshal.Copy(buf, bytes, 0, length);
                return Encoding.GetEncoding(Glk.LATIN1).GetString(bytes);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        short IZMachineIO.ReadKey(int time, TimedInputCallback callback, CharTranslator translator)
        {
            Glk.glk_request_char_event(currentWin);
            Glk.glk_request_timer_events((uint)(time * 100));

            event_t ev;
            bool done = false;
            short result = 0;
            do
            {
                Glk.glk_select(out ev);

                switch (ev.type)
                {
                    case EvType.CharInput:
                        if (ev.win == currentWin)
                        {
                            done = true;
                            if (ev.val1 <= 255)
                            {
                                result = translator((char)ev.val1);
                            }
                            else
                            {
                                switch ((KeyCode)ev.val1)
                                {
                                    case KeyCode.Delete: result = 8; break;
                                    case KeyCode.Return: result = 13; break;
                                    case KeyCode.Escape: result = 27; break;
                                    case KeyCode.Up: result = 129; break;
                                    case KeyCode.Down: result = 130; break;
                                    case KeyCode.Left: result = 131; break;
                                    case KeyCode.Right: result = 132; break;
                                    case KeyCode.Func1: result = 133; break;
                                    case KeyCode.Func2: result = 134; break;
                                    case KeyCode.Func3: result = 135; break;
                                    case KeyCode.Func4: result = 136; break;
                                    case KeyCode.Func5: result = 137; break;
                                    case KeyCode.Func6: result = 138; break;
                                    case KeyCode.Func7: result = 139; break;
                                    case KeyCode.Func8: result = 140; break;
                                    case KeyCode.Func9: result = 141; break;
                                    case KeyCode.Func10: result = 142; break;
                                    case KeyCode.Func11: result = 143; break;
                                    case KeyCode.Func12: result = 144; break;
                                    default: result = 0; break;
                                }
                            }
                        }
                        break;

                    case EvType.Timer:
                        Glk.glk_cancel_char_event(currentWin);
                        done = true;
                        break;

                    case EvType.Arrange:
                        UpdateScreenSize();
                        break;

                    case EvType.SoundNotify:
                        SoundNotify();
                        break;
                }
            }
            while (!done);

            Glk.glk_request_timer_events(0);
            return result;
        }

        bool IZMachineIO.ReadingCommandsFromFile
        {
            get { return false; }
            set { }
        }

        bool IZMachineIO.WritingCommandsToFile
        {
            get { return false; }
            set { }
        }

        private readonly char[] encodingChar = new char[1];
        private readonly byte[] encodedBytes = new byte[2];

        void IZMachineIO.PutChar(char ch)
        {
            byte b;
            encodingChar[0] = ch;
            int result = Encoding.GetEncoding(Glk.LATIN1).GetBytes(encodingChar, 0, 1, encodedBytes, 0);
            if (result != 1)
                b = (byte)'?';
            else
                b = encodedBytes[0];
            Glk.glk_put_char(b);

            if (currentWin == upperWin)
            {
                xpos++;
                uint width, height;
                Glk.glk_window_get_size(upperWin, out width, out height);
                if (xpos >= width)
                {
                    xpos = 0;
                    ypos++;
                    if (ypos >= height)
                        ypos = (int)height - 1;
                }
            }
        }

        void IZMachineIO.PutString(string str)
        {
            Glk.glk_put_string(str);

            if (currentWin == upperWin)
            {
                xpos += str.Length;
                uint width, height;
                Glk.glk_window_get_size(upperWin, out width, out height);
                while (xpos >= width)
                {
                    xpos -= (int)width;
                    ypos++;
                    if (ypos >= height)
                        ypos = (int)height - 1;
                }
            }
        }

        void IZMachineIO.PutTextRectangle(string[] lines)
        {
            if (currentWin == lowerWin)
            {
                foreach (string str in lines)
                {
                    Glk.glk_put_string(str);
                    Glk.glk_put_char((byte)'\n');
                }
            }
            else
            {
                int oxpos = xpos;

                foreach (string str in lines)
                {
                    Glk.glk_window_move_cursor(upperWin, (uint)oxpos, (uint)ypos);
                    xpos = oxpos + str.Length;
                    if (xpos >= screenWidth)
                        xpos = (int)screenWidth - 1;
                }

                if (lines.Length > 0)
                {
                    ypos += lines.Length - 1;
                    if (ypos >= screenHeight)
                        ypos = (int)screenHeight - 1;
                }
            }
        }

        bool IZMachineIO.Buffering
        {
            get { return true; }
            set { /* can't really change this */ }
        }

        bool IZMachineIO.Transcripting
        {
            get
            {
                return !transcriptStream.IsNull;
            }
            set
            {
                if (value == false && !transcriptStream.IsNull)
                {
                    stream_result_t dummy;
                    Glk.glk_stream_close(transcriptStream, out dummy);
                    transcriptStream = strid_t.Null;
                }
                else if (value==true && transcriptStream.IsNull)
                {
                    if (transcriptFile.IsNull)
                    {
                        transcriptFile = Glk.glk_fileref_create_by_prompt(
                            FileUsage.Transcript | FileUsage.TextMode, FileMode.WriteAppend, 0);
                        if (transcriptFile.IsNull)
                            return;
                    }

                    transcriptStream = Glk.glk_stream_open_file(transcriptFile, FileMode.WriteAppend, 0);
                }
            }
        }

        void IZMachineIO.PutTranscriptChar(char ch)
        {
            if (ch > 255)
                ch = '?';

            if (!transcriptStream.IsNull)
                Glk.glk_put_char_stream(transcriptStream, (byte)ch);
        }

        void IZMachineIO.PutTranscriptString(string str)
        {
            if (!transcriptStream.IsNull)
                Glk.glk_put_string_stream(transcriptStream, str);
        }

        Stream IZMachineIO.OpenSaveFile(int size)
        {
            frefid_t file = Glk.glk_fileref_create_by_prompt(
                FileUsage.SavedGame | FileUsage.BinaryMode, FileMode.Write, 0);
            return OpenStream(file, FileMode.Write);
        }

        Stream IZMachineIO.OpenRestoreFile()
        {
            frefid_t file = Glk.glk_fileref_create_by_prompt(
                FileUsage.SavedGame | FileUsage.BinaryMode, FileMode.Read, 0);
            return OpenStream(file, FileMode.Read);
        }

        Stream IZMachineIO.OpenAuxiliaryFile(string name, int size, bool writing)
        {
            frefid_t file = Glk.glk_fileref_create_by_name(
                FileUsage.Data | FileUsage.BinaryMode, name, 0);
            return OpenStream(file, writing ? FileMode.Write : FileMode.Read);
        }

        private Stream OpenStream(frefid_t fileref, FileMode mode)
        {
            if (fileref.IsNull)
                return null;

            strid_t gstr = Glk.glk_stream_open_file(fileref, mode, 0);
            if (gstr.IsNull)
                return null;

            return new GlkStream(gstr);
        }

        void IZMachineIO.SetTextStyle(TextStyle style)
        {
            Style glkStyle;

            // the full range of styles is only available when force fixed is off, or
            // when the upper window is selected (since force fixed has no effect there)
            if (!forceFixed || currentWin == upperWin)
            {
                switch (style)
                {
                    case TextStyle.Roman:
                        glkStyle = Style.Normal;
                        break;

                    case TextStyle.Reverse:
                        glkStyle = Style.User1;
                        break;

                    case TextStyle.Bold:
                        glkStyle = Style.Subheader;
                        break;

                    case TextStyle.Italic:
                        glkStyle = Style.Emphasized;
                        break;

                    case TextStyle.FixedPitch:
                        glkStyle = Style.Preformatted;
                        break;

                    default:
                        return;
                }
            }
            else
            {
                // when force fixed is on in the lower window, just choose between preformatted and bold-preformatted
                switch (style)
                {
                    case TextStyle.Reverse:
                    case TextStyle.Bold:
                    case TextStyle.Italic:
                        glkStyle = Style.User2;
                        break;

                    default:
                        glkStyle = Style.Preformatted;
                        break;
                }
            }

            lastStyle = style;
            Glk.glk_set_style(glkStyle);
        }

        void IZMachineIO.SplitWindow(short lines)
        {
            if (lines > 0)
            {
                if (upperWin.IsNull)
                    upperWin = Glk.glk_window_open(lowerWin, WinMethod.Above | WinMethod.Fixed,
                        (uint)lines, WinType.TextGrid, 0);
                else
                    Glk.glk_window_set_arrangement(Glk.glk_window_get_parent(upperWin),
                        WinMethod.Above | WinMethod.Fixed, (uint)lines, winid_t.Null);
            }
            else
            {
                if (!upperWin.IsNull)
                {
                    stream_result_t dummy;
                    Glk.glk_window_close(upperWin, out dummy);
                    upperWin = winid_t.Null;
                    currentWin = lowerWin;
                }
            }
        }

        void IZMachineIO.SelectWindow(short num)
        {
            if (num == 0)
            {
                currentWin = lowerWin;
            }
            else if (num == 1)
            {
                /* work around a bug in some Inform games where the screen is erased,
                 * destroying the split, but the split isn't restored before drawing
                 * the status line. */
                if (upperWin.IsNull)
                    ((IZMachineIO)this).SplitWindow(1);

                currentWin = upperWin;
            }

            Glk.glk_set_window(currentWin);
        }

        void IZMachineIO.EraseWindow(short num)
        {
            switch (num)
            {
                case 0:
                    // lower only
                    Glk.glk_window_clear(lowerWin);
                    break;

                case 1:
                    // upper only
                    if (!upperWin.IsNull)
                        Glk.glk_window_clear(upperWin);
                    break;

                case -1:
                    // erase both and unsplit
                    if (!upperWin.IsNull)
                    {
                        stream_result_t dummy;
                        Glk.glk_window_close(upperWin, out dummy);
                        upperWin = winid_t.Null;
                    }
                    goto case -2;
                case -2:
                    // erase both but keep split
                    if (!upperWin.IsNull)
                        Glk.glk_window_clear(upperWin);
                    Glk.glk_window_clear(lowerWin);
                    currentWin = lowerWin;
                    xpos = 0; ypos = 0;
                    break;
            }
        }

        void IZMachineIO.EraseLine()
        {
            if (currentWin == upperWin)
            {
                uint width, height;
                Glk.glk_window_get_size(upperWin, out width, out height);
                for (int i = xpos; i < width; i++)
                    Glk.glk_put_char((byte)' ');
                Glk.glk_window_move_cursor(upperWin, (uint)xpos, (uint)ypos);
            }
        }

        void IZMachineIO.MoveCursor(short x, short y)
        {
            // convert to Glk coordinates
            x--; y--;

            if (currentWin == upperWin)
            {
                uint width, height;
                Glk.glk_window_get_size(upperWin, out width, out height);

                if (x < 0)
                    xpos = 0;
                else if (x >= width)
                    xpos = (int)width - 1;
                else
                    xpos = x;

                if (y < 0)
                    ypos = 0;
                else if (y >= height)
                    ypos = (int)height - 1;
                else
                    ypos = y;

                Glk.glk_window_move_cursor(upperWin, (uint)xpos, (uint)ypos);
            }
        }

        void IZMachineIO.GetCursorPos(out short x, out short y)
        {
            // convert to Z-machine coordinates
            x = (short)(xpos + 1);
            y = (short)(ypos + 1);
        }

        void IZMachineIO.SetColors(short fg, short bg)
        {
            // not supported
        }

        short IZMachineIO.SetFont(short num)
        {
            // not supported
            return 0;
        }

        bool IZMachineIO.GraphicsFontAvailable
        {
            get { return false; }
        }

        void IZMachineIO.PlaySoundSample(ushort number, SoundAction action, byte volume, byte repeats,
            SoundFinishedCallback callback)
        {
            switch (action)
            {
                case SoundAction.Prepare:
                    Glk.glk_sound_load_hint(number, true);
                    break;

                case SoundAction.FinishWith:
                    Glk.glk_sound_load_hint(number, false);
                    break;

                case SoundAction.Start:
                    if (soundChannel.IsNull)
                        soundChannel = Glk.glk_schannel_create(0);

                    if (!soundChannel.IsNull)
                    {
                        volume = Math.Min(volume, (byte)8);
                        Glk.glk_schannel_set_volume(soundChannel, (uint)(volume << 13));
                        soundCallback = callback;
                        Glk.glk_schannel_play_ext(soundChannel, number, repeats, 1);
                    }
                    break;

                case SoundAction.Stop:
                    if (!soundChannel.IsNull)
                    {
                        Glk.glk_schannel_stop(soundChannel);
                        soundChannel = schanid_t.Null;
                        soundCallback = null;
                    }
                    break;
            }
        }

        private void SoundNotify()
        {
            if (soundCallback != null)
                soundCallback();
        }

        void IZMachineIO.PlayBeep(bool highPitch)
        {
            Console.Beep(highPitch ? 1600 : 800, 200);
        }

        bool IZMachineIO.ForceFixedPitch
        {
            get
            {
                return forceFixed;
            }
            set
            {
                if (forceFixed != value)
                {
                    forceFixed = value;
                    ((IZMachineIO)this).SetTextStyle(lastStyle);
                }
            }
        }

        bool IZMachineIO.BoldAvailable
        {
            get { return true; }
        }

        bool IZMachineIO.ItalicAvailable
        {
            get { return true; }
        }

        bool IZMachineIO.FixedPitchAvailable
        {
            get { return true; }
        }

        bool IZMachineIO.TimedInputAvailable
        {
            get { return Glk.glk_gestalt(Gestalt.Timer, 0) != 0; }
        }

        bool IZMachineIO.ColorsAvailable
        {
            get { return false; }
        }

        bool IZMachineIO.SoundSamplesAvailable
        {
            get { return false; }
        }

        byte IZMachineIO.WidthChars
        {
            get
            {
                if (screenWidth > 255)
                    return 255;
                else
                    return (byte)screenWidth;
            }
        }

        short IZMachineIO.WidthUnits
        {
            get
            {
                if (screenWidth > 32767)
                    return 32767;
                else
                    return (short)screenWidth;
            }
        }

        byte IZMachineIO.HeightChars
        {
            get
            {
                if (screenHeight > 255)
                    return 255;
                else
                    return (byte)screenHeight;
            }
        }

        short IZMachineIO.HeightUnits
        {
            get
            {
                if (screenHeight > 32767)
                    return 32767;
                else
                    return (short)screenHeight;
            }
        }

        byte IZMachineIO.FontHeight
        {
            get { return 1; }
        }

        byte IZMachineIO.FontWidth
        {
            get { return 1; }
        }

        byte IZMachineIO.DefaultForeground
        {
            get { return 9; }
        }

        byte IZMachineIO.DefaultBackground
        {
            get { return 2; }
        }

        UnicodeCaps IZMachineIO.CheckUnicode(char ch)
        {
            UnicodeCaps result = 0;
            if (ch < 255)
            {
                if (Glk.glk_gestalt(Gestalt.CharOutput, ch) != 0)
                    result |= UnicodeCaps.CanPrint;
                if (Glk.glk_gestalt(Gestalt.CharInput, ch) != 0)
                    result |= UnicodeCaps.CanInput;
            }
            return result;
        }

        #endregion
    }

    internal class GlkStream : Stream
    {
        private strid_t gstr;

        public GlkStream(strid_t gstr)
        {
            if (gstr.IsNull)
                throw new ArgumentNullException("gstr");

            this.gstr = gstr;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
            // nada
        }

        protected override void Dispose(bool disposing)
        {
            if (!gstr.IsNull)
            {
                stream_result_t dummy;
                Glk.glk_stream_close(gstr, out dummy);
                gstr = strid_t.Null;
            }
        }

        public override long Length
        {
            get
            {
                uint curpos = Glk.glk_stream_get_position(gstr);

                Glk.glk_stream_set_position(gstr, 0, SeekMode.End);
                uint length = Glk.glk_stream_get_position(gstr);

                Glk.glk_stream_set_position(gstr, (int)curpos, SeekMode.Start);
                return length;
            }
        }

        public override long Position
        {
            get { return Glk.glk_stream_get_position(gstr); }
            set { Glk.glk_stream_set_position(gstr, (int)value, SeekMode.Start); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset == 0)
            {
                return (int)Glk.glk_get_buffer_stream(gstr, buffer, (uint)count);
            }
            else
            {
                byte[] temp = new byte[count];
                int actual = (int)Glk.glk_get_buffer_stream(gstr, temp, (uint)count);
                Array.Copy(temp, 0, buffer, offset, actual);
                return actual;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            SeekMode gseek;
            switch (origin)
            {
                case SeekOrigin.Begin: gseek = SeekMode.Start; break;
                case SeekOrigin.Current: gseek = SeekMode.Current; break;
                case SeekOrigin.End: gseek = SeekMode.End; break;
                default: throw new ArgumentOutOfRangeException("origin");
            }
            Glk.glk_stream_set_position(gstr, (int)offset, gseek);
            return Glk.glk_stream_get_position(gstr);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("GlkStream cannot set stream length");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (offset == 0)
            {
                Glk.glk_put_buffer_stream(gstr, buffer, (uint)count);
            }
            else
            {
                byte[] temp = new byte[count];
                Array.Copy(buffer, offset, temp, 0, count);
                Glk.glk_put_buffer_stream(gstr, temp, (uint)count);
            }
        }
    }
}
