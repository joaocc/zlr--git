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
        static int Main(string[] args)
        {
            Console.Title = "ConsoleZLR";

            Stream gameStream = null, debugStream = null;
            string fileName = null, commandFile = null;
            bool dumb = false, debugger = false;

            if (args.Length >= 1 && args[0].Length > 0)
            {
                int n = 0;

                if (args[n].ToLower() == "-commands")
                {
                    if (args.Length > n + 1)
                    {
                        commandFile = args[n + 1];
                        n += 2;
                        if (args.Length <= n)
                            return Usage();
                    }
                    else
                        return Usage();
                }
                else if (args[n].ToLower() == "-dumb")
                {
                    n++;
                    dumb = true;
                }
                else if (args[n].ToLower() == "-debug")
                {
                    n++;
                    dumb = true;
                    debugger = true;
                }

                gameStream = new FileStream(args[n], FileMode.Open, FileAccess.Read);
                fileName = Path.GetFileName(args[n]);

                if (args.Length > n + 1)
                    debugStream = new FileStream(args[n + 1], FileMode.Open, FileAccess.Read);
            }
            else
            {
                return Usage();
            }

            IZMachineIO io;

            if (dumb)
            {
                io = new DumbIO();
            }
            else
            {
                ConsoleIO cio = new ConsoleIO(fileName);
                if (commandFile != null)
                {
                    cio.SuppliedCommandFile = commandFile;
                    cio.HideMorePrompts = true;
                }
                io = cio;
            }

            ZMachine zm = new ZMachine(gameStream, io);
            if (commandFile != null)
                zm.ReadingCommandsFromFile = true;
            if (debugStream != null)
                zm.LoadDebugInfo(debugStream);

            if (debugger)
            {
                DebuggerLoop(zm);
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

        private static int Usage()
        {
            string exe = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
            Console.WriteLine("Usage: {0} [-commands <commandfile.txt>] <game_file.z5/z8> [<debug_file.dbg>]", exe);
            return 1;
        }

        private static void DebuggerLoop(ZMachine zm)
        {
            IDebugger dbg = zm.Debug();

            Console.WriteLine("ConsoleZLR Debugger");
            dbg.Restart();

            string lastCmd = null;
            char[] delim = new char[] { ' ' };
            while (true)
            {
                Console.Write("${0:x5}   ", dbg.CurrentPC);
                Console.WriteLine(dbg.Disassemble(dbg.CurrentPC));

                if (zm.DebugInfo != null)
                {
                    RoutineInfo rtn = zm.DebugInfo.FindRoutine(dbg.CurrentPC);
                    if (rtn != null)
                    {
                        Console.Write("(in ");
                        Console.Write(rtn.Name);

                        LineInfo? li = zm.DebugInfo.FindLine(dbg.CurrentPC);
                        if (li != null)
                            Console.WriteLine(" at {0}:{1})", li.Value.File, li.Value.Line);
                        else
                            Console.WriteLine(")");
                    }
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
                switch (parts[0].ToLower())
                {
                    case "reset":
                        dbg.Restart();
                        break;

                    case "s":
                    case "step":
                        dbg.StepInto();
                        break;

                    case "o":
                    case "over":
                        dbg.StepOver();
                        break;

                    case "r":
                    case "run":
                        dbg.Run();
                        break;

                    case "b":
                    case "break":
                        if (parts.Length < 1 || (address = ParseAddress(zm, dbg, parts[1])) < 0)
                        {
                            Console.WriteLine("Usage: break <addrspec>");
                        }
                        else
                        {
                            dbg.SetBreakpoint(address, true);
                            Console.WriteLine("Set breakpoint at ${0:x5}.", address);
                        }
                        break;

                    case "c":
                    case "clear":
                        if (parts.Length < 1 || (address = ParseAddress(zm, dbg, parts[1])) < 0)
                        {
                            Console.WriteLine("Usage: clear <addrspec>");
                        }
                        else
                        {
                            dbg.SetBreakpoint(address, true);
                            Console.WriteLine("Cleared breakpoint at ${0:x5}.", address);
                        }
                        break;

                    case "q":
                    case "quit":
                        Console.WriteLine("Goodbye.");
                        return;

                    default:
                        Console.WriteLine("Unrecognized debugger command.");
                        Console.WriteLine("Commands: reset, (s)tep, (o)ver, (r)un, (b)reak, (c)lear, (q)uit");
                        break;
                }
            }
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

                    RoutineInfo rtn = zm.DebugInfo.FindRoutine(spec);
                    if (rtn != null && rtn.LineOffsets.Length > 0)
                        return rtn.CodeStart + rtn.LineOffsets[0];
                }
            }

            return -1;
        }
    }
}
