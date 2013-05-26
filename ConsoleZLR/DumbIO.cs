using System;
using System.Collections.Generic;
using System.Text;
using ZLR.VM;

namespace ZLR.Interfaces.SystemConsole
{
    class DumbIO : IZMachineIO
    {
        private readonly bool bottomWinOnly;
        private short curWin = 0;

        public DumbIO()
            : this(false)
        {
        }

        public DumbIO(bool bottomWinOnly)
        {
            this.bottomWinOnly = bottomWinOnly;
        }

        public string ReadLine(string initial, int time, TimedInputCallback callback,
            byte[] terminatingKeys, out byte terminator)
        {
            terminator = 13;
            return Console.ReadLine() ?? "";
        }

        public short ReadKey(int time, TimedInputCallback callback, CharTranslator translator)
        {
            short ch;
            do
            {
                ConsoleKeyInfo info = Console.ReadKey();
                ch = translator(info.KeyChar);
            } while (ch == 0);
            return ch;
        }

        public void PutCommand(string command)
        {
            // nada
        }

        public void PutChar(char ch)
        {
            if (!bottomWinOnly || curWin == 0)
                Console.Write(ch);
        }

        public void PutString(string str)
        {
            if (!bottomWinOnly || curWin == 0) 
                Console.Write(str);
        }

        public void PutTextRectangle(string[] lines)
        {
            if (!bottomWinOnly || curWin == 0) 
                foreach (string str in lines)
                    Console.WriteLine(str);
        }

        public bool Buffering
        {
            get { return false; }
            set { /* nada */ }
        }

        public bool Transcripting
        {
            get { return false; }
            set { /* nada */ }
        }

        public void PutTranscriptChar(char ch)
        {
            // not implemented
        }

        public void PutTranscriptString(string str)
        {
            // not implemented
        }

        public System.IO.Stream OpenSaveFile(int size)
        {
            // not implemented
            return null;
        }

        public System.IO.Stream OpenRestoreFile()
        {
            // not implemented
            return null;
        }

        public System.IO.Stream OpenAuxiliaryFile(string name, int size, bool writing)
        {
            // not implemented
            return null;
        }

        public System.IO.Stream OpenCommandFile(bool writing)
        {
            // not implemented
            return null;
        }

        public void SetTextStyle(TextStyle style)
        {
            // nada
        }

        public void SplitWindow(short lines)
        {
            // nada
        }

        public void SelectWindow(short num)
        {
            curWin = num;
        }

        public void EraseWindow(short num)
        {
            // nada
        }

        public void EraseLine()
        {
            // nada
        }

        public void MoveCursor(short x, short y)
        {
            // nada
        }

        public void GetCursorPos(out short x, out short y)
        {
            x = 1;
            y = 1;
        }

        public void SetColors(short fg, short bg)
        {
            // nada
        }

        public short SetFont(short num)
        {
            return 0;
        }

        public void PlaySoundSample(ushort number, SoundAction action, byte volume, byte repeats, SoundFinishedCallback callback)
        {
            // nada
        }

        public void PlayBeep(bool highPitch)
        {
            // nada
        }

        public bool ForceFixedPitch
        {
            get { return false; }
            set { /* nada */ }
        }

        public bool BoldAvailable
        {
            get { return false; }
        }

        public bool ItalicAvailable
        {
            get { return false; }
        }

        public bool FixedPitchAvailable
        {
            get { return false; }
        }

        public bool VariablePitchAvailable
        {
            get { return false; }
        }

        public bool ScrollFromBottom
        {
            get { return false; }
            set { /* nada */ }
        }

        public bool GraphicsFontAvailable
        {
            get { return false; }
        }

        public bool TimedInputAvailable
        {
            get { return false; }
        }

        public bool SoundSamplesAvailable
        {
            get { return false; }
        }

        public byte WidthChars
        {
            get { return 80; }
        }

        public short WidthUnits
        {
            get { return 80; }
        }

        public byte HeightChars
        {
            get { return 25; }
        }

        public short HeightUnits
        {
            get { return 25; }
        }

        public byte FontHeight
        {
            get { return 1; }
        }

        public byte FontWidth
        {
            get { return 1; }
        }

        public event EventHandler SizeChanged
        {
            add { /* nada */ }
            remove { /* nada */ }
        }

        public bool ColorsAvailable
        {
            get { return false; }
        }

        public byte DefaultForeground
        {
            get { return 9; }
        }

        public byte DefaultBackground
        {
            get { return 2; }
        }

        public UnicodeCaps CheckUnicode(char ch)
        {
            return UnicodeCaps.CanInput | UnicodeCaps.CanPrint;
        }

        public bool DrawCustomStatusLine(string location, short hoursOrScore, short minsOrTurns, bool useTime)
        {
            return false;
        }
    }
}
