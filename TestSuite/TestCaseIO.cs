using System;
using System.Collections.Generic;
using System.Text;
using ZLR.VM;
using System.IO;

namespace TestSuite
{
    abstract class TestCaseIO : IZMachineIO
    {
        protected readonly Queue<string> inputBuffer = new Queue<string>(); 
        protected readonly StringBuilder outputBuffer = new StringBuilder();

        public string CollectOutput()
        {
            string result = outputBuffer.ToString();
            outputBuffer.Length = 0;
            return result;
        }

        #region IZMachineIO Members

        public abstract string ReadLine(string initial, int time, TimedInputCallback callback, byte[] terminatingKeys, out byte terminator);

        public abstract short ReadKey(int time, TimedInputCallback callback, CharTranslator translator);

        public void PutCommand(string command)
        {
            // nada
        }

        public abstract void PutChar(char ch);

        public abstract void PutString(string str);

        public abstract void PutTextRectangle(string[] lines);

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
            // nada
        }

        public void PutTranscriptString(string str)
        {
            // nada
        }

        public Stream OpenSaveFile(int size)
        {
            return null;
        }

        public Stream OpenRestoreFile()
        {
            return null;
        }

        public Stream OpenAuxiliaryFile(string name, int size, bool writing)
        {
            return null;
        }

        public abstract Stream OpenCommandFile(bool writing);

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
            // nada
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
            // not supported
            return 0;
        }

        public bool DrawCustomStatusLine(string location, short hoursOrScore, short minsOrTurns, bool useTime)
        {
            return false;
        }

        public void PlaySoundSample(ushort number, SoundAction action, byte volume, byte repeats, SoundFinishedCallback callback)
        {
            // nada
        }

        public virtual void PlayBeep(bool highPitch)
        {
            // nada
        }

        public bool ForceFixedPitch
        {
            get { return true; }
            set { /* nada */ }
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
            return UnicodeCaps.CanPrint | UnicodeCaps.CanInput;
        }

        #endregion
    }

    class ReplayIO : TestCaseIO
    {
        private readonly string inputFile;

        public ReplayIO(string prevInputFile)
        {
            this.inputFile = prevInputFile;
        }

        public override string ReadLine(string initial, int time, TimedInputCallback callback, byte[] terminatingKeys, out byte terminator)
        {
            terminator = 13;
            return inputBuffer.Dequeue();
        }

        public override short ReadKey(int time, TimedInputCallback callback, CharTranslator translator)
        {
            string inputLine;
            do { inputLine = inputBuffer.Dequeue(); } while (inputLine.Length == 0);
            return translator(inputLine[0]);
        }

        public override void PutChar(char ch)
        {
            outputBuffer.Append(ch);
        }

        public override void PutString(string str)
        {
            outputBuffer.Append(str);
        }

        public override void PutTextRectangle(string[] lines)
        {
            foreach (string line in lines)
                outputBuffer.AppendLine(line);
        }

        public override Stream OpenCommandFile(bool writing)
        {
            if (writing)
                return null;

            return new FileStream(inputFile, FileMode.Open, FileAccess.Read);
        }
    }

    class RecordingIO : TestCaseIO
    {
        private readonly string inputFile;

        public RecordingIO(string newInputFile)
        {
            this.inputFile = newInputFile;
        }

        public override string ReadLine(string initial, int time, TimedInputCallback callback, byte[] terminatingKeys, out byte terminator)
        {
            terminator = 13;
            return Console.ReadLine();
        }

        public override short ReadKey(int time, TimedInputCallback callback, CharTranslator translator)
        {
            ConsoleKeyInfo info = Console.ReadKey(true);
            return translator(info.KeyChar);
        }

        public override void PutChar(char ch)
        {
            Console.Write(ch);
            outputBuffer.Append(ch);
        }

        public override void PutString(string str)
        {
            Console.Write(str);
            outputBuffer.Append(str);
        }

        public override void PutTextRectangle(string[] lines)
        {
            foreach (string str in lines)
            {
                Console.WriteLine(str);
                outputBuffer.AppendLine(str);
            }
        }

        public override Stream OpenCommandFile(bool writing)
        {
            if (!writing)
                return null;

            return new FileStream(inputFile, FileMode.Create, FileAccess.Write);
        }

        public override void PlayBeep(bool highPitch)
        {
            Console.Beep(highPitch ? 1600 : 800, 200);
        }
    }
}
