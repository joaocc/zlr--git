using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ZLR.VM;
using System.Threading;
using System.Reflection;
using ZLR.VM.Debugging;

namespace ZLR.Interfaces.SystemConsole
{
    class Program
    {
        enum DisplayType { FullScreen, Dumb, DumbBottomWinOnly }

        static int Main(string[] args)
        {
            try
            {
                Console.Title = "ConsoleZLR";

                Stream gameStream = null, debugStream = null;
                string gameDir = null, debugDir = null;
                string fileName = null, commandFile = null;
                DisplayType displayType = DisplayType.FullScreen;
                bool debugger = false, predictable = false;

                if (args.Length >= 1 && args[0].Length > 0)
                {
                    int n = 0;

                    bool parsing = true;
                    do
                    {
                        switch (args[n].ToLower())
                        {
                            case "-commands":
                                if (args.Length > n + 1)
                                {
                                    commandFile = args[n + 1];
                                    n += 2;
                                    if (args.Length <= n)
                                        return Usage();
                                }
                                else
                                    return Usage();
                                break;
                            case "-dumb":
                                n++;
                                displayType = DisplayType.Dumb;
                                break;
                            case "-dumb2":
                                n++;
                                displayType = DisplayType.DumbBottomWinOnly;
                                break;
                            case "-debug":
                                n++;
                                debugger = true;
                                break;
                            case "-predictable":
                                n++;
                                predictable = true;
                                break;
                            default:
                                parsing = false;
                                break;
                        }
                    } while (parsing);

                    gameStream = new FileStream(args[n], FileMode.Open, FileAccess.Read);
                    gameDir = Path.GetDirectoryName(Path.GetFullPath(args[n]));
                    fileName = Path.GetFileName(args[n]);

                    if (args.Length > n + 1)
                    {
                        debugStream = new FileStream(args[n + 1], FileMode.Open, FileAccess.Read);
                        debugDir = Path.GetDirectoryName(Path.GetFullPath(args[n + 1]));
                    }
                }
                else
                {
                    return Usage();
                }

                IZMachineIO io;

                switch (displayType)
                {
                    case DisplayType.Dumb:
                        io = new DumbIO();
                        break;

                    case DisplayType.DumbBottomWinOnly:
                        io = new DumbIO(true);
                        break;

                    case DisplayType.FullScreen:
                        ConsoleIO cio = new ConsoleIO(fileName);
                        if (commandFile != null)
                        {
                            cio.SuppliedCommandFile = commandFile;
                            cio.HideMorePrompts = true;
                        }
                        io = cio;
                        break;

                    default:
                        throw new NotImplementedException();
                }

                ZMachine zm = new ZMachine(gameStream, io);
                zm.PredictableRandom = predictable;
                if (commandFile != null)
                    zm.ReadingCommandsFromFile = true;
                if (debugStream != null)
                    zm.LoadDebugInfo(debugStream);

                if (debugger)
                {
                    List<string> sourcePath = new List<string>(3);
                    if (debugDir != null)
                        sourcePath.Add(debugDir);
                    sourcePath.Add(gameDir);
                    sourcePath.Add(Directory.GetCurrentDirectory());

                    DebuggerLoop(zm, sourcePath.ToArray());
                }
                else
                {
#if DEBUG
                    zm.Run();
#else
                try
                {
                    zm.Run();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
#endif
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey(true);
                }
                return 0;
            }
            catch (Exception ex)
            {
                return Error(ex.Message + " (" + ex.GetType().Name + ")");
            }
        }

        private static int Usage()
        {
            string exe = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
            Console.WriteLine("Usage: {0} [-commands <commandfile.txt>] [-dumb | -dumb2] [-debug] [-predictable] <game_file.z5/z8> [<debug_file.dbg>]", exe);
            return 1;
        }

        private static int Error(string msg)
        {
            Console.Error.Write("Error: ");
            Console.Error.WriteLine(msg);
            return 2;
        }

        private static void DebuggerLoop(ZMachine zm, string[] sourcePath)
        {
            IDebugger dbg = zm.Debug();
            RoutineInfo rtn;
            SourceCache src = new SourceCache(sourcePath);

            Console.WriteLine("ConsoleZLR Debugger");
            dbg.Restart();

            string lastCmd = null;
            char[] delim = new char[] { ' ' };
            while (true)
            {
                Console.WriteLine();
                if (dbg.State == DebuggerState.Paused)
                {
                    if (zm.DebugInfo != null &&
                        (rtn = zm.DebugInfo.FindRoutine(dbg.CurrentPC)) != null)
                    {
                        Console.Write("${0:x5} ({1}+{2})   ",
                            dbg.CurrentPC,
                            rtn.Name,
                            dbg.CurrentPC - rtn.CodeStart);
                        Console.WriteLine(dbg.Disassemble(dbg.CurrentPC));

                        LineInfo? li = zm.DebugInfo.FindLine(dbg.CurrentPC);
                        if (li != null)
                            Console.WriteLine("{0}:{1}: {2}",
                                li.Value.File,
                                li.Value.Line,
                                src.Load(li.Value));
                    }
                    else
                    {
                        Console.Write("${0:x5}   ", dbg.CurrentPC);
                        Console.WriteLine(dbg.Disassemble(dbg.CurrentPC));
                    }
                }
                else if (dbg.State == DebuggerState.Stopped)
                {
                    Console.WriteLine("Debugger is stopped.");
                }

                Console.Write("D> ");

                string cmd = Console.ReadLine();
                if (cmd.Trim() == "")
                {
                    if (lastCmd == null)
                    {
                        Console.WriteLine("No last command.");
                        continue;
                    }
                    cmd = lastCmd;
                }
                else
                {
                    lastCmd = cmd;
                }

                string[] parts = cmd.Split(delim, StringSplitOptions.RemoveEmptyEntries);
                int address;
                ICallFrame[] frames;
                switch (parts[0].ToLower())
                {
                    case "reset":
                        dbg.Restart();
                        break;

                    case "s":
                    case "step":
                        if (dbg.State == DebuggerState.Paused)
                            dbg.StepInto();
                        break;
                        
                    case "o":
                    case "over":
                        if (dbg.State == DebuggerState.Paused)
                            dbg.StepOver();
                        break;

                    case "up":
                        if (dbg.State == DebuggerState.Paused)
                            dbg.StepUp();
                        break;

                    case "sl":
                    case "stepline":
                        if (dbg.State == DebuggerState.Paused)
                        {
                            if (zm.DebugInfo == null)
                            {
                                Console.WriteLine("No line information.");
                            }
                            else
                            {
                                LineInfo? oldLI = zm.DebugInfo.FindLine(dbg.CurrentPC);
                                LineInfo? newLI;
                                do
                                {
                                    dbg.StepInto();
                                    if (dbg.State != DebuggerState.Paused)
                                        break;

                                    newLI = zm.DebugInfo.FindLine(dbg.CurrentPC);
                                } while (newLI != null && newLI == oldLI);
                            }
                        }
                        break;


                    case "ol":
                    case "overline":
                        if (dbg.State == DebuggerState.Paused)
                        {
                            if (zm.DebugInfo == null)
                            {
                                Console.WriteLine("No line information.");
                            }
                            else
                            {
                                LineInfo? oldLI = zm.DebugInfo.FindLine(dbg.CurrentPC);
                                LineInfo? newLI;
                                do
                                {
                                    dbg.StepOver();
                                    if (dbg.State != DebuggerState.Paused)
                                        break;

                                    newLI = zm.DebugInfo.FindLine(dbg.CurrentPC);
                                } while (newLI != null && newLI == oldLI);
                            }
                        }
                        break;

                    case "r":
                    case "run":
                        if (dbg.State == DebuggerState.Stopped)
                            dbg.Restart();
                        dbg.Run();
                        break;

                    case "b":
                    case "break":
                        if (parts.Length < 2 || (address = ParseAddress(zm, dbg, parts[1])) < 0)
                        {
                            Console.WriteLine("Usage: break <addrspec>");
                        }
                        else
                        {
                            dbg.SetBreakpoint(address, true);
                            Console.WriteLine("Set breakpoint at {0}.", DumpCodeAddress(zm, dbg, address));
                        }
                        break;

                    case "c":
                    case "clear":
                        if (parts.Length < 2 || (address = ParseAddress(zm, dbg, parts[1])) < 0)
                        {
                            Console.WriteLine("Usage: clear <addrspec>");
                        }
                        else
                        {
                            dbg.SetBreakpoint(address, false);
                            Console.WriteLine("Cleared breakpoint at {0}.", DumpCodeAddress(zm, dbg, address));
                        }
                        break;

                    case "bps":
                    case "breakpoints":
                        int[] breakpoints = dbg.GetBreakpoints();
                        if (breakpoints.Length == 0)
                        {
                            Console.WriteLine("No breakpoints.");
                        }
                        else
                        {
                            Console.WriteLine("{0} breakpoint{1}:",
                                breakpoints.Length,
                                breakpoints.Length == 1 ? "" : "s");

                            Array.Sort(breakpoints);
                            foreach (int bp in breakpoints)
                                Console.WriteLine("    {0}", DumpCodeAddress(zm, dbg, bp));
                        }
                        break;

                    case "bt":
                    case "backtrace":
                        frames = dbg.GetCallFrames();
                        Console.WriteLine("Call depth: {0}", frames.Length);
                        Console.WriteLine("PC = {0}", DumpCodeAddress(zm, dbg, dbg.CurrentPC));

                        for (int i = 0; i < frames.Length; i++)
                        {
                            ICallFrame cf = frames[i];
                            Console.WriteLine("==========");
                            Console.WriteLine("[{0}] return PC = {1}", i + 1, DumpCodeAddress(zm, dbg, cf.ReturnPC));
                            Console.WriteLine("called with {0} arg{1}, stack depth {2}",
                                cf.ArgCount,
                                cf.ArgCount == 1 ? "" : "s",
                                cf.PrevStackDepth);

                            if (cf.ResultStorage < 16)
                            {
                                if (cf.ResultStorage == -1)
                                {
                                    Console.WriteLine("discarding result");
                                }
                                else if (cf.ResultStorage == 0)
                                {
                                    Console.WriteLine("storing result to stack");
                                }
                                else
                                {
                                    rtn = null;
                                    if (zm.DebugInfo != null)
                                        rtn = zm.DebugInfo.FindRoutine(cf.ReturnPC);
                                    if (rtn != null && cf.ResultStorage - 1 < rtn.Locals.Length)
                                        Console.WriteLine("storing result to local {0} ({1})",
                                            cf.ResultStorage,
                                            rtn.Locals[cf.ResultStorage - 1]);
                                    else
                                        Console.WriteLine("storing result to local {0}", cf.ResultStorage);
                                }
                            }
                            else if (zm.DebugInfo.Globals.Contains((byte)cf.ResultStorage))
                            {
                                Console.WriteLine("storing result to global {0} ({1})", cf.ResultStorage,
                                    zm.DebugInfo.Globals[(byte)cf.ResultStorage]);
                            }
                            else
                            {
                                Console.WriteLine("storing result to global {0}", cf.ResultStorage);
                            }
                        }
                        Console.WriteLine("==========");
                        break;

                    case "l":
                    case "locals":
                        frames = dbg.GetCallFrames();
                        int stackItems;
                        if (frames.Length == 0)
                        {
                            Console.WriteLine("No call frame.");
                            stackItems = dbg.StackDepth;
                        }
                        else
                        {
                            ICallFrame cf = frames[0];
                            if (cf.Locals.Length == 0)
                            {
                                Console.WriteLine("No local variables.");
                            }
                            else
                            {
                                Console.WriteLine("{0} local variable{1}:",
                                    cf.Locals.Length,
                                    cf.Locals.Length == 1 ? "" : "s");

                                rtn = zm.DebugInfo.FindRoutine(dbg.CurrentPC);
                                for (int i = 0; i < cf.Locals.Length; i++)
                                {
                                    Console.Write("    ");
                                    if (rtn != null && i < rtn.Locals.Length)
                                        Console.Write(rtn.Locals[i]);
                                    else
                                        Console.Write("local_{0}", i + 1);
                                    Console.WriteLine(" = {0} (${0:x4})", cf.Locals[i]);
                                }
                            }
                            stackItems = dbg.StackDepth - cf.PrevStackDepth;
                        }
                        if (stackItems == 0)
                        {
                            Console.WriteLine("No data on stack.");
                        }
                        else
                        {
                            Console.WriteLine("{0} word{1} on stack:",
                                stackItems,
                                stackItems == 1 ? "" : "s");
                            Stack<short> temp = new Stack<short>();
                            for (int i = 0; i < stackItems; i++)
                            {
                                short value = dbg.StackPop();
                                temp.Push(value);
                                Console.WriteLine("    ${0:x4} (${0})", value);
                            }
                            while (temp.Count > 0)
                                dbg.StackPush(temp.Pop());
                        }
                        break;

                    case "q":
                    case "quit":
                        Console.WriteLine("Goodbye.");
                        return;

                    default:
                        Console.WriteLine("Unrecognized debugger command.");
                        
                        Console.WriteLine("Commands:");
                        Console.WriteLine("reset, (s)tep, (o)ver, stepline (sl), overline (ol), up, (r)un,");
                        Console.WriteLine("(b)reak, (c)lear, breakpoints (bps)");
                        Console.WriteLine("backtrace (bt), (l)ocals, (g)lobals, (q)uit");
                        break;
                }
            }
        }

        private static string DumpCodeAddress(ZMachine zm, IDebugger dbg, int address)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("${0:x5}", address);

            if (zm.DebugInfo != null)
            {
                RoutineInfo rtn = zm.DebugInfo.FindRoutine(address);
                if (rtn != null)
                {
                    sb.AppendFormat(" ({0}+{1}", rtn.Name, address - rtn.CodeStart);

                    LineInfo? li = zm.DebugInfo.FindLine(address);
                    if (li != null)
                        sb.AppendFormat(", {0}:{1}", li.Value.File, li.Value.Line);

                    sb.Append(')');
                }
            }

            return sb.ToString();
        }

        private static int ParseAddress(ZMachine zm, IDebugger dbg, string spec)
        {
            if (!string.IsNullOrEmpty(spec))
            {
                if (spec[0] == '$')
                    return Convert.ToInt32(spec.Substring(1), 16);

                if (char.IsDigit(spec[0]))
                    return Convert.ToInt32(spec);

                if (zm.DebugInfo != null)
                {
                    int idx = spec.IndexOf(':');
                    if (idx >= 0)
                    {
                        try
                        {
                            int result = zm.DebugInfo.FindCodeAddress(
                                spec.Substring(0, idx),
                                Convert.ToInt32(spec.Substring(idx + 1)));
                            if (result >= 0)
                                return result;
                        }
                        catch (FormatException) { }
                        catch (OverflowException) { }
                    }

                    RoutineInfo rtn;

                    idx = spec.IndexOf('+');
                    if (idx >= 0)
                    {
                        try
                        {
                            rtn = zm.DebugInfo.FindRoutine(spec.Substring(0, idx));
                            if (rtn != null)
                                return rtn.CodeStart + Convert.ToInt32(spec.Substring(idx + 1));
                        }
                        catch (FormatException) { }
                        catch (OverflowException) { }
                    }

                    rtn = zm.DebugInfo.FindRoutine(spec);
                    if (rtn != null && rtn.LineOffsets.Length > 0)
                        return rtn.CodeStart + rtn.LineOffsets[0];
                }
            }

            return -1;
        }
    }

    class SourceCache
    {
        private const int MAX_SRC_LINE_LEN = 50;

        private readonly string[] searchPath;
        private Dictionary<string, string[]> cache = new Dictionary<string, string[]>();

        public SourceCache(string[] searchPath)
        {
            this.searchPath = searchPath;
        }

        private string FindFile(string filename)
        {
            foreach (string p in searchPath)
            {
                string combined = Path.Combine(p, filename);
                if (File.Exists(combined))
                    return combined;
            }

            if (File.Exists(filename))
                return Path.GetFullPath(filename);

            return null;
        }

        public string Load(LineInfo li)
        {
            string[] lines;

            if (cache.TryGetValue(li.File, out lines) == false)
            {
                string file = FindFile(li.File);
                if (file == null)
                {
                    cache.Add(li.File, null);
                }
                else if (cache.TryGetValue(file, out lines) == true)
                {
                    cache.Add(li.File, lines);
                }
                else
                {
                    lines = File.ReadAllLines(file);
                    cache.Add(li.File, lines);
                    cache.Add(file, lines);
                }
            }

            if (lines != null)
            {
                int line = li.Line - 1;
                if (line < lines.Length)
                {
                    string result = lines[line];
                    if (result.Length > MAX_SRC_LINE_LEN)
                        return result.Substring(0, MAX_SRC_LINE_LEN - 3) + "...";
                    else
                        return result;
                }
            }

            return null;
        }
    }
}
