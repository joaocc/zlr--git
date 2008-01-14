using System;
using System.Collections.Generic;
using System.Text;
using ZLR.VM;
using System.Reflection;
using System.IO;

namespace TestSuite
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ZLR Test Suite {0}", ZMachine.ZLR_VERSION);
            Console.WriteLine();

            string testpath = FindTestCases();
            if (testpath == null)
            {
                Console.WriteLine("Test cases not found.");
                return;
            }

            Console.WriteLine("Using test cases in {0}.", testpath);
            TestCase[] testCases = TestCase.LoadAll(testpath);
            
            do
            {
                Console.WriteLine();
                Console.WriteLine("=== Menu ===");
                Console.WriteLine();
                Console.WriteLine("1. List all tests");
                Console.WriteLine("2. Run all tests");
                Console.WriteLine("3. Record expected outcome");
                Console.WriteLine("0. Quit");
                Console.WriteLine();
                Console.Write("Choice: ");

                ConsoleKeyInfo info = Console.ReadKey();
                Console.WriteLine();

                switch (info.KeyChar)
                {
                    case '1':
                        ListAllTests();
                        break;

                    case '2':
                        RunAllTests();
                        break;

                    case '3':
                        RecordExpectedOutcome();
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

        static string FindTestCases()
        {
            const string DIRNAME = "Test Cases";
            
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (path.Length > 0)
            {
                if (Directory.Exists(Path.Combine(path, DIRNAME)))
                    return Path.Combine(path, DIRNAME);

                path = Path.GetDirectoryName(path);
            }

            return null;
        }

        private static void RecordExpectedOutcome()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        private static void RunAllTests()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        private static void ListAllTests()
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
