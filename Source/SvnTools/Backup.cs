﻿using System;
using System.IO;
using Ionic.Zip;
using SvnTools.Services;
using SvnTools.Utility;

// $Id$

namespace SvnTools
{
    /// <summary>
    /// A class to backup subversion repositories.
    /// </summary>
    public static class Backup
    {
        #region Logging Definition
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(Backup));
        #endregion

        /// <summary>
        /// Runs a backup with the specified <see cref="BackupArguments"/>.
        /// </summary>
        /// <param name="args">The arguments used in the backup.</param>
        public static void Run(BackupArguments args)
        {
            var repoRoot = new DirectoryInfo(args.RepositoryRoot);

            if (!repoRoot.Exists)
                throw new InvalidOperationException(string.Format(
                    "The repository root directory '{0}' does not exist.",
                    args.RepositoryRoot));

            var backupRoot = new DirectoryInfo(args.BackupRoot);
            if (!backupRoot.Exists)
                backupRoot.Create();

            // first try repoRoot as a repository
            if (PathHelper.IsRepository(repoRoot.FullName))
                BackupRepository(args, repoRoot, backupRoot);

            // next try as partent folder for repositories
            else
                foreach (var repo in repoRoot.GetDirectories())
                    BackupRepository(args, repo, backupRoot);
        }

        private static void BackupRepository(BackupArguments args, DirectoryInfo repository, DirectoryInfo backupRoot)
        {
            try
            {
                string revString = GetRevision(args, repository);

                if (string.IsNullOrEmpty(revString))
                {
                    Log.Info(string.Format("rev string is null in {0}", repository));
                    return; // couldn't find repo
                }

                string backupRepoPath = Path.Combine(backupRoot.FullName, repository.Name);
                string backupRevPath = Path.Combine(backupRepoPath, revString);
                string backupZipPath = backupRevPath + ".zip";

                if (!Directory.Exists(backupRepoPath))
                    Directory.CreateDirectory(backupRepoPath);

                if (File.Exists(backupZipPath))
                {
                    Log.Info(string.Format("this rev is already backed up, dir:{0}", backupRevPath)); // this rev is already backed up
                    return;
                }

                if (!Directory.Exists(backupRevPath))
                {
                    // hotcopy
                    Log.InfoFormat("Backing up '{0}' from '{1}'.", revString, repository.Name);
                    RunHotCopy(args, repository, backupRevPath);
                }
                else
                {
                    Log.Info(string.Format("this rev is already backed up, dir:{0}", backupRevPath)); // this rev is already backed up
                }

                // compress
                if (args.Compress)
                {
                    Log.Info(string.Format("Compressing {0}", backupZipPath));
                    CompressBackup(backupRevPath, backupZipPath);
                }
                else
                {
                    Log.Info("compress not active");
                }

                Log.Info(string.Format("Purging dir {0}", backupRepoPath));
                // purge old
                PruneBackups(backupRepoPath, args.History);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex);
            }
        }

        private static void RunHotCopy(BackupArguments args, DirectoryInfo repo, string backupRevPath)
        {
            var tempDir = backupRevPath + "Temp";

            using (var hotCopy = new HotCopy())
            {
                if (!string.IsNullOrEmpty(args.SubverisonPath))
                    hotCopy.ToolPath = args.SubverisonPath;

                hotCopy.BackupPath = tempDir;
                hotCopy.RepositoryPath = repo.FullName;

                hotCopy.Execute();

                if (!string.IsNullOrEmpty(hotCopy.StandardError))
                    Log.Info(hotCopy.StandardError);
            }

            Directory.Move(tempDir, backupRevPath);
        }

        private static string GetRevision(BackupArguments args, DirectoryInfo repo)
        {
            int rev;

            // version
            using (var look = new SvnLook(SvnLook.Commands.Youngest))
            {
                look.RepositoryPath = repo.FullName;
                if (!string.IsNullOrEmpty(args.SubverisonPath))
                    look.ToolPath = args.SubverisonPath;

                look.Execute();
                if (!string.IsNullOrEmpty(look.StandardError))
                    Log.Info(look.StandardError);

                if (!look.TryGetRevision(out rev))
                {
                    Log.WarnFormat("'{0}' is not a repository.", repo.Name);

                    if (!string.IsNullOrEmpty(look.StandardOutput))
                        Log.Info(look.StandardOutput);

                    return null;
                }
            }

            return "v" + rev.ToString().PadLeft(7, '0');
        }

        private static void CompressBackup(string backupRevPath, string backupZipPath)
        {
            using (var zipFile = new ZipFile())
            {
                // for large zip files
                zipFile.UseZip64WhenSaving = Zip64Option.AsNecessary;
                zipFile.UseUnicodeAsNecessary = true;

                zipFile.AddDirectory(backupRevPath);
                zipFile.Save(backupZipPath);
            }

            PathHelper.DeleteDirectory(backupRevPath);
        }

        private static void PruneBackups(string backupRepoPath, int historyCount)
        {
            if (historyCount < 1)
                return;

            var dirs = Directory.GetDirectories(backupRepoPath);
            if (dirs.Length > historyCount)
            {
                for (int i = 0; i < dirs.Length - historyCount; i++)
                {
                    string dir = dirs[i];

                    PathHelper.DeleteDirectory(dir);
                    Log.InfoFormat("Removed backup '{0}'.", dir);
                }
            }

            var files = Directory.GetFiles(backupRepoPath, "*.zip");
            if (files.Length > historyCount)
            {
                for (int i = 0; i < files.Length - historyCount; i++)
                {
                    string file = files[i];

                    File.Delete(file);
                    Log.InfoFormat("Removed backup '{0}'.", file);
                }
            }
        }

    }
}
