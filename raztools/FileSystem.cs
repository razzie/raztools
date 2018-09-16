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

using System.Collections.Generic;
using System.IO;

namespace raztools
{
    public static class FileSystem
    {
        public class Container
        {
            private Dictionary<string, byte[]> Cache { get; } = new Dictionary<string, byte[]>();
            private List<DirectoryInfo> Directories { get; } = new List<DirectoryInfo>();
            private List<ArchiveReader> Archives { get; } = new List<ArchiveReader>();

            public string Name { get; private set; }

            public Container(string name)
            {
                Name = name;
            }

            public void AddDirectory(string dir)
            {
                Directories.Add(new DirectoryInfo(dir));
            }

            public void AddArchive(string archive)
            {
                Archives.Add(FileSystem.GetArchive(archive));
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
                            Cache.Add(file, data);

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
                            Cache.Add(file, data);

                        return data;
                    }
                }

                return null;
            }

            public void ClearCache()
            {
                Cache.Clear();
            }
        }

        static private Dictionary<string, Container> Containers { get; } = new Dictionary<string, Container>();
        static internal Dictionary<string, ArchiveReader> Archives { get; } = new Dictionary<string, ArchiveReader>();

        static internal ArchiveReader GetArchive(string archive_file)
        {
            if (Archives.TryGetValue(archive_file, out ArchiveReader archive))
                return archive;

            archive = new ArchiveReader(archive_file);
            Archives.Add(archive_file, archive);
            return archive;
        }

        static public Container GetContainer(string name)
        {
            if (Containers.TryGetValue(name, out Container container))
                return container;

            container = new Container(name);
            Containers.Add(name, container);
            return container;
        }

        static public void RemoveContainer(string name)
        {
            Containers.Remove(name);
        }
    }
}
