using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace ZLR.Interfaces.Demona
{
#pragma warning disable 649
    // opaque structure definitions (these are actually pointers to structs defined by the Glk library)
    internal struct winid_t
    {
        int value;
        public bool IsNull { get { return value == 0; } }
        public static readonly winid_t Null = new winid_t();
        public static bool operator ==(winid_t a, winid_t b) { return a.value == b.value; }
        public static bool operator !=(winid_t a, winid_t b) { return a.value != b.value; }
        public override bool Equals(object obj)
        {
            if (obj is winid_t)
                return (winid_t)obj == this;
            else
                return false;
        }
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
    }

    internal struct strid_t
    {
        int value;
        public bool IsNull { get { return value == 0; } }
        public static readonly strid_t Null = new strid_t();
    }

    internal struct frefid_t
    {
        int value;
        public bool IsNull { get { return value == 0; } }
        public static readonly frefid_t Null = new frefid_t();
    }

    internal struct schanid_t
    {
        int value;
        public bool IsNull { get { return value == 0; } }
        public static readonly schanid_t Null = new schanid_t();
    }

    // non-opaque structures
    internal struct event_t
    {
        public EvType type;
        public winid_t win;
        public uint val1, val2;
    }

    internal struct stream_result_t
    {
        public uint readcount;
        public uint writecount;
    }
#pragma warning restore

    // gestalt_* constants
    internal enum Gestalt
    {
        Version = 0,
        CharInput = 1,
        LineInput = 2,
        CharOutput = 3,
        CannotPrint = 0,
        ApproxPrint = 1,
        ExactPrint = 2,
        MouseInput = 4,
        Timer = 5,
        Graphics = 6,
        DrawImage = 7,
        Sound = 8,
        SoundVolume = 9,
        SoundNotify = 10,
        Hyperlinks = 11,
        HyperlinkInput = 12,
        SoundMusic = 13,
        GraphicsTransparency = 14,
    }

    // evtype_* constants
    internal enum EvType
    {
        None = 0,
        Timer = 1,
        CharInput = 2,
        LineInput = 3,
        MouseInput = 4,
        Arrange = 5,
        Redraw = 6,
        SoundNotify = 7,
        Hyperlink = 8,
    }

    // keycode_* constants
    internal enum KeyCode : uint
    {
        Unknown = 0xffffffff,
        Left = 0xfffffffe,
        Right = 0xfffffffd,
        Up = 0xfffffffc,
        Down = 0xfffffffb,
        Return = 0xfffffffa,
        Delete = 0xfffffff9,
        Escape = 0xfffffff8,
        Tab = 0xfffffff7,
        PageUp = 0xfffffff6,
        PageDown = 0xfffffff5,
        Home = 0xfffffff4,
        End = 0xfffffff3,
        Func1 = 0xffffffef,
        Func2 = 0xffffffee,
        Func3 = 0xffffffed,
        Func4 = 0xffffffec,
        Func5 = 0xffffffeb,
        Func6 = 0xffffffea,
        Func7 = 0xffffffe9,
        Func8 = 0xffffffe8,
        Func9 = 0xffffffe7,
        Func10 = 0xffffffe6,
        Func11 = 0xffffffe5,
        Func12 = 0xffffffe4,
        MAXVAL = 28,
    }

    // style_* constants
    internal enum Style
    {
        Normal = 0,
        Emphasized = 1,
        Preformatted = 2,
        Header = 3,
        Subheader = 4,
        Alert = 5,
        Note = 6,
        BlockQuote = 7,
        Input = 8,
        User1 = 9,
        User2 = 10,
        NUMSTYLES = 11,
    }

    // wintype_* constants
    internal enum WinType
    {
        AllTypes = 0,
        Pair = 1,
        Blank = 2,
        TextBuffer = 3,
        TextGrid = 4,
        Graphics = 5,
    }

    // winmethod_* constants
    internal enum WinMethod
    {
        Left = 0x00,
        Right = 0x01,
        Above = 0x02,
        Below = 0x03,
        DirMask = 0x0f,

        Fixed = 0x10,
        Proportional = 0x20,
        DivisionMask = 0xf0,
    }

    // fileusage_* constants
    internal enum FileUsage
    {
        Data = 0x00,
        SavedGame = 0x01,
        Transcript = 0x02,
        InputRecord = 0x03,
        TypeMask = 0x0f,

        TextMode = 0x100,
        BinaryMode = 0x000,
    }

    // filemode_* constants
    internal enum FileMode
    {
        Write = 0x01,
        Read = 0x02,
        ReadWrite = 0x03,
        WriteAppend = 0x05,
    }

    // seekmode_* constants
    internal enum SeekMode
    {
        Start = 0,
        Current = 1,
        End = 2,
    }

    // stylehint_* constants
    internal enum StyleHint
    {
        Indentation = 0,
        ParaIndentation = 1,
        Justification = 2,
        Size = 3,
        Weight = 4,
        Oblique = 5,
        Proportional = 6,
        TextColor = 7,
        BackColor = 8,
        ReverseColor = 9,
        NUMHINTS = 10,

        LeftFlush = 0,
        LeftRight = 1,
        Centered = 2,
        RightFlush = 3,
    }

    // giblorb_err_* constants
    internal enum BlorbError
    {
        None = 0,
        CompileTime = 1,
        Alloc = 2,
        Read = 3,
        NotAMap = 4,
        Format = 5,
        NotFound = 6,
    }

    // import the Glk functions from a DLL
    internal static class Glk
    {
        private const string GLKDLL = "libgarglk.dll";

        public const int LATIN1 = 28591; // code page number

        #region Gargoyle Specific

        // internal function: we call this directly because we aren't using the glk_main() idiom.
        [DllImport(GLKDLL)]
        public extern static void gli_startup(int argc, IntPtr argv);

        [DllImport(GLKDLL)]
        public extern static void garglk_set_program_name(string name);

        [DllImport(GLKDLL)]
        public extern static void garglk_set_program_info(string info);

        [DllImport(GLKDLL)]
        public extern static void garglk_set_story_name(string name);

        [DllImport(GLKDLL)]
        public extern static void garglk_set_config(string name);

        #endregion

        #region Standard GLK Functions

        [DllImport(GLKDLL)]
        public extern static void glk_exit();
        [DllImport(GLKDLL)]
        public extern static void glk_tick();

        [DllImport(GLKDLL)]
        public static extern uint glk_gestalt(Gestalt sel, uint val);
        [DllImport(GLKDLL)]
        public static extern uint glk_gestalt_ext(Gestalt sel, uint val, [Out] int[] arr,
            uint arrlen);

        [DllImport(GLKDLL)]
        public static extern winid_t glk_window_get_root();
        [DllImport(GLKDLL)]
        public static extern winid_t glk_window_open(winid_t split, WinMethod method, uint size,
            WinType wintype, uint rock);
        [DllImport(GLKDLL)]
        public static extern void glk_window_close(winid_t win, out stream_result_t result);
        [DllImport(GLKDLL)]
        public static extern void glk_window_get_size(winid_t win, out uint width,
            out uint height);
        [DllImport(GLKDLL)]
        public static extern void glk_window_set_arrangement(winid_t win, WinMethod method,
            uint size, winid_t keywin);
        [DllImport(GLKDLL)]
        public static extern void glk_window_get_arrangement(winid_t win, out WinMethod method,
            out uint sizeptr, out winid_t keywinptr);
        [DllImport(GLKDLL)]
        public static extern winid_t glk_window_iterate(winid_t win, out uint rockptr);
        [DllImport(GLKDLL)]
        public static extern uint glk_window_get_rock(winid_t win);
        [DllImport(GLKDLL)]
        public static extern WinType glk_window_get_type(winid_t win);
        [DllImport(GLKDLL)]
        public static extern winid_t glk_window_get_parent(winid_t win);
        [DllImport(GLKDLL)]
        public static extern winid_t glk_window_get_sibling(winid_t win);
        [DllImport(GLKDLL)]
        public static extern void glk_window_clear(winid_t win);
        [DllImport(GLKDLL)]
        public static extern void glk_window_move_cursor(winid_t win, uint xpos, uint ypos);

        [DllImport(GLKDLL)]
        public static extern strid_t glk_window_get_stream(winid_t win);
        [DllImport(GLKDLL)]
        public static extern void glk_window_set_echo_stream(winid_t win, strid_t str);
        [DllImport(GLKDLL)]
        public static extern strid_t glk_window_get_echo_stream(winid_t win);
        [DllImport(GLKDLL)]
        public static extern void glk_set_window(winid_t win);

        [DllImport(GLKDLL)]
        public static extern strid_t glk_stream_open_file(frefid_t fileref, FileMode fmode,
            uint rock);
        [DllImport(GLKDLL)]
        public static extern strid_t glk_stream_open_memory(IntPtr buf, uint buflen, FileMode fmode,
            uint rock);
        [DllImport(GLKDLL)]
        public static extern void glk_stream_close(strid_t str, out stream_result_t result);
        [DllImport(GLKDLL)]
        public static extern strid_t glk_stream_iterate(strid_t str, out uint rockptr);
        [DllImport(GLKDLL)]
        public static extern uint glk_stream_get_rock(strid_t str);
        [DllImport(GLKDLL)]
        public static extern void glk_stream_set_position(strid_t str, int pos, SeekMode seekmode);
        [DllImport(GLKDLL)]
        public static extern uint glk_stream_get_position(strid_t str);
        [DllImport(GLKDLL)]
        public static extern void glk_stream_set_current(strid_t str);
        [DllImport(GLKDLL)]
        public static extern strid_t glk_stream_get_current();

        [DllImport(GLKDLL)]
        public static extern void glk_put_char(byte ch);
        [DllImport(GLKDLL)]
        public static extern void glk_put_char_stream(strid_t str, byte ch);
        [DllImport(GLKDLL)]
        private static extern void glk_put_string(IntPtr s);
        public static void glk_put_string(string s)
        {
            IntPtr buf = StrToLatin1(s);
            try { glk_put_string(buf); }
            finally { Marshal.FreeHGlobal(buf); }
        }
        [DllImport(GLKDLL)]
        private static extern void glk_put_string_stream(strid_t str, IntPtr s);
        public static void glk_put_string_stream(strid_t str, string s)
        {
            IntPtr buf = StrToLatin1(s);
            try { glk_put_string_stream(str, buf); }
            finally { Marshal.FreeHGlobal(buf); }
        }
        [DllImport(GLKDLL)]
        public static extern void glk_put_buffer(byte[] buf, uint len);
        [DllImport(GLKDLL)]
        public static extern void glk_put_buffer_stream(strid_t str, [In] byte[] buf, uint len);
        [DllImport(GLKDLL)]
        public static extern void glk_set_style(Style styl);
        [DllImport(GLKDLL)]
        public static extern void glk_set_style_stream(strid_t str, Style styl);

        [DllImport(GLKDLL)]
        public static extern int glk_get_char_stream(strid_t str);
        [DllImport(GLKDLL)]
        private static extern uint glk_get_line_stream(strid_t str, IntPtr buf, uint len);
        public static uint glk_get_line_stream(strid_t str, StringBuilder sb)
        {
            int len = sb.Capacity;
            IntPtr buf = Marshal.AllocHGlobal(len);
            try
            {
                uint result = glk_get_line_stream(str, buf, (uint)len);
                StrFromLatin1(buf, sb);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        [DllImport(GLKDLL)]
        public static extern uint glk_get_buffer_stream(strid_t str, [Out] byte[] buf, uint len);

        [DllImport(GLKDLL)]
        public static extern void glk_stylehint_set(WinType wintype, Style styl, StyleHint hint,
            int val);
        [DllImport(GLKDLL)]
        public static extern void glk_stylehint_clear(WinType wintype, Style styl, StyleHint hint);
        [DllImport(GLKDLL)]
        public static extern bool glk_style_distinguish(winid_t win, Style styl1, Style styl2);
        [DllImport(GLKDLL)]
        public static extern bool glk_style_measure(winid_t win, Style styl, StyleHint hint,
            out int result);

        [DllImport(GLKDLL)]
        public static extern frefid_t glk_fileref_create_temp(FileUsage usage, uint rock);
        [DllImport(GLKDLL)]
        private static extern frefid_t glk_fileref_create_by_name(FileUsage usage, IntPtr name, uint rock);
        public static frefid_t glk_fileref_create_by_name(FileUsage usage, string name, uint rock)
        {
            IntPtr buf = StrToLatin1(name);
            try { return glk_fileref_create_by_name(usage, buf, rock); }
            finally { Marshal.FreeHGlobal(buf); }
        }
        [DllImport(GLKDLL)]
        public static extern frefid_t glk_fileref_create_by_prompt(FileUsage usage, FileMode fmode,
            uint rock);
        [DllImport(GLKDLL)]
        public static extern frefid_t glk_fileref_create_from_fileref(FileUsage usage, frefid_t fref,
            uint rock);
        [DllImport(GLKDLL)]
        public static extern void glk_fileref_destroy(frefid_t fref);
        [DllImport(GLKDLL)]
        public static extern frefid_t glk_fileref_iterate(frefid_t fref, out uint rockptr);
        [DllImport(GLKDLL)]
        public static extern uint glk_fileref_get_rock(frefid_t fref);
        [DllImport(GLKDLL)]
        public static extern void glk_fileref_delete_file(frefid_t fref);
        [DllImport(GLKDLL)]
        public static extern bool glk_fileref_does_file_exist(frefid_t fref);

        [DllImport(GLKDLL)]
        public static extern void glk_select(out event_t @event);
        [DllImport(GLKDLL)]
        public static extern void glk_select_poll(out event_t @event);

        [DllImport(GLKDLL)]
        public static extern void glk_request_timer_events(uint millisecs);

        [DllImport(GLKDLL)]
        public static extern void glk_request_line_event(winid_t win, IntPtr buf, uint maxlen,
            uint initlen);
        [DllImport(GLKDLL)]
        public static extern void glk_request_char_event(winid_t win);
        [DllImport(GLKDLL)]
        public static extern void glk_request_mouse_event(winid_t win);

        [DllImport(GLKDLL)]
        public static extern void glk_cancel_line_event(winid_t win, out event_t @event);
        [DllImport(GLKDLL)]
        public static extern void glk_cancel_char_event(winid_t win);
        [DllImport(GLKDLL)]
        public static extern void glk_cancel_mouse_event(winid_t win);

        [DllImport(GLKDLL)]
        public static extern schanid_t glk_schannel_create(uint rock);
        [DllImport(GLKDLL)]
        public static extern void glk_schannel_destroy(schanid_t chan);
        [DllImport(GLKDLL)]
        public static extern schanid_t glk_schannel_iterate(schanid_t chan, out uint rockptr);
        [DllImport(GLKDLL)]
        public static extern uint glk_schannel_get_rock(schanid_t chan);

        [DllImport(GLKDLL)]
        public static extern uint glk_schannel_play(schanid_t chan, uint snd);
        [DllImport(GLKDLL)]
        public static extern uint glk_schannel_play_ext(schanid_t chan, uint snd, uint repeats,
            bool notify);
        [DllImport(GLKDLL)]
        public static extern void glk_schannel_stop(schanid_t chan);
        [DllImport(GLKDLL)]
        public static extern void glk_schannel_set_volume(schanid_t chan, uint vol);

        [DllImport(GLKDLL)]
        public static extern void glk_sound_load_hint(uint snd, bool flag);

        [DllImport(GLKDLL)]
        public static extern BlorbError giblorb_set_resource_map(strid_t file);

        #endregion

        public static IntPtr StrToLatin1(string s)
        {
            byte[] bytes = Encoding.GetEncoding(LATIN1).GetBytes(s);
            IntPtr result = Marshal.AllocHGlobal(bytes.Length + 1);
            if (result == IntPtr.Zero)
                throw new Exception("Can't allocate unmanaged memory in StrToLatin1");

            Marshal.Copy(bytes, 0, result, bytes.Length);
            Marshal.WriteByte(result, bytes.Length, 0);
            return result;
        }

        public static void StrFromLatin1(IntPtr buf, StringBuilder sb)
        {
            int len = 0;
            while (Marshal.ReadByte(buf, len) != 0)
                len++;

            byte[] bytes = new byte[len];
            Marshal.Copy(buf, bytes, 0, len);
            string str = Encoding.GetEncoding(LATIN1).GetString(bytes);

            sb.Length = 0;
            sb.Append(str);
        }
    }
}
