using System;
using System.Text;
using log4net;
using SvnTools;
using SvnTools.CommandLine;

// $Id$

namespace SvnBackup
{
    class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

        static int Main(string[] args)
        {

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Log.Info("Current Dir:" + Environment.CurrentDirectory);
            if (Parser.ParseHelp(args))
            {
                OutputHeader();
                OutputUsageHelp();
                return 0;
            }

            StringBuilder errorBuffer = new StringBuilder();
            BackupArguments arguments = new BackupArguments();
            if (!Parser.ParseArguments(args, arguments, s => errorBuffer.AppendLine(s)))
            {
                OutputHeader();
                Console.Error.WriteLine(errorBuffer.ToString());
                OutputUsageHelp();
                return 1;
            }

            Backup.Run(arguments);

            return 0;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Fatal("Unhandled exception:" + e.ExceptionObject);
        }

        private static void OutputUsageHelp()
        {
            Console.WriteLine();
            Console.WriteLine("SvnBackup.exe /r:<directory> /b:<directory> /c");
            Console.WriteLine();
            Console.WriteLine("     - BACKUP OPTIONS -");
            Console.WriteLine();
            Console.WriteLine(Parser.ArgumentsUsage(typeof(BackupArguments)));
        }

        private static void OutputHeader()
        {
            Console.WriteLine("SvnBackup v{0}", ThisAssembly.AssemblyInformationalVersion);
            Console.WriteLine();
        }
    }
}
