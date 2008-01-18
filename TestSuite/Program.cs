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
        private static Dictionary<string, TestCase> testCases;
        private static string testPath;

        private const string TESTCASES_DIR_NAME = "Test Cases";

        static void Main(string[] args)
        {
            Console.WriteLine("ZLR Test Suite {0}", ZMachine.ZLR_VERSION);
            Console.WriteLine();

            testPath = FindTestCases();
            if (testPath == null)
            {
                Console.WriteLine("Test cases not found.");
                return;
            }

            Console.WriteLine("Using test cases in {0}.", testPath);
            testCases = TestCase.LoadAll(testPath);

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
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            while (path.Length > 0)
            {
                if (Directory.Exists(Path.Combine(path, TESTCASES_DIR_NAME)))
                    return Path.Combine(path, TESTCASES_DIR_NAME);

                path = Path.GetDirectoryName(path);
            }

            return null;
        }

        private static void RecordExpectedOutcome()
        {
            TestCase selected = PromptForTestCase();

            if (selected != null)
            {
                try
                {
                    RecordingIO io = new RecordingIO(selected.InputFile);
                    ZMachine zm = new ZMachine(selected.GetZCode(), io);

                    zm.WritingCommandsToFile = true;

                    string output = null;

                    try
                    {
                        zm.Run();
                        output = io.CollectOutput();
                    }
                    catch (Exception ex)
                    {
                        if (output == null)
                            output = io.CollectOutput();

                        output += "\n\n*** Exception ***\n" + ex.ToString();
                    }

                    File.WriteAllText(selected.OutputFile, output);
                }
                finally
                {
                    selected.CleanUp();
                }
            }
        }

        private static void RunAllTests()
        {
            throw new NotImplementedException();
        }

        private static void ListAllTests()
        {
            List<string> names = new List<string>(testCases.Keys);
            names.Sort();

            if (names.Count == 0)
            {
                Console.WriteLine("No tests.");
            }
            else
            {
                foreach (string name in names)
                    Console.WriteLine("{0} - {1}", name, Path.GetFileName(testCases[name].TestFile));
            }
        }

        private static TestCase PromptForTestCase()
        {
            const string prompt = "Select a test case (blank to cancel, \"?\" for list): ";

            while (true)
            {
                Console.Write(prompt);
                string line = Console.ReadLine().Trim();

                Console.WriteLine();

                if (line.Length == 0)
                    return null;
                else if (line == "?")
                    ListAllTests();
                else if (testCases.ContainsKey(line))
                    return testCases[line];
                else
                    Console.WriteLine("Invalid selection.");

                Console.WriteLine();
            }
        }
    }
}
