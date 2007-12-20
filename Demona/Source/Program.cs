using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using ZLR.VM;

namespace ZLR.Interfaces.Demona
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Stream gameFile;
            string storyName;

            if (args.Length > 0)
            {
                if (!File.Exists(args[0]))
                {
                    MessageBox.Show("File not found: " + args[0]);
                    return;
                }

                try
                {
                    gameFile = new FileStream(args[0], System.IO.FileMode.Open, FileAccess.Read);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }
                storyName = Path.GetFileName(args[0]);
            }
            else
            {
                using (OpenFileDialog dlg = new OpenFileDialog())
                {
                    dlg.Title = "Select Game File";
                    dlg.Filter = "Supported Z-code files (*.z5;*.z8;*.zblorb;*.zlb)|*.z5;*.z8;*.zblorb;*.zlb|All files (*.*)|*.*";
                    dlg.CheckFileExists = true;
                    if (dlg.ShowDialog() != DialogResult.OK)
                        return;

                    try
                    {
                        gameFile = dlg.OpenFile();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error Loading Game");
                        return;
                    }
                    storyName = Path.GetFileName(dlg.FileName);
                }
            }

            using (GlkIO io = new GlkIO(args, storyName))
            {
#if !DEBUG
                try
                {
#endif
                    try
                    {
                        ZMachine engine = new ZMachine(gameFile, io);
                        engine.Run();
                    }
                    finally
                    {
                        gameFile.Close();
                    }
#if !DEBUG
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error Running Game");
                    return;
                }
#endif
            }
        }
    }
}