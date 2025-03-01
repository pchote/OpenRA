#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenRA.FileSystem
{
	public sealed class Folder : IReadWritePackage
	{
		public string Name { get; }

		public Folder(string path)
		{
			Name = path;
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);
		}

		public IEnumerable<string> Contents
		{
			get
			{
				// Order may vary on different file systems and it matters for hashing.
				return Directory.GetFiles(Name, "*", SearchOption.TopDirectoryOnly)
					.Concat(Directory.GetDirectories(Name))
					.Select(Path.GetFileName)
					.Order();
			}
		}

		public Stream GetStream(string filename)
		{
			var combined = Path.Combine(Name, filename);
			if (!File.Exists(combined))
				return null;

			try { return File.OpenRead(combined); }
			catch { return null; }
		}

		public bool Contains(string filename)
		{
			var combined = Path.Combine(Name, filename);
			return combined.StartsWith(Name, StringComparison.Ordinal) && File.Exists(combined);
		}

		public IReadOnlyPackage OpenPackage(string filename, FileSystem context)
		{
			var resolvedPath = Platform.ResolvePath(Path.Combine(Name, filename));
			if (Directory.Exists(resolvedPath))
				return new Folder(resolvedPath);

			// Zip files loaded from Folders (and *only* from Folders) can be read-write
			if (ZipFileLoader.TryParseReadWritePackage(resolvedPath, out var readWritePackage))
				return readWritePackage;

			// Other package types can be loaded normally
			var s = GetStream(filename);
			if (s == null)
				return null;

			if (context.TryParsePackage(s, filename, out var package))
				return package;

			s.Dispose();
			return null;
		}

		public void Update(string filename, byte[] contents)
		{
			// HACK: ZipFiles can't be loaded as read-write from a stream, so we are
			// forced to bypass the parent package and load them with their full path
			// in FileSystem.OpenPackage.  Their internal name therefore contains the
			// full parent path too.  We need to be careful to not add a second path
			// prefix to these hacked packages.
			var filePath = filename.StartsWith(Name, StringComparison.Ordinal) ? filename : Path.Combine(Name, filename);

			Directory.CreateDirectory(Path.GetDirectoryName(filePath));
			using (var s = File.Create(filePath))
				s.Write(contents, 0, contents.Length);
		}

		public void Delete(string filename)
		{
			// HACK: ZipFiles can't be loaded as read-write from a stream, so we are
			// forced to bypass the parent package and load them with their full path
			// in FileSystem.OpenPackage.  Their internal name therefore contains the
			// full parent path too.  We need to be careful to not add a second path
			// prefix to these hacked packages.
			var filePath = filename.StartsWith(Name, StringComparison.Ordinal) ? filename : Path.Combine(Name, filename);
			if (Directory.Exists(filePath))
				Directory.Delete(filePath, true);
			else if (File.Exists(filePath))
				File.Delete(filePath);
		}

		public void Dispose() { }
	}
}
