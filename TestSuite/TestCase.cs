using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace TestSuite
{
    abstract class TestCase
    {
        private const string INPUT_SUFFIX = ".input.txt";
        private const string OUTPUT_SUFFIX = ".output.txt";
        private const string FAILURE_SUFFIX = ".failed-output.txt";

        protected readonly string testFile;

        protected TestCase(string file)
        {
            this.testFile = file;
        }

        public abstract Stream GetZCode();

        public string TestFile
        {
            get { return testFile; }
        }

        public string InputFile
        {
            get { return testFile + INPUT_SUFFIX; }
        }

        public string OutputFile
        {
            get { return testFile + OUTPUT_SUFFIX; }
        }

        public string FailureFile
        {
            get { return testFile + FAILURE_SUFFIX; }
        }

        public virtual void CleanUp()
        {
            // nada
        }

        public static Dictionary<string, TestCase> LoadAll(string path)
        {
            Dictionary<string, TestCase> result = new Dictionary<string, TestCase>();

            foreach (string file in Directory.GetFiles(path))
            {
                string shortname = Path.GetFileNameWithoutExtension(file);

                if (result.ContainsKey(shortname))
                {
                    int num = 2;
                    string shortbase = shortname;
                    do
                    {
                        shortname = shortbase + num.ToString();
                        num++;
                    } while (result.ContainsKey(shortname));
                }

                string ext = Path.GetExtension(file).ToLower();
                switch (ext)
                {
                    case ".z1":
                    case ".z2":
                    case ".z3":
                    case ".z4":
                    case ".z5":
                    case ".z6":
                    case ".z7":
                    case ".z8":
                    case ".zcode":
                    case ".zlb":
                    case ".zblorb":
                        result.Add(shortname, new CompiledTestCase(file));
                        break;

                    case ".inf":
                        result.Add(shortname, new InformTestCase(file));
                        break;
                }
            }

            return result;
        }
    }

    class CompiledTestCase : TestCase
    {
        public CompiledTestCase(string file) : base(file) { }

        public override Stream GetZCode()
        {
            return new FileStream(testFile, FileMode.Open, FileAccess.Read);
        }
    }

    class SourceCodeTestCase : TestCase, IDisposable
    {
        private readonly string compiler;
        private string zfile;

        public SourceCodeTestCase(string compiler, string file)
            : base(file)
        {
            this.compiler = compiler;

            // finalizer only needs to be called once we've compiled the test
            GC.SuppressFinalize(this);
        }

        public override Stream GetZCode()
        {
            string path = Path.GetDirectoryName(testFile);
            string compiler = Path.Combine(path, this.compiler);
            string infbase = Path.GetFileNameWithoutExtension(testFile);

            ProcessStartInfo info = new ProcessStartInfo();
            info.WorkingDirectory = path;
            info.FileName = compiler;
            info.Arguments = infbase;

            // TODO: check for compiler errors

            using (Process compilerProcess = Process.Start(info))
            {
                compilerProcess.WaitForExit();
            }

            string outpath = Path.Combine(path, "Compiled");
            string outfile = Path.Combine(outpath, infbase + ".zcode");
            if (!File.Exists(outfile))
                throw new Exception("Failed to compile test case");

            zfile = outfile;
            GC.ReRegisterForFinalize(this);
            return new FileStream(outfile, FileMode.Open, FileAccess.Read);
        }

        public override void CleanUp()
        {
            if (zfile != null)
            {
                try
                {
                    File.Delete(zfile);
                    File.Delete(Path.ChangeExtension(zfile, ".dbg"));
                }
                catch
                {
                    return;
                }

                zfile = null;
                GC.SuppressFinalize(this);
            }
        }

        void IDisposable.Dispose()
        {
            CleanUp();
        }

        ~SourceCodeTestCase()
        {
            CleanUp();
        }
    }

    class InformTestCase : SourceCodeTestCase
    {
        public InformTestCase(string file) :
            base("compile-inform-case.bat", file)
        {
        }
    }
}
