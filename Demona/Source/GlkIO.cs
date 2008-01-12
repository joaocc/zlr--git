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
        private bool unicode;

        private winid_t upperWin, lowerWin;
        private winid_t currentWin;
        private bool forceFixed;
        private int xpos, ypos; // in Glk coordinates (i.e. counting from 0)
        private uint screenWidth, screenHeight;
        private TextStyle lastStyle;
        private int targetSplit;

        private frefid_t transcriptFile;
        private strid_t transcriptStream;
        private schanid_t soundChannel;
        private SoundFinishedCallback soundCallback;

        private IntPtr argv;
        private IntPtr[] argvStrings;

        // used to temporarily cancel line input when the timer callback needs to print
        private bool lineInputActive;
        private event_t canceledLineEvent;

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

            unicode = (Glk.glk_gestalt(Gestalt.Unicode, 0) != 0);
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

        string IZMachineIO.ReadLine(string initial, int time, TimedInputCallback callback, byte[] terminatingKeys, out byte terminator)
        {
            const int BUFSIZE = 256;
            IntPtr buf = Marshal.AllocHGlobal(unicode ? BUFSIZE * 4 : BUFSIZE);
            Encoding encoding = unicode ? Encoding.UTF32 : Encoding.GetEncoding(Glk.LATIN1);

            try
            {
                uint initlen = 0;

                if (initial.Length > 0)
                {
                    if (unicode)
                        Glk.garglk_unput_string_uni(initial);
                    else
                        Glk.garglk_unput_string(initial);

                    byte[] initBytes = encoding.GetBytes(initial);
                    Marshal.Copy(initBytes, 0, buf, initBytes.Length);

                    initlen = (uint)initBytes.Length;
                    if (unicode)
                        initlen /= 4;
                }

                if (unicode)
                    Glk.glk_request_line_event_uni(currentWin, buf, BUFSIZE, initlen);
                else
                    Glk.glk_request_line_event(currentWin, buf, BUFSIZE, initlen);
                Glk.glk_request_timer_events((uint)(time * 100));

                KeyCode[] glkTerminators = null;
                if (terminatingKeys != null && terminatingKeys.Length > 0)
                {
                    glkTerminators = GlkKeysFromZSCII(terminatingKeys);
                    Glk.garglk_set_line_terminators(currentWin, glkTerminators, (uint)glkTerminators.Length);
                }

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
                                if (glkTerminators == null || ev.val2 == 0)
                                    terminator = 13;
                                else
                                    terminator = GlkKeyToZSCII((KeyCode)ev.val2);
                            }
                            break;

                        case EvType.Timer:
                            lineInputActive = true;
                            if (callback() == true)
                            {
                                done = true;
                            }
                            else if (!lineInputActive)
                            {
                                // the callback cancelled the line input request to print something...
                                if (unicode)
                                    Glk.glk_request_line_event_uni(currentWin, buf, BUFSIZE, canceledLineEvent.val1);
                                else
                                    Glk.glk_request_line_event(currentWin, buf, BUFSIZE, canceledLineEvent.val1);
                                if (glkTerminators != null)
                                    Glk.garglk_set_line_terminators(currentWin, glkTerminators, (uint)glkTerminators.Length);
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
                PerformSplit(targetSplit);

                // convert the string from Latin-1 or UTF-32
                int length = (int)ev.val1;
                if (unicode)
                    length *= 4;
                byte[] bytes = new byte[length];
                Marshal.Copy(buf, bytes, 0, length);
                return encoding.GetString(bytes);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }

        short IZMachineIO.ReadKey(int time, TimedInputCallback callback, CharTranslator translator)
        {
            PerformSplit(targetSplit);

            if (unicode)
                Glk.glk_request_char_event_uni(currentWin);
            else
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
                            if (ev.val1 <= 255 || (unicode && ev.val1 <= 0x10000))
                                result = translator((char)ev.val1);
                            else
                                result = GlkKeyToZSCII((KeyCode)ev.val1);

                            if (result != 0)
                                done = true;
                            else if (unicode)
                                Glk.glk_request_char_event_uni(currentWin);
                            else
                                Glk.glk_request_char_event(currentWin);
                        }
                        break;

                    case EvType.Timer:
                        if (callback() == true)
                        {
                            Glk.glk_cancel_char_event(currentWin);
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

            return result;
        }

        private static byte GlkKeyToZSCII(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Delete: return 8;
                case KeyCode.Return: return 13;
                case KeyCode.Escape: return 27;

                case KeyCode.Up: return 129;
                case KeyCode.Down: return 130;
                case KeyCode.Left: return 131;
                case KeyCode.Right: return 132;
                case KeyCode.Func1: return 133;
                case KeyCode.Func2: return 134;
                case KeyCode.Func3: return 135;
                case KeyCode.Func4: return 136;
                case KeyCode.Func5: return 137;
                case KeyCode.Func6: return 138;
                case KeyCode.Func7: return 139;
                case KeyCode.Func8: return 140;
                case KeyCode.Func9: return 141;
                case KeyCode.Func10: return 142;
                case KeyCode.Func11: return 143;
                case KeyCode.Func12: return 144;
                default: return 0;
            }
        }

        private static KeyCode[] GlkKeysFromZSCII(byte[] zkeys)
        {
            // 255 means every input-only key above 128, and ZLR ensures that it's the only key in the array if present
            if (zkeys[0] == 255)
                return new KeyCode[] {
                    KeyCode.Up, KeyCode.Down, KeyCode.Left, KeyCode.Right,
                    KeyCode.Func1, KeyCode.Func2, KeyCode.Func3, KeyCode.Func4,
                    KeyCode.Func5, KeyCode.Func6, KeyCode.Func7, KeyCode.Func8,
                    KeyCode.Func9, KeyCode.Func10, KeyCode.Func11, KeyCode.Func12,
                };

            List<KeyCode> result = new List<KeyCode>(zkeys.Length);
            foreach (byte zk in zkeys)
            {
                switch (zk)
                {
                    // keys <= 128 aren't valid terminating keys, but just for completeness...
                    case 8: result.Add(KeyCode.Delete); break;
                    case 13: result.Add(KeyCode.Return); break;
                    case 27: result.Add(KeyCode.Escape); break;

                    case 129: result.Add(KeyCode.Up); break;
                    case 130: result.Add(KeyCode.Down); break;
                    case 131: result.Add(KeyCode.Left); break;
                    case 132: result.Add(KeyCode.Right); break;
                    case 133: result.Add(KeyCode.Func1); break;
                    case 134: result.Add(KeyCode.Func2); break;
                    case 135: result.Add(KeyCode.Func3); break;
                    case 136: result.Add(KeyCode.Func4); break;
                    case 137: result.Add(KeyCode.Func5); break;
                    case 138: result.Add(KeyCode.Func6); break;
                    case 139: result.Add(KeyCode.Func7); break;
                    case 140: result.Add(KeyCode.Func8); break;
                    case 141: result.Add(KeyCode.Func9); break;
                    case 142: result.Add(KeyCode.Func10); break;
                    case 143: result.Add(KeyCode.Func11); break;
                    case 144: result.Add(KeyCode.Func12); break;
                    // 145-154: num pad keys, not supported by Glk
                    // 254: mouse click
                }
            }
            return result.ToArray();
        }

        private readonly char[] encodingChar = new char[1];
        private readonly byte[] encodedBytes = new byte[2];

        void IZMachineIO.PutChar(char ch)
        {
            if (lineInputActive)
            {
                Glk.glk_cancel_line_event(currentWin, out canceledLineEvent);
                lineInputActive = false;
                // glk_cancel_line_event prints a newline
                if (ch == '\n')
                    return;
            }

            if (unicode)
            {
                Glk.glk_put_char_uni((uint)ch);
            }
            else
            {
                byte b;
                encodingChar[0] = ch;
                int result = Encoding.GetEncoding(Glk.LATIN1).GetBytes(encodingChar, 0, 1, encodedBytes, 0);
                if (result != 1)
                    b = (byte)'?';
                else
                    b = encodedBytes[0];
                Glk.glk_put_char(b);
            }

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
            if (lineInputActive)
            {
                Glk.glk_cancel_line_event(currentWin, out canceledLineEvent);
                lineInputActive = false;
                // glk_cancel_line_event prints a newline
                if (str.Length > 0 && str[0] == '\n')
                    str = str.Substring(1);
            }

            if (unicode)
                Glk.glk_put_string_uni(str);
            else
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
            if (lineInputActive)
            {
                Glk.glk_cancel_line_event(currentWin, out canceledLineEvent);
                lineInputActive = false;
            }

            if (currentWin == lowerWin)
            {
                foreach (string str in lines)
                {
                    if (unicode)
                        Glk.glk_put_string_uni(str);
                    else
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
                    if (unicode)
                        Glk.glk_put_string_uni(str);
                    else
                        Glk.glk_put_string(str);

                    ypos++;
                    if (ypos >= screenHeight)
                        ypos = (int)screenHeight - 1;

                    xpos = oxpos + str.Length;
                    if (xpos >= screenWidth)
                        xpos = (int)screenWidth - 1;
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

        Stream IZMachineIO.OpenCommandFile(bool writing)
        {
            FileMode mode = writing ? FileMode.Write : FileMode.Read;
            frefid_t file = Glk.glk_fileref_create_by_prompt(
                FileUsage.InputRecord | FileUsage.TextMode, mode, 0);
            return OpenStream(file, mode);
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
            uint curHeight;

            if (upperWin.IsNull)
            {
                curHeight = 0;
            }
            else
            {
                WinMethod method;
                winid_t keywin;
                Glk.glk_window_get_arrangement(Glk.glk_window_get_parent(upperWin),
                    out method, out curHeight, out keywin);
            }

            targetSplit = lines;
            if (lines > curHeight)
                PerformSplit(lines);
        }

        private void PerformSplit(int lines)
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
            // basic support for the normal font
            if (num == 1)
                return 1;

            // no font changes supported
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
            if (Glk.glk_gestalt(Gestalt.CharOutput, ch) != 0)
                result |= UnicodeCaps.CanPrint;
            if (Glk.glk_gestalt(Gestalt.CharInput, ch) != 0)
                result |= UnicodeCaps.CanInput;
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
