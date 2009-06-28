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

        void Run();
        void SetBreakpoint(int address, bool enabled);

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

        int UnpackAddress(short packedAddress);
        short PackAddress(int address);
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
            }

            public void StepInto()
            {
                zm.stepping = 1;
                zm.running = true;

                CachedCode entry;
                int thisPC = zm.pc;
                if (thisPC < zm.romStart || zm.cache.TryGetValue(thisPC, out entry) == false)
                {
                    int count;
                    entry.Code = zm.CompileZCode(out count);
                    entry.NextPC = zm.pc;
                    if (thisPC >= zm.romStart)
                        zm.cache.Add(thisPC, entry, count);
                }
                zm.pc = entry.NextPC;
                entry.Code();

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

            public void Run()
            {
                zm.running = true;
                zm.debugState = DebuggerState.Running;
                while (zm.running && zm.debugState == DebuggerState.Running)
                    zm.JitLoop();

                zm.debugState = zm.running ? DebuggerState.Paused : DebuggerState.Stopped;
            }

            public void SetBreakpoint(int address, bool enabled)
            {
                if (enabled)
                    zm.breakpoints[address] = true;
                else
                    zm.breakpoints.Remove(address);
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
                            return rtn.Locals[varnum - 1];
                        else if (varnum < 16)
                            return "local_" + varnum;
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

            public int UnpackAddress(short packedAddress)
            {
                return zm.UnpackAddress(packedAddress);
            }

            public short PackAddress(int address)
            {
                if (zm.zversion == 5)
                    return (short)(address / 4);
                else
                    return (short)(address / 8);
            }

            #endregion
        }
    }
}
