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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace raztools
{
    public class ArchiveReader : IDisposable
    {
        public class FileInfo
        {
            public string FileName { get; private set; }
            public ulong Size { get; private set; }
            public int Index { get; private set; }

            public FileInfo(string filename, ulong size, int index)
            {
                FileName = filename;
                Size = size;
                Index = index;
            }
        }

        class InternalFileInfo
        {
            public string FileName { get; set; }
            public ulong OriginalSize { get; set; }
            public ulong CompressedSize { get; set; }
            public long Position { get; set; }
        }

        public ArchiveReader(string archive)
        {
            m_archive = new FileStream(archive, FileMode.Open);

            var end = m_archive.Length;
            using (var reader = new BinaryReader(m_archive, Encoding.UTF8, true))
            {
                while (m_archive.Position != end)
                {
                    var info = new InternalFileInfo();

                    var filename_len = reader.ReadUInt32();
                    var filename = new byte[filename_len];
                    reader.Read(filename, 0, filename.Length);
                    info.FileName = Encoding.UTF8.GetString(filename);

                    info.OriginalSize = reader.ReadUInt64();
                    info.CompressedSize = reader.ReadUInt64();
                    info.Position = m_archive.Position;
                    m_files.Add(info);

                    m_archive.Position = info.Position + (long)info.CompressedSize;
                }
            }
        }

        public IEnumerable<FileInfo> Files
        {
            get
            {
                foreach (var file in m_files)
                {
                    yield return new FileInfo(file.FileName, file.OriginalSize, m_files.IndexOf(file));
                }
            }
        }

        public int FileCount
        {
            get { return m_files.Count; }
        }

        public FileInfo GetFile(string filename)
        {
            var file = m_files.First(f => f.FileName == filename);
            if (file != null)
            {
                return new FileInfo(file.FileName, file.OriginalSize, m_files.IndexOf(file));
            }

            return null;
        }

        public byte[] Decompress(FileInfo file)
        {
            var info = m_files[file.Index];
            var compressed = new byte[info.CompressedSize];
            var decompressed = new byte[info.OriginalSize];

            lock (m_archive)
            {
                m_archive.Position = info.Position;
                m_archive.Read(compressed, 0, compressed.Length);
            }

            new Doboz.Decompressor().Decompress(compressed, decompressed);
            return decompressed;
        }

        public void Dispose()
        {
            m_archive.Close();
        }

        private FileStream m_archive;
        private List<InternalFileInfo> m_files = new List<InternalFileInfo>();
    }
}
