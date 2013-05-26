using System;
using System.Collections.Generic;
using System.Text;

namespace ZLR.VM.Debugging
{
    public enum DebuggerState
    {
        Stopped,
        Paused,
        Running,
    }

    public interface IDebugger
    {
        DebuggerState State { get; }

        void Restart();

        void StepInto();
        void StepOver();
        void StepUp();

        void Run();
        void SetBreakpoint(int address, bool enabled);
        int[] GetBreakpoints();

        short Call(short packedAddress, short[] args);

        byte ReadByte(int address);
        short ReadWord(int address);
        void WriteByte(int address, byte value);
        void WriteWord(int address, short value);

        int CallDepth { get; }
        ICallFrame[] GetCallFrames();
        int CurrentPC { get; }
        string Disassemble(int address);

        int StackDepth { get; }
        void StackPush(short value);
        short StackPop();

        int UnpackAddress(short packedAddress, bool forString);
        short PackAddress(int address, bool forString);
    }

    public interface ICallFrame
    {
        int ReturnPC { get; }
        int PrevStackDepth { get; }
        short[] Locals { get; }
        int ArgCount { get; }
        int ResultStorage { get; }
    }
}

namespace ZLR.VM
{
    using Debugging;

    partial class ZMachine
    {
        private int stepping = -1;
        private Dictionary<int, bool> breakpoints = new Dictionary<int, bool>();
        private DebuggerState debugState;

        public IDebugger Debug()
        {
            debugging = true;
            if (cache != null)
                cache.Clear();

            return new Debugger(this);
        }

#pragma warning disable 0169
        private bool DebugCheck(int pc)
        {
            if (stepping >= 0)
            {
                if (--stepping < 0)
                {
                    this.pc = pc;
                    return true;
                }
            }
            else if (breakpoints.ContainsKey(pc))
            {
                this.pc = pc;
                this.debugState = DebuggerState.Paused;
                return true;
            }

            // continue
            return false;
        }
#pragma warning restore 0169

        private class Debugger : IDebugger
        {
            private readonly ZMachine zm;

            public Debugger(ZMachine zm)
            {
                this.zm = zm;
            }

            #region IDebugger Members

            public DebuggerState State
            {
                get { return zm.debugState; }
            }

            public void Restart()
            {
                zm.Restart();
                if (zm.cache == null)
                    zm.cache = new LruCache<int, CachedCode>(zm.cacheSize);
                zm.debugState = DebuggerState.Paused;
            }

            private void OneStep()
            {
                CachedCode entry;
                int thisPC = zm.pc;
                if (thisPC < zm.romStart || zm.cache.TryGetValue(thisPC, out entry) == false)
                {
                    int count;
                    entry = new CachedCode(zm.pc, zm.CompileZCode(out count));
                    if (thisPC >= zm.romStart)
                        zm.cache.Add(thisPC, entry, count);
                }
                zm.pc = entry.NextPC;
                entry.Code();
            }

            public void StepInto()
            {
                zm.stepping = 1;
                zm.running = true;

                OneStep();

                zm.stepping = -1;
                zm.debugState = zm.running ? DebuggerState.Paused : DebuggerState.Stopped;
            }

            public void StepOver()
            {
                int callDepth = zm.callStack.Count;
                StepInto();

                while (zm.callStack.Count > callDepth)
                    StepInto();
            }

            public void StepUp()
            {
                int callDepth = zm.callStack.Count;
                StepInto();

                while (zm.callStack.Count >= callDepth)
                    StepInto();
            }

            public void Run()
            {
                // step ahead if the current line has a breakpoint on it
                if (zm.breakpoints.ContainsKey(zm.pc))
                    StepInto();

                zm.running = true;
                zm.debugState = DebuggerState.Running;
                while (zm.running && zm.debugState == DebuggerState.Running)
                    OneStep();

                zm.debugState = zm.running ? DebuggerState.Paused : DebuggerState.Stopped;
            }

            public void SetBreakpoint(int address, bool enabled)
            {
                if (enabled)
                    zm.breakpoints[address] = true;
                else
                    zm.breakpoints.Remove(address);
            }

            public int[] GetBreakpoints()
            {
                return new List<int>(zm.breakpoints.Keys).ToArray();
            }

            public short Call(short packedAddress, short[] args)
            {
                zm.EnterFunctionImpl(packedAddress, args, 0, zm.pc);
                zm.JitLoop();
                return zm.stack.Pop();
            }

            public byte ReadByte(int address)
            {
                return zm.zmem[address];
            }

            public short ReadWord(int address)
            {
                return (short)((zm.zmem[address] << 8) | zm.zmem[address + 1]);
            }

            public void WriteByte(int address, byte value)
            {
                zm.zmem[address] = value;
            }

            public void WriteWord(int address, short value)
            {
                zm.zmem[address] = (byte)(value >> 8);
                zm.zmem[address + 1] = (byte)value;
            }

            public int CallDepth
            {
                get { return zm.callStack.Count; }
            }

            public ICallFrame[] GetCallFrames()
            {
                return zm.callStack.ToArray();
            }

            public int CurrentPC
            {
                get { return zm.pc; }
            }

            public string Disassemble(int address)
            {
                int opc = zm.pc;
                try
                {
                    zm.pc = address;
                    OperandType[] types = new OperandType[8];
                    short[] argv = new short[8];

                    Opcode opcode = zm.DecodeOneOp(types, argv);

                    RoutineInfo rtn;
                    if (zm.debugFile == null)
                        rtn = null;
                    else
                        rtn = zm.debugFile.FindRoutine(address);

                    return opcode.Disassemble(delegate(byte varnum)
                    {
                        if (rtn != null && varnum - 1 < rtn.Locals.Length)
                            return "local_" + varnum + "(" + rtn.Locals[varnum - 1] + ")";
                        else if (varnum < 16)
                            return "local_" + varnum;
                        else if (zm.debugFile != null && zm.debugFile.Globals.Contains(varnum))
                            return "global_" + varnum + "(" + zm.debugFile.Globals[varnum] + ")";
                        else
                            return "global_" + varnum;
                    });

                }
                finally
                {
                    zm.pc = opc;
                }
            }

            public int StackDepth
            {
                get { return zm.stack.Count; }
            }

            public void StackPush(short value)
            {
                zm.stack.Push(value);
            }

            public short StackPop()
            {
                return zm.stack.Pop();
            }

            public int UnpackAddress(short packedAddress, bool forString)
            {
                return zm.UnpackAddress(packedAddress, forString);
            }

            public short PackAddress(int address, bool forString)
            {
                switch (zm.zversion)
                {
                    case 1:
                    case 2:
                    case 3:
                        return (short) (address/2);

                    case 4:
                    case 5:
                        return (short) (address/4);

                    case 6:
                    case 7:
                        const int HDR_CODE_OFFSET = 0x28;
                        const int HDR_STR_OFFSET = 0x2A;
                        var offset = ReadWord(forString ? HDR_STR_OFFSET : HDR_CODE_OFFSET)*8;
                        return (short) ((address - offset)/4);

                    case 8:
                        return (short) (address/8);

                    default:
                        throw new NotImplementedException();
                }
            }

            #endregion
        }
    }
}
