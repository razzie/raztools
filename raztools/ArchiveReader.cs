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
    public class ArchiveReader
    {
        public class FileInfo
        {
            public string FileName { get; set; }
            public ulong Size { get; set; }
        }

        class InternalFileInfo
        {
            public string FileName { get; set; }
            ulong Position { get; set; }
            ulong OriginalSize { get; set; }
            ulong CompressedSize { get; set; }
        }

        public ArchiveReader(string archive)
        {

        }

        public int FileCount
        {
            get { return m_files.Count; }
        }

        int? GetFileIndex(string filename)
        {
            return null;
        }

        FileInfo GetFileInfo(string filename)
        {
            return null;
        }

        byte[] Decompress(int fileindex)
        {
            return null;
        }

        private FileStream m_archive;
        private List<InternalFileInfo> m_files = new List<InternalFileInfo>();
    }
}
