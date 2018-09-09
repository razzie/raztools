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
    class Compressor
    {
        // Returns the maximum compressed size of any block of data with the specified size
        // This function should be used to determine the size of the compression destination buffer
        public static int GetMaxCompressedSize(int size)
        {
            // The header + the original uncompressed data
            return GetHeaderSize(int.MaxValue) + size;
        }

        // Compresses a block of data
        // The source and destination buffers must not overlap and their size must be greater than 0
        // This operation is memory safe
        // On success, returns RESULT_OK and outputs the compressed size
        public Result Compress(byte[] source, byte[] destination, ref int compressedSize)
        {
            Debug.Assert(source != null);
            Debug.Assert(destination != null);

            if (source.Length == 0)
            {
                return Result.RESULT_ERROR_BUFFER_TOO_SMALL;
            }

            int maxCompressedSize = GetMaxCompressedSize(source.Length);
            if (destination.Length < maxCompressedSize)
            {
                return Result.RESULT_ERROR_BUFFER_TOO_SMALL;
            }

            byte[] inputBuffer = source;
            byte[] outputBuffer = destination;
            //byte* outputEnd = outputBuffer + destinationSize;
            int outputEnd = destination.Length;
            //Debug.Assert((inputBuffer + sourceSize <= outputBuffer || inputBuffer >= outputEnd), "The source and destination buffers must not overlap.");

            // Compute the maximum output end pointer
            // We use this to determine whether we should store the data instead of compressing it
            //byte* maxOutputEnd = outputBuffer + maxCompressedSize;
            int maxOutputEnd = maxCompressedSize;
            // Allocate the header
            //byte* outputIterator = outputBuffer;
            int outputIterator = 0;
            outputIterator += GetHeaderSize(maxCompressedSize);

            // Initialize the dictionary
            dictionary_.SetBuffer(inputBuffer);

            // Initialize the control word which contains the literal/match bits
            // The highest bit of a control word is a guard bit, which marks the end of the bit list
            // The guard bit simplifies and speeds up the decoding process, and it 
            const int controlWordBitCount = Common.WORD_SIZE * 8 - 1;
            const uint controlWordGuardBit = 1u << controlWordBitCount;
            uint controlWord = controlWordGuardBit;
            int controlWordBit = 0;

            // Since we do not know the contents of the control words in advance, we allocate space for them and subsequently fill them with data as soon as we can
            // This is necessary because the decoder must encounter a control word *before* the literals and matches it refers to
            // We begin the compressed data with a control word
            int controlWordPointer = outputIterator;
            outputIterator += Common.WORD_SIZE;

            // The match located at the current inputIterator position
            Match match;

            // The match located at the next inputIterator position
            // Initialize it to 'no match', because we are at the beginning of the inputIterator buffer
            // A match with a length of 0 means that there is no match
            Match nextMatch = new Match();
            nextMatch.length = 0;

            // The dictionary matching look-ahead is 1 character, so set the dictionary position to 1
            // We don't have to worry about getting matches beyond the inputIterator, because the dictionary ignores such requests
            dictionary_.Skip();

            // At each position, we select the best match to encode from a list of match candidates provided by the match finder
            Match[] matchCandidates = new Match[Common.MAX_MATCH_CANDIDATE_COUNT];
            int matchCandidateCount;

            // Iterate while there is still data left
            while (dictionary_.Position - 1 < source.Length)
            {
                // Check whether the output is too large
                // During each iteration, we may output up to 8 bytes (2 words), and the compressed stream ends with 4 dummy bytes
                if (outputIterator + 2 * Common.WORD_SIZE + Common.TRAILING_DUMMY_SIZE > maxOutputEnd)
                {
                    // Stop the compression and instead store
                    return Store(source, destination, ref compressedSize);
                }

                // Check whether the control word must be flushed
                if (controlWordBit == controlWordBitCount)
                {
                    // Flush current control word
                    Common.FastWrite(outputBuffer, controlWordPointer, controlWord, Common.WORD_SIZE);

                    // New control word
                    controlWord = controlWordGuardBit;
                    controlWordBit = 0;

                    controlWordPointer = outputIterator;
                    outputIterator += Common.WORD_SIZE;
                }

                // The current match is the previous 'next' match
                match = nextMatch;

                // Find the best match at the next position
                // The dictionary position is automatically incremented
                matchCandidateCount = dictionary_.FindMatches(matchCandidates);
                nextMatch = GetBestMatch(matchCandidates, matchCandidateCount);

                // If we have a match, do not immediately use it, because we may miss an even better match (lazy evaluation)
                // If encoding a literal and the next match has a higher compression ratio than encoding the current match, discard the current match
                if (match.length > 0 && (1 + nextMatch.length) * GetMatchCodedSize(match) > match.length * (1 + GetMatchCodedSize(nextMatch)))
                {
                    match.length = 0;
                }

                // Check whether we must encode a literal or a match
                if (match.length == 0)
                {
                    // Encode a literal (0 control word flag)
                    // In order to efficiently decode literals in runs, the literal bit (0) must differ from the guard bit (1)

                    // The current dictionary position is now two characters ahead of the literal to encode
                    Debug.Assert(outputIterator + 1 <= outputEnd);
                    Common.FastWrite(outputBuffer, outputIterator, inputBuffer[dictionary_.Position - 2], 1);
                    ++outputIterator;
                }
                else
                {
                    // Encode a match (1 control word flag)
                    controlWord |= (uint)(1 << controlWordBit);

                    Debug.Assert(outputIterator + Common.WORD_SIZE <= outputEnd);
                    outputIterator += EncodeMatch(match, outputBuffer, outputIterator);

                    // Skip the matched characters
                    for (int i = 0; i < match.length - 2; ++i)
                    {
                        dictionary_.Skip();
                    }

                    matchCandidateCount = dictionary_.FindMatches(matchCandidates);
                    nextMatch = GetBestMatch(matchCandidates, matchCandidateCount);
                }

                // Next control word bit
                ++controlWordBit;
            }

            // Flush the control word
            Common.FastWrite(outputBuffer, controlWordPointer, controlWord, Common.WORD_SIZE);

            // Output trailing safety dummy bytes
            // This reduces the number of necessary buffer checks during decoding
            Debug.Assert(outputIterator + Common.TRAILING_DUMMY_SIZE <= outputEnd);
            Common.FastWrite(outputBuffer, outputIterator, 0, Common.TRAILING_DUMMY_SIZE);
            outputIterator += Common.TRAILING_DUMMY_SIZE;

            // Done, compute the compressed size
            compressedSize = outputIterator;

            // Encode the header
            Header header;
            header.version = Common.VERSION;
            header.isStored = false;
            header.uncompressedSize = (ulong)source.Length;
            header.compressedSize = (ulong)compressedSize;

            EncodeHeader(header, maxCompressedSize, outputBuffer);

            // Return the compressed size
            return Result.RESULT_OK;
        }

        private static int GetSizeCodedSize(int size)
        {
            if (size <= byte.MaxValue)
            {
                return 1;
            }

            if (size <= ushort.MaxValue)
            {
                return 2;
            }

            //if (size <= uint.MaxValue)
            //{
            //    return 4;
            //}

            //return 8;

            return 4;
        }

        private static int GetHeaderSize(int maxCompressedSize)
        {
            return 1 + 2 * GetSizeCodedSize(maxCompressedSize);
        }

        // Store the source
        private Result Store(byte[] source, byte[] destination, ref int compressedSize)
        {
            byte[] outputBuffer = destination;
            int outputIterator = 0;

            // Encode the header
            int maxCompressedSize = GetMaxCompressedSize(source.Length);
            int headerSize = GetHeaderSize(maxCompressedSize);

            compressedSize = headerSize + source.Length;

            Header header;

            header.version = Common.VERSION;
            header.isStored = true;
            header.uncompressedSize = (ulong)source.Length;
            header.compressedSize = (ulong)compressedSize;

            EncodeHeader(header, maxCompressedSize, destination);
            outputIterator += headerSize;

            // Store the data
            Array.Copy(source, 0, outputBuffer, outputIterator, source.Length);

            return Result.RESULT_OK;
        }

        private Match GetBestMatch(Match[] matchCandidates, int matchCandidateCount)
        {
            Match bestMatch = new Match();
            bestMatch.length = 0;

            // Select the longest match which can be coded efficiently (coded size is less than the length)
            for (int i = matchCandidateCount - 1; i >= 0; --i)
            {
                if (matchCandidates[i].length > GetMatchCodedSize(matchCandidates[i]))
                {
                    bestMatch = matchCandidates[i];
                    break;
                }
            }

            return bestMatch;
        }

        private int EncodeMatch(Match match, byte[] destination, int destinationOffset)
        {
            Debug.Assert(match.length <= Common.MAX_MATCH_LENGTH);
            Debug.Assert(match.length == 0 || match.offset < Common.DICTIONARY_SIZE);

            uint word;
            int size;

            uint lengthCode = (uint)(match.length - Common.MIN_MATCH_LENGTH);
            uint offsetCode = (uint)(match.offset);

            if (lengthCode == 0 && offsetCode < 64)
            {
                word = offsetCode << 2; // 00
                size = 1;
            }
            else if (lengthCode == 0 && offsetCode < 16384)
            {
                word = (offsetCode << 2) | 1; // 01
                size = 2;
            }
            else if (lengthCode < 16 && offsetCode < 1024)
            {
                word = (offsetCode << 6) | (lengthCode << 2) | 2; // 10
                size = 2;
            }
            else if (lengthCode < 32 && offsetCode < 65536)
            {
                word = (offsetCode << 8) | (lengthCode << 3) | 3; // 11
                size = 3;
            }
            else
            {
                word = (offsetCode << 11) | (lengthCode << 3) | 7; // 111
                size = 4;
            }

            if (destination != null)
            {
                Common.FastWrite(destination, destinationOffset, word, size);
            }

            return size;
        }

        private int GetMatchCodedSize(Match match)
        {
            return EncodeMatch(match, null, 0);
        }

        private void EncodeHeader(Header header, int maxCompressedSize, byte[] destination)
        {
            Debug.Assert(header.version < 8);

            // Encode the attribute byte
            uint attributes = (uint)header.version;

            uint sizeCodedSize = (uint)GetSizeCodedSize(maxCompressedSize);
            attributes |= (sizeCodedSize - 1) << 3;

            if (header.isStored)
            {
                attributes |= 128;
            }

            var iterator = 0;
            destination[iterator++] = (byte)attributes;

            // Encode the uncompressed and compressed sizes
            switch (sizeCodedSize)
            {
                case 1:
                    destination[iterator] = (byte)header.uncompressedSize;
                    destination[iterator + sizeCodedSize] = (byte)header.compressedSize;
                    break;

                case 2:
                    Array.Copy(BitConverter.GetBytes((ushort)header.uncompressedSize), 0, destination, iterator, sizeof(ushort));
                    Array.Copy(BitConverter.GetBytes((ushort)header.compressedSize), 0, destination, iterator + sizeCodedSize, sizeof(ushort));
                    break;

                case 4:
                    Array.Copy(BitConverter.GetBytes((uint)header.uncompressedSize), 0, destination, iterator, sizeof(uint));
                    Array.Copy(BitConverter.GetBytes((uint)header.compressedSize), 0, destination, iterator + sizeCodedSize, sizeof(uint));
                    break;

                case 8:
                    Array.Copy(BitConverter.GetBytes((ulong)header.uncompressedSize), 0, destination, iterator, sizeof(ulong));
                    Array.Copy(BitConverter.GetBytes((ulong)header.compressedSize), 0, destination, iterator + sizeCodedSize, sizeof(ulong));
                    break;
            }
        }

        private Dictionary dictionary_ = new Dictionary();
    }
}
