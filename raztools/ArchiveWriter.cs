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
using System.IO;
using System.Text;

namespace raztools
{
    public class ArchiveWriter : IDisposable
    {
        public ArchiveWriter(string archive, bool append = true)
        {
            m_archive = new FileStream(archive, append ? FileMode.Append : FileMode.Create);
        }

        public void CompressDirectory(string directory)
        {
            CompressDirectory(directory, directory);
        }

        public void CompressDirectory(string directory, string root)
        {
            var dir = new DirectoryInfo(directory);
            var rootdir = new DirectoryInfo(root);

            foreach (var file in dir.GetFiles())
            {
                string relative_filename = new Uri(rootdir.FullName).MakeRelativeUri(new Uri(file.FullName)).LocalPath;
                Compress(file.FullName, relative_filename);
            }

            foreach (var subdir in dir.GetDirectories())
            {
                CompressDirectory(subdir.FullName, root);
            }
        }

        public void Compress(string source_filename, string dest_filename)
        {
            var uncompressed = File.ReadAllBytes(source_filename);
            var compressed_size = Doboz.Compressor.GetMaxCompressedSize(uncompressed.Length);
            var compressed = new byte[compressed_size];

            new Doboz.Compressor().Compress(uncompressed, compressed, ref compressed_size);

            lock (m_archive)
            {
                using (var writer = new BinaryWriter(m_archive, Encoding.UTF8, true))
                {
                    var filename = Encoding.UTF8.GetBytes(dest_filename);
                    writer.Write((uint)filename.Length);
                    writer.Write(filename);

                    writer.Write((ulong)uncompressed.Length);
                    writer.Write((ulong)compressed_size);
                    writer.Write(compressed, 0, compressed_size);

                    m_archive.Flush();
                }
            }
        }

        public void Dispose()
        {
            m_archive.Close();
        }

        private FileStream m_archive;
    }
}
