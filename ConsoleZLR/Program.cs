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
            string fileName = null;
            if (args.Length >= 1 && args[0].Length > 0)
            {
                gameStream = new FileStream(args[0], FileMode.Open, FileAccess.Read);
                fileName = Path.GetFileName(args[0]);

                if (args.Length >= 2)
                    debugStream = new FileStream(args[1], FileMode.Open, FileAccess.Read);
            }
            else
            {
                string exe = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
                Console.WriteLine("Usage: {0} <game_file.z5/z8> [<debug_file.dbg>]");
                return 1;
            }

            ZMachine zm = new ZMachine(gameStream, new ConsoleIO(fileName));
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
    }
}
