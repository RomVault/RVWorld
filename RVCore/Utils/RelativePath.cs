/******************************************************
 *     ROMVault3 is written by Gordon J.              *
 *     Contact gordon@romvault.com                    *
 *     Copyright 2019                                 *
 ******************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace RVCore.Utils
{
    public static class RelativePath
    {
        /// <summary>
        ///     Creates a relative path from one file
        ///     or folder to another.
        /// </summary>
        /// <param name="fromDirectory">
        ///     Contains the directory that defines the
        ///     start of the relative path.
        /// </param>
        /// <param name="toPath">
        ///     Contains the path that defines the
        ///     endpoint of the relative path.
        /// </param>
        /// <returns>
        ///     The relative path from the start
        ///     directory to the end path.
        /// </returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string MakeRelative(string fromDirectory, string toPath)
        {
            if (fromDirectory == null)
            {
                throw new ArgumentNullException("fromDirectory");
            }

            if (toPath == null)
            {
                throw new ArgumentNullException("toPath");
            }

            bool isRooted = Path.IsPathRooted(fromDirectory) && Path.IsPathRooted(toPath);

            if (isRooted)
            {
                bool isDifferentRoot = string.Compare(Path.GetPathRoot(fromDirectory), Path.GetPathRoot(toPath), StringComparison.OrdinalIgnoreCase) != 0;

                if (isDifferentRoot)
                {
                    return toPath;
                }
            }

            List<string> relativePath = new List<string>();
            string[] fromDirectories = fromDirectory.Split(Path.DirectorySeparatorChar);

            string[] toDirectories = toPath.Split(Path.DirectorySeparatorChar);

            int length = Math.Min(fromDirectories.Length, toDirectories.Length);

            int lastCommonRoot = -1;

            // find common root
            for (int x = 0; x < length; x++)
            {
                if (string.Compare(fromDirectories[x], toDirectories[x], StringComparison.OrdinalIgnoreCase) != 0)
                {
                    break;
                }

                lastCommonRoot = x;
            }

            if (lastCommonRoot == -1)
            {
                return toPath;
            }

            // add relative folders in from path
            for (int x = lastCommonRoot + 1; x < fromDirectories.Length; x++)
            {
                if (fromDirectories[x].Length > 0)
                {
                    relativePath.Add("..");
                }
            }

            // add to folders to path
            for (int x = lastCommonRoot + 1; x < toDirectories.Length; x++)
            {
                relativePath.Add(toDirectories[x]);
            }

            // create relative path
            string[] relativeParts = new string[relativePath.Count];
            relativePath.CopyTo(relativeParts, 0);

            string newPath = string.Join(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), relativeParts);

            return newPath;
        }
    }
}