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

namespace raztools.ThirdParty.Doboz
{
    struct CompressionInfo
    {
        public ulong uncompressedSize;
        public ulong compressedSize;
        public int version;
    };

    class Decompressor
    {
        // Decompresses a block of data
        // The source and destination buffers must not overlap
        // This operation is memory safe
        // On success, returns RESULT_OK
        public Result Decompress(byte[] source, byte[] destination)
        {
            Debug.Assert(source != null);
            Debug.Assert(destination != null);

            byte[] inputBuffer = source;
            int inputIterator = 0;

            byte[] outputBuffer = destination;
            int outputIterator = 0;

            //Debug.Assert((inputBuffer + sourceSize <= outputBuffer || inputBuffer >= outputBuffer + destinationSize),
            //    "The source and destination buffers must not overlap.");

            // Decode the header
            Header header = new Header();
            int headerSize = 0;
            Result decodeHeaderResult = DecodeHeader(ref header, source, ref headerSize);

            if (decodeHeaderResult != Result.RESULT_OK)
            {
                return decodeHeaderResult;
            }

            inputIterator += headerSize;

            if (header.version != Common.VERSION)
            {
                return Result.RESULT_ERROR_UNSUPPORTED_VERSION;
            }

            // Check whether the supplied buffers are large enough
            if ((ulong)source.Length < header.compressedSize || (ulong)destination.Length < header.uncompressedSize)
            {
                return Result.RESULT_ERROR_BUFFER_TOO_SMALL;
            }

            int uncompressedSize = (int)(header.uncompressedSize);

            // If the data is simply stored, copy it to the destination buffer and we're done
            if (header.isStored)
            {
                Array.Copy(inputBuffer, inputIterator, outputBuffer, 0, uncompressedSize);
                return Result.RESULT_OK;
            }

            int inputEnd = (int)header.compressedSize;
            int outputEnd = uncompressedSize;

            // Compute pointer to the first byte of the output 'tail'
            // Fast write operations can be used only before the tail, because those may write beyond the end of the output buffer
            int outputTail = (uncompressedSize > Common.TAIL_LENGTH) ? (outputEnd - Common.TAIL_LENGTH) : 0;

            // Initialize the control word to 'empty'
            uint controlWord = 1;

            // Decoding loop
            for (;;)
            {
                // Check whether there is enough data left in the input buffer
                // In order to decode the next literal/match, we have to read up to 8 bytes (2 words)
                // Thanks to the trailing dummy, there must be at least 8 remaining input bytes
                if (inputIterator + 2 * Common.WORD_SIZE > inputEnd)
                {
                    return Result.RESULT_ERROR_CORRUPTED_DATA;
                }

                // Check whether we must read a control word
                if (controlWord == 1)
                {
                    Debug.Assert(inputIterator + Common.WORD_SIZE <= inputEnd);
                    controlWord = Common.FastRead(inputBuffer, inputIterator, Common.WORD_SIZE);
                    inputIterator += Common.WORD_SIZE;
                }

                // Detect whether it's a literal or a match
                if ((controlWord & 1) == 0)
                {
                    // It's a literal

                    // If we are before the tail, we can safely use fast writing operations
                    if (outputIterator < outputTail)
                    {
                        // We copy literals in runs of up to 4 because it's faster than copying one by one

                        // Copy implicitly 4 literals regardless of the run length
                        Debug.Assert(inputIterator + Common.WORD_SIZE <= inputEnd);
                        Debug.Assert(outputIterator + Common.WORD_SIZE <= outputEnd);
                        Common.FastWrite(outputBuffer, outputIterator, Common.FastRead(inputBuffer, inputIterator, Common.WORD_SIZE), Common.WORD_SIZE);

                        // Get the run length using a lookup table
                        //static const byte[] literalRunLengthTable = new byte[16] { 4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0 };
                        int runLength = literalRunLengthTable_[controlWord & 0xf];

                        // Advance the inputBuffer and outputBuffer pointers with the run length
                        inputIterator += runLength;
                        outputIterator += runLength;

                        // Consume as much control word bits as the run length
                        controlWord >>= runLength;
                    }
                    else
                    {
                        // We have reached the tail, we cannot output literals in runs anymore
                        // Output all remaining literals
                        while (outputIterator < outputEnd)
                        {
                            // Check whether there is enough data left in the input buffer
                            // In order to decode the next literal, we have to read up to 5 bytes
                            if (inputIterator + Common.WORD_SIZE + 1 > inputEnd)
                            {
                                return Result.RESULT_ERROR_CORRUPTED_DATA;
                            }

                            // Check whether we must read a control word
                            if (controlWord == 1)
                            {
                                Debug.Assert(inputIterator + Common.WORD_SIZE <= inputEnd);
                                controlWord = Common.FastRead(inputBuffer, inputIterator, Common.WORD_SIZE);
                                inputIterator += Common.WORD_SIZE;
                            }

                            // Output one literal
                            // We cannot use fast read/write functions
                            Debug.Assert(inputIterator + 1 <= inputEnd);
                            Debug.Assert(outputIterator + 1 <= outputEnd);
                            outputBuffer[outputIterator++] = inputBuffer[inputIterator++]; // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! ++i vagy i++ ?

                            // Next control word bit
                            controlWord >>= 1;
                        }

                        // Done
                        return Result.RESULT_OK;
                    }
                }
                else
                {
                    // It's a match

                    // Decode the match
                    Debug.Assert(inputIterator + Common.WORD_SIZE <= inputEnd);
                    Match match = new Match();
                    inputIterator += DecodeMatch(ref match, inputBuffer, inputIterator);

                    // Copy the matched string
                    // In order to achieve high performance, we copy characters in groups of machine words
                    // Overlapping matches require special care
                    int matchString = outputIterator - match.offset;

                    // Check whether the match is out of range
                    if (matchString < 0 || outputIterator + match.length > outputTail)
                    {
                        return Result.RESULT_ERROR_CORRUPTED_DATA;
                    }

                    int i = 0;

                    if (match.offset < Common.WORD_SIZE)
                    {
                        // The match offset is less than the word size
                        // In order to correctly handle the overlap, we have to copy the first three bytes one by one
                        do
                        {
                            Debug.Assert(matchString + i >= 0);
                            Debug.Assert(matchString + i + Common.WORD_SIZE <= outputEnd);
                            Debug.Assert(outputIterator + i + Common.WORD_SIZE <= outputEnd);
                            Common.FastWrite(outputBuffer, outputIterator + i, Common.FastRead(outputBuffer, matchString + i, 1), 1); // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! 2. input v output?
                            ++i;
                        }
                        while (i < 3);

                        // With this trick, we increase the distance between the source and destination pointers
                        // This enables us to use fast copying for the rest of the match
                        matchString -= 2 + (match.offset & 1);
                    }

                    // Fast copying
                    // There must be no overlap between the source and destination words

                    do
                    {
                        Debug.Assert(matchString + i >= 0);
                        Debug.Assert(matchString + i + Common.WORD_SIZE <= outputEnd);
                        Debug.Assert(outputIterator + i + Common.WORD_SIZE <= outputEnd);
                        Common.FastWrite(outputBuffer, outputIterator + i, Common.FastRead(outputBuffer, matchString + i, Common.WORD_SIZE), Common.WORD_SIZE);
                        i += Common.WORD_SIZE;
                    }
                    while (i < match.length);

                    outputIterator += match.length;

                    // Next control word bit
                    controlWord >>= 1;
                }
            }
        }

        // Retrieves information about a compressed block of data
        // This operation is memory safe
        // On success, returns RESULT_OK and outputs the compression information
        public Result GetCompressionInfo(byte[] source, ref CompressionInfo compressionInfo)
        {
            Debug.Assert(source != null);

            // Decode the header
            Header header = new Header();
            int headerSize = 0;
            Result decodeHeaderResult = DecodeHeader(ref header, source, ref headerSize);

            if (decodeHeaderResult != Result.RESULT_OK)
            {
                return decodeHeaderResult;
            }

            // Return the requested info
            compressionInfo.uncompressedSize = header.uncompressedSize;
            compressionInfo.compressedSize = header.compressedSize;
            compressionInfo.version = header.version;

            return Result.RESULT_OK;

        }

        // Decodes a match and returns its size in bytes
        private int DecodeMatch(ref Match match, byte[] source, int sourceOffset)
        {
            // Read the maximum number of bytes a match is coded in (4)
            uint word = Common.FastRead(source, sourceOffset, Common.WORD_SIZE);

            // Compute the decoding lookup table entry index: the lowest 3 bits of the encoded match
            uint i = word & 7;

            // Compute the match offset and length using the lookup table entry
            match.offset = (int)((word & lut_[i].mask) >> lut_[i].offsetShift);
            match.length = (int)(((word >> lut_[i].lengthShift) & lut_[i].lengthMask) + Common.MIN_MATCH_LENGTH);

            return lut_[i].size;
        }

        // Decodes a header and returns its size in bytes
        // If the header is not valid, the function returns 0
        private Result DecodeHeader(ref Header header, byte[] source, ref int headerSize)
        {
            // Decode the attribute bytes
            if (source.Length < 1)
            {
                return Result.RESULT_ERROR_BUFFER_TOO_SMALL;
            }

            int iterator = 0;
            uint attributes = source[iterator++];

            header.version = (int)(attributes & 7);
            int sizeCodedSize = (int)((attributes >> 3) & 7) + 1;

            // Compute the size of the header
            headerSize = 1 + 2 * sizeCodedSize;

            if (source.Length < headerSize)
            {
                return Result.RESULT_ERROR_BUFFER_TOO_SMALL;
            }

            header.isStored = (attributes & 128) != 0;

            // Decode the uncompressed and compressed sizes
            switch (sizeCodedSize)
            {
                case 1:
                    header.uncompressedSize = source[iterator];
                    header.compressedSize = source[iterator + sizeCodedSize];
                    break;

                case 2:
                    header.uncompressedSize = BitConverter.ToUInt16(source, iterator);
                    header.compressedSize = BitConverter.ToUInt16(source, iterator + sizeCodedSize);
                    break;

                case 4:
                    header.uncompressedSize = BitConverter.ToUInt32(source, iterator);
                    header.compressedSize = BitConverter.ToUInt32(source, iterator + sizeCodedSize);
                    break;

                case 8:
                    header.uncompressedSize = BitConverter.ToUInt64(source, iterator);
                    header.compressedSize = BitConverter.ToUInt64(source, iterator + sizeCodedSize);
                    break;

                default:
                    return Result.RESULT_ERROR_CORRUPTED_DATA;
            }

            return Result.RESULT_OK;
        }

        private struct LookupTable
        {
            public uint mask; // the mask for the entire encoded match
            public byte offsetShift;
            public byte lengthMask;
            public byte lengthShift;
            public sbyte size; // the size of the encoded match in bytes
        }

        private static readonly sbyte[] literalRunLengthTable_ = new sbyte[16] { 4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0 };
        private static readonly LookupTable[] lut_ = new LookupTable[]
        {
            new LookupTable { mask = 0xff,       offsetShift =  2, lengthMask =   0, lengthShift = 0, size = 1 }, // (0)00
		    new LookupTable { mask = 0xffff,     offsetShift =  2, lengthMask =   0, lengthShift = 0, size = 2 }, // (0)01
		    new LookupTable { mask = 0xffff,     offsetShift =  6, lengthMask =  15, lengthShift = 2, size = 2 }, // (0)10
		    new LookupTable { mask = 0xffffff,   offsetShift =  8, lengthMask =  31, lengthShift = 3, size = 3 }, // (0)11
		    new LookupTable { mask = 0xff,       offsetShift =  2, lengthMask =   0, lengthShift = 0, size = 1 }, // (1)00 = (0)00
		    new LookupTable { mask = 0xffff,     offsetShift =  2, lengthMask =   0, lengthShift = 0, size = 2 }, // (1)01 = (0)01
		    new LookupTable { mask = 0xffff,     offsetShift =  6, lengthMask =  15, lengthShift = 2, size = 2 }, // (1)10 = (0)10
		    new LookupTable { mask = 0xffffffff, offsetShift = 11, lengthMask = 255, lengthShift = 3, size = 4 }, // 111
        };
    }
}
