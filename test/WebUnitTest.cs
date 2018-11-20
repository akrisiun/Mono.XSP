using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace test
{
    [TestClass]
    public class UnitTest
    {
        [STAThread]
        public static void Main()
        {
            Console.WriteLine("Testing XSP5 ..");

            new UnitTest().TestMethod1();
        }

        [TestMethod]
        public void TestMethod1()
        {
            /*
            : This method cannot be called during the application's pre-start initialization phase.]
   System.Web.Compilation.BuildManager.EnsureTopLevelFilesCompiled() + 473
   System.Web.Compilation.BuildManager.GetGlobalAsaxTypeInternal() + 16
   System.Web.HttpApplicationFactory.CompileApplication() + 43
   System.Web.HttpApplicationFactory.Init() + 213
            */

            // Mono.WebServer.XSP.Server.Main(Environment.GetCommandLineArgs());
            // ProcessPS("debug.ps1");
            ProcessPS("--debug  ./bin/Mono.WebServer.XSP.exe --printlog");
        }

        // https://stackoverflow.com/questions/23920906/keep-console-window-of-a-new-process-open-after-it-finishes
        public static void ProcessPS(string argParm)
        {
            var sb = new StringBuilder();
            var psi = new ProcessStartInfo();
            // psi.FileName = "@powershell";
            // psi.Arguments = $"-file {ps1}";
            // psi.FileName = @"C:\Program Files\Mono\bin\mono.exe";

            Console.WriteLine($"Dir = {Environment.CurrentDirectory}");
            var bs = Path.DirectorySeparatorChar.ToString();
            var mono = Path.GetFullPath(Environment.CurrentDirectory  + bs + @"mono.exe");
            Console.WriteLine($"mono: {mono}");

            psi.FileName = mono;

            psi.Arguments = argParm;

            var p = new Process { StartInfo = psi };

            // redirect the output
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;

            // hookup the eventhandlers to capture the data that is received
            p.OutputDataReceived += (sender, args) =>
            {
                sb.AppendLine(args.Data);
                Console.WriteLine(args.Data);
            };
            p.ErrorDataReceived += (sender, args) =>
            {
                sb.AppendLine(args.Data);
                Console.WriteLine($"ERR: {args.Data}");
            };

            // direct start
            p.StartInfo.UseShellExecute = false;

            p.Start();
            // start our event pumps
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            // until we are done
            p.WaitForExit();

        }
    }
}
