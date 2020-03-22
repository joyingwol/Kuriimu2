﻿using System;
using System.IO;
using System.Text.RegularExpressions;
using Kontract.Interfaces.FileSystem;
using Kontract.Interfaces.Managers;
using Kontract.Interfaces.Plugins.State;
using Kontract.Models;
using Kontract.Models.IO;
using Kore.FileSystem.Implementations;

namespace Kore.Factories
{
    // TODO: Make internal again
    /// <summary>
    /// Contains methods to create specific <see cref="IFileSystem"/> implementations.
    /// </summary>
    public static class FileSystemFactory
    {
        /// <summary>
        /// Create a <see cref="PhysicalFileSystem"/> based on the directory in <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The path of the physical file system.</param>
        /// <param name="streamManager">The stream manager for this file system.</param>
        /// <returns>The created <see cref="PhysicalFileSystem"/> for this folder.</returns>
        public static IFileSystem CreatePhysicalFileSystem(UPath path, IStreamManager streamManager)
        {
            if (!IsRooted(path))
                throw new InvalidOperationException($"Path {path} is not rooted to a drive.");

            var internalPath = ReplaceWindowsDrive(path);
            var physicalPath = ReplaceMountPoint(path);

            if (!Directory.Exists(physicalPath.FullName))
                Directory.CreateDirectory(physicalPath.FullName);

            var fileSystem = (IFileSystem)new PhysicalFileSystem(streamManager);
            fileSystem = new SubFileSystem(fileSystem, internalPath.FullName.Substring(0, 7));
            fileSystem = new SubFileSystem(fileSystem, internalPath.FullName.Substring(6));

            return fileSystem;
        }

        /// <summary>
        /// Create a <see cref="AfiFileSystem"/> based on the given <see cref="IArchiveState"/>.
        /// </summary>
        /// <param name="archiveState"><see cref="IArchiveState"/> to create the file system from.</param>
        /// <param name="path">The path of the virtual file system.</param>
        /// <param name="streamManager">The stream manager for this file system.</param>
        /// <returns>The created <see cref="IArchiveState"/> for this state.</returns>
        public static IFileSystem CreateAfiFileSystem(IArchiveState archiveState, UPath path, IStreamManager streamManager)
        {
            var fileSystem = (IFileSystem)new AfiFileSystem(archiveState, streamManager);
            if (path != UPath.Empty)
                fileSystem = new SubFileSystem(fileSystem, path);

            return fileSystem;
        }

        /// <summary>
        /// Clone a <see cref="IFileSystem"/> with a new sub path.
        /// </summary>
        /// <param name="fileSystem"><see cref="IFileSystem"/> to clone.</param>
        /// <param name="path">The sub path of the cloned file system.</param>
        /// <param name="streamManager">The stream manager for this file system.</param>
        /// <returns>The cloned <see cref="IFileSystem"/>.</returns>
        public static IFileSystem CloneFileSystem(IFileSystem fileSystem, UPath path, IStreamManager streamManager)
        {
            var newFileSystem = fileSystem.Clone(streamManager);
            if (path != UPath.Empty)
                newFileSystem = new SubFileSystem(newFileSystem, path);

            return newFileSystem;
        }

        /// <summary>
        /// Checks if the path rooted to either a windows drive or <see cref="IFileSystem"/> compatible mount point.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>If the path is rooted.</returns>
        private static bool IsRooted(UPath path)
        {
            var mntRegex = new Regex("^/mnt/[a-zA-Z]/");
            var driveRegex = new Regex(@"^[a-zA-Z]:[/\\]");

            return mntRegex.IsMatch(path.FullName) || driveRegex.IsMatch(path.FullName);
        }

        /// <summary>
        /// Replaces a windows drive root (eg C:/) with a <see cref="IFileSystem"/> compatible mount point.
        /// </summary>
        /// <param name="path">The path to modify.</param>
        /// <returns>The modified path.</returns>
        private static UPath ReplaceWindowsDrive(UPath path)
        {
            var driveRegex = new Regex(@"^[a-zA-Z]:[/\\]");
            if (!driveRegex.IsMatch(path.FullName))
                return path;

            var driveLetter = path.FullName[0];
            return new UPath(driveRegex.Replace(path.FullName, $"/mnt/{char.ToLower(driveLetter)}/"));
        }

        /// <summary>
        /// Replaces a <see cref="IFileSystem"/> compatible mount point with a windows drive root (eg C:/)
        /// </summary>
        /// <param name="path">The path to modify.</param>
        /// <returns>The modified path.</returns>
        private static UPath ReplaceMountPoint(UPath path)
        {
            var mntRegex = new Regex("^/mnt/[a-zA-Z]/");
            if (!mntRegex.IsMatch(path.FullName))
                return path;

            var driveLetter = path.FullName[5];
            return new UPath(mntRegex.Replace(path.FullName, $"{char.ToUpper(driveLetter)}:/"));
        }
    }
}