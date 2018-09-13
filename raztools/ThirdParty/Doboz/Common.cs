/*
 * Doboz Data Compression Library
 * Copyright (C) 2010-2011 Attila T. Afra <attila.afra@gmail.com>
 * 
 * This software is provided 'as-is', without any express or implied warranty. In no event will
 * the authors be held liable for any damages arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose, including commercial
 * applications, and to alter it and redistribute it freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not claim that you wrote the
 *    original software. If you use this software in a product, an acknowledgment in the product
 *    documentation would be appreciated but is not required.
 * 2. Altered source versions must be plainly marked as such, and must not be misrepresented as
 *    being the original software.
 * 3. This notice may not be removed or altered from any source distribution.
 */

/*
 * C# port by Gábor Görzsöny <gabor@gorzsony.com>
 */

using System;
using System.Diagnostics;

namespace Doboz
{
    enum Result
    {
        RESULT_OK,
        RESULT_ERROR_BUFFER_TOO_SMALL,
        RESULT_ERROR_CORRUPTED_DATA,
        RESULT_ERROR_UNSUPPORTED_VERSION,
    };

    struct Match
    {
        public int length;
        public int offset;
    };

    struct Header
    {
        public ulong uncompressedSize;
        public ulong compressedSize;
        public int version;
        public bool isStored;
    };


    static class Common
    {
        public const int VERSION = 0; // encoding format

        public const int WORD_SIZE = 4; // uint32_t

        public const int MIN_MATCH_LENGTH = 3;
        public const int MAX_MATCH_LENGTH = 255 + MIN_MATCH_LENGTH;
        public const int MAX_MATCH_CANDIDATE_COUNT = 128;
        public const int DICTIONARY_SIZE = 1 << 21; // 2 MB, must be a power of 2!

        public const int TAIL_LENGTH = 2 * WORD_SIZE; // prevents fast write operations from writing beyond the end of the buffer during decoding
        public const int TRAILING_DUMMY_SIZE = WORD_SIZE; // safety trailing bytes which decrease the number of necessary buffer checks


        // Reads up to 4 bytes and returns them in a word
        // WARNING: May read more bytes than requested!
        public static uint FastRead(byte[] source, int sourceOffset, int size)
        {
            Debug.Assert(size <= WORD_SIZE);

            switch (size)
            {
                case 4:
                    return BitConverter.ToUInt32(source, sourceOffset);

                case 3:
                    return BitConverter.ToUInt32(source, sourceOffset);

                case 2:
                    return BitConverter.ToUInt16(source, sourceOffset);

                case 1:
                    return source[sourceOffset];

                default:
                    return 0; // dummy
            }
        }

        // Writes up to 4 bytes specified in a word
        // WARNING: May write more bytes than requested!
        public static void FastWrite(byte[] destination, int destinationOffset, uint word, int size)
        {
            Debug.Assert(size <= WORD_SIZE);

            switch (size)
            {
                case 4:
                    Array.Copy(BitConverter.GetBytes(word), 0, destination, destinationOffset, sizeof(uint));
                    break;

                case 3:
                    Array.Copy(BitConverter.GetBytes(word), 0, destination, destinationOffset, sizeof(uint));
                    break;

                case 2:
                    Array.Copy(BitConverter.GetBytes((ushort)word), 0, destination, destinationOffset, sizeof(ushort));
                    break;

                case 1:
                    destination[destinationOffset] = (byte)word;
                    break;
            }
        }
    }
}
