using System;
using System.Collections.Generic;
using System.Text;
using ZLR.VM;

namespace TestSuite
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ZLR Test Suite {0}", ZMachine.ZLR_VERSION);

            do
            {
                Console.WriteLine();
                Console.WriteLine("=== Menu ===");
                Console.WriteLine();
                Console.WriteLine("1. List all tests");
                Console.WriteLine("2. Run all tests");
                Console.WriteLine("0. Quit");
                Console.WriteLine();
                Console.Write("Choice: ");

                ConsoleKeyInfo info = Console.ReadKey();
                Console.WriteLine();

                switch (info.KeyChar)
                {
                    case '1':
                        break;

                    case '2':
                        break;

                    case '0':
                        Console.WriteLine("Goodbye.");
                        return;

                    default:
                        Console.WriteLine("Invalid selection.");
                        break;
                }
            }
            while (true);
        }
    }
}
