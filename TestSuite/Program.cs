using System;
using System.Collections.Generic;
using System.Text;
using ZLR.VM;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;

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
                Console.WriteLine();
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
                    using (Stream zcode = selected.GetZCode())
                    {
                        RecordingIO io = new RecordingIO(selected.InputFile);
                        ZMachine zm = new ZMachine(zcode, io);
                        zm.PredictableRandom = true;
                        zm.WritingCommandsToFile = true;

                        string output = RunAndCollectOutput(zm, io);
                        File.WriteAllText(selected.OutputFile, output);
                    }
                }
                finally
                {
                    selected.CleanUp();
                }
            }
        }

        private static string RunAndCollectOutput(ZMachine zm, TestCaseIO io)
        {
            string output = null;

            try
            {
                zm.PredictableRandom = true;
                zm.Run();
                output = io.CollectOutput();
            }
            catch (Exception ex)
            {
                if (output == null)
                    output = io.CollectOutput();

                output += "\n\n*** Exception ***\n" + ex.ToString();
            }

            return output;
        }

        private static void RunAllTests()
        {
            List<string> names = new List<string>(testCases.Keys);
            names.Sort();

            if (names.Count == 0)
            {
                Console.WriteLine("No tests to run.");
            }
            else
            {
                int failures = 0;

                foreach (string name in names)
                {
                    TestCase test = testCases[name];

                    Console.Write("{0} - ", name);

                    if (!File.Exists(test.InputFile) || !File.Exists(test.OutputFile))
                    {
                        Console.WriteLine("skipping (expected outcome not recorded).");
                    }
                    else
                    {
                        try
                        {
                            using (Stream zcode = test.GetZCode())
                            {
                                ReplayIO io = new ReplayIO(test.InputFile);
                                ZMachine zm = new ZMachine(zcode, io);

                                zm.PredictableRandom = true;
                                zm.ReadingCommandsFromFile = true;

                                string output = RunAndCollectOutput(zm, io);
                                string expectedOutput = File.ReadAllText(test.OutputFile);

                                if (OutputDiffers(expectedOutput, output))
                                {
                                    Console.WriteLine("failed!");
                                    failures++;
                                    File.WriteAllText(test.FailureFile, output);
                                }
                                else
                                {
                                    Console.WriteLine("passed.");
                                }
                            }
                        }
                        finally
                        {
                            test.CleanUp();
                        }
                    }
                }

                if (failures > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("{0} test{1} failed. The actual output is saved with the suffix \".failed-output.txt\".",
                        failures,
                        failures == 1 ? "" : "s");
                }
            }
        }

        private static bool OutputDiffers(string expected, string actual)
        {
            // ignore compilation dates and tool versions in the output
            Regex rex = new Regex(
                @"serial number \d{6}|sn \d{6}|" +                              // serial number
                @"inform \d+ build .{4}|i\d/v\d\.\d+|lib \d+/\d+n?( [sd]+)?|" +  // I7 versions
                @"inform v\d\.\d+|library \d+/\d+n?( [sd]+)?",                  // I6 versions
                RegexOptions.IgnoreCase);
            expected = rex.Replace(expected, "");
            actual = rex.Replace(actual, "");

            return (expected != actual);
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
