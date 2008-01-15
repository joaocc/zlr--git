using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace TestSuite
{
    abstract class TestCase
    {
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
                    case ".z5":
                    case ".z8":
                        result.Add(shortname, new CompiledTestCase(file));
                        break;

                    case ".inf":
                        result.Add(shortname, new InformTestCase(file));
                        break;
                }
            }

            return result;
        }

        public abstract Stream GetZCode();

        public virtual void CleanUp()
        {
        }
    }

    class CompiledTestCase : TestCase
    {
        private readonly string zfile;

        public CompiledTestCase(string file)
        {
            zfile = file;
        }

        public override Stream GetZCode()
        {
            return new FileStream(zfile, FileMode.Open, FileAccess.Read);
        }
    }

    class InformTestCase : TestCase
    {
        private readonly string inffile;
        private string zfile;

        public InformTestCase(string file)
        {
            inffile = file;
        }

        public override Stream GetZCode()
        {
            string path = Path.GetDirectoryName(inffile);
            string compiler = Path.Combine(path, "compile-game.bat");
            string infbase = Path.GetFileNameWithoutExtension(inffile);

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
            string outfile = Path.Combine(outpath, infbase + ".z5");
            if (!File.Exists(outfile))
                throw new Exception("Failed to compile test case");

            zfile = outfile;
            return new FileStream(outfile, FileMode.Open, FileAccess.Read);
        }

        public override void CleanUp()
        {
            if (zfile != null)
            {
                File.Delete(zfile);
                zfile = null;
            }
        }
    }
}
