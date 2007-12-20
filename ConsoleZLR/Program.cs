using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ZLR.VM;
using System.Threading;

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
                Console.WriteLine("*** Sample games ***");
                Console.WriteLine();
                Console.WriteLine("1) Hello");
                Console.WriteLine("2) Montana Parks and the Trivial Example");
                Console.WriteLine("3) Toyshop");
                Console.WriteLine();
                Console.Write("Press a key to select: ");

                bool repeat;
                do
                {
                    repeat = false;
                    ConsoleKeyInfo info = Console.ReadKey();
                    switch (info.KeyChar)
                    {
                        case '1':
                            gameStream = new MemoryStream(Properties.Resources.hello_z5);
                            debugStream = new MemoryStream(Properties.Resources.hello_dbg);
                            fileName = "Hello";
                            break;
                        case '2':
                            gameStream = new MemoryStream(Properties.Resources.montana_z5);
                            debugStream = new MemoryStream(Properties.Resources.montana_dbg);
                            fileName = "Montana";
                            break;
                        case '3':
                            gameStream = new MemoryStream(Properties.Resources.toyshop_z5);
                            debugStream = new MemoryStream(Properties.Resources.toyshop_dbg);
                            fileName = "Toyshop";
                            break;
                        default:
                            Console.WriteLine();
                            Console.Write("Invalid selection. Try again: ");
                            repeat = true;
                            break;
                    }
                }
                while (repeat);
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
