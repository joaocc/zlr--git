using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ZLR.VM;
using System.Threading;
using System.Reflection;

namespace ZLR.Interfaces.SystemConsole
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.Title = "ConsoleZLR";

            Stream gameStream = null, debugStream = null;
            string fileName = null, commandFile = null;
            bool dumb = false;

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
            return 0;
        }

        private static int Usage()
        {
            string exe = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
            Console.WriteLine("Usage: {0} [-commands <commandfile.txt>] <game_file.z5/z8> [<debug_file.dbg>]", exe);
            return 1;
        }
    }
}
