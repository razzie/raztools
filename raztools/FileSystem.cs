/*
Copyright (C) Gábor "Razzie" Görzsöny
Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:
The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE
*/

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace raztools
{
    public static class FileSystem
    {
        public class Container : IDisposable
        {
            private ConcurrentDictionary<string, byte[]> Cache { get; } = new ConcurrentDictionary<string, byte[]>();
            private DirectoryInfo[] Directories { get; set; } = new DirectoryInfo[0];
            private ArchiveReader[] Archives { get; set; } = new ArchiveReader[0];

            public string Name { get; private set; }

            public Container(string name)
            {
                Name = name;
            }

            ~Container()
            {
                Dispose();
            }

            public void AddDirectory(string dir)
            {
                lock (Directories)
                {
                    Directories = Directories.Concat(new[] { new DirectoryInfo(dir) }).ToArray();
                }
            }

            public void AddArchive(string archive)
            {
                lock (Archives)
                {
                    Archives = Archives.Concat(new[] { FileSystem.GetArchive(archive) }).ToArray();
                }
            }

            public byte[] GetFileData(string file, bool cache_file = false)
            {
                if (Cache.TryGetValue(file, out byte[] data))
                    return data;
                
                foreach (var dir in Directories)
                {
                    var path = Path.Combine(dir.FullName, file);
                    var file_info = new FileInfo(path);
                    if (file_info.Exists)
                    {
                        data = File.ReadAllBytes(file_info.FullName);

                        if (cache_file && data != null)
                            Cache.TryAdd(file, data);

                        return data;
                    }
                }

                foreach (var archive in Archives)
                {
                    var result = archive.GetFile(file);
                    if (result != null)
                    {
                        data = archive.Decompress(result);

                        if (cache_file && data != null)
                            Cache.TryAdd(file, data);

                        return data;
                    }
                }

                return null;
            }

            public void ClearCache()
            {
                Cache.Clear();
            }

            public void Dispose()
            {
                Cache.Clear();
                Directories = null;
                Archives = null;
            }
        }

        static private ConcurrentDictionary<string, Container> Containers { get; } = new ConcurrentDictionary<string, Container>();
        static internal ConcurrentDictionary<string, ArchiveReader> Archives { get; } = new ConcurrentDictionary<string, ArchiveReader>();

        static internal ArchiveReader GetArchive(string archive_file)
        {
            if (Archives.TryGetValue(archive_file, out ArchiveReader archive))
                return archive;

            archive = new ArchiveReader(archive_file);
            Archives.TryAdd(archive_file, archive);
            return archive;
        }

        static public Container GetContainer(string name)
        {
            if (Containers.TryGetValue(name, out Container container))
                return container;

            container = new Container(name);
            Containers.TryAdd(name, container);
            return container;
        }

        static public void RemoveContainer(string name)
        {
            Containers.TryRemove(name, out Container container);
        }
    }
}
