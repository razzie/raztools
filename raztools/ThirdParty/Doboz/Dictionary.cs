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
    class Dictionary : IDisposable
    {
        public Dictionary()
        {
            hashTable_ = null;
            children_ = null;

            Debug.Assert(INVALID_POSITION < 0);
            Debug.Assert(REBASE_THRESHOLD > Common.DICTIONARY_SIZE && REBASE_THRESHOLD % Common.DICTIONARY_SIZE == 0);
        }

        public void Dispose()
        {
            hashTable_ = null;
            children_ = null;
        }

        public void SetBuffer(byte[] buffer)
        {
            // Set the buffer
            buffer_ = buffer;
            absolutePosition_ = 0;

            // Compute the matchable buffer length
            if (buffer_.Length > Common.TAIL_LENGTH + Common.MIN_MATCH_LENGTH)
            {
                matchableBufferLength_ = buffer_.Length - (Common.TAIL_LENGTH + Common.MIN_MATCH_LENGTH);
            }
            else
            {
                matchableBufferLength_ = 0;
            }

            // Since we always store 32-bit positions in the dictionary, we need relative positions in order to support buffers larger then 2 GB
            // This can be possible, because the difference between any two positions stored in the dictionary never exceeds the size of the dictionary
            // We don't store larger (64-bit) positions, because that can significantly degrade performance
            // Initialize the relative position base pointer
            bufferBase_ = 0;

            // Initialize if necessary
            if (hashTable_ == null)
            {
                Initialize();
            }

            // Clear the hash table
            for (int i = 0; i < HASH_TABLE_SIZE; ++i)
            {
                hashTable_[i] = INVALID_POSITION;
            }
        }

        // Finds match candidates at the current buffer position and slides the matching window to the next character
        // Call findMatches/update with increasing positions
        // The match candidates are stored in the supplied array, ordered by their length (ascending)
        // The return value is the number of match candidates in the array
        public int FindMatches(Match[] matchCandidates)
        {
            Debug.Assert(hashTable_ != null);

            // Check whether we can find matches at this position
            if (absolutePosition_ >= matchableBufferLength_)
            {
                // Slide the matching window with one character
                ++absolutePosition_;

                return 0;
            }

            // Compute the maximum match length
            int maxMatchLength = Math.Min(buffer_.Length - Common.TAIL_LENGTH - absolutePosition_, Common.MAX_MATCH_LENGTH);

            // Compute the position relative to the beginning of bufferBase_
            // All other positions (including the ones stored in the hash table and the binary trees) are relative too
            // From now on, we can safely ignore this position technique
            int position = ComputeRelativePosition();

            // Compute the minimum match position
            int minMatchPosition = (position < Common.DICTIONARY_SIZE) ? 0 : (position - Common.DICTIONARY_SIZE + 1);

            // Compute the hash value for the current string
            uint hashValue = Hash(buffer_, bufferBase_ + position) % HASH_TABLE_SIZE;

            // Get the position of the first match from the hash table
            int matchPosition = hashTable_[hashValue];

            // Set the current string as the root of the binary tree corresponding to the hash table entry
            hashTable_[hashValue] = position;

            // Compute the current cyclic position in the dictionary
            int cyclicInputPosition = position % Common.DICTIONARY_SIZE;

            // Initialize the references to the leaves of the new root's left and right subtrees
            int leftSubtreeLeaf = cyclicInputPosition * 2;
            int rightSubtreeLeaf = cyclicInputPosition * 2 + 1;

            // Initialize the match lenghts of the lower and upper bounds of the current string (lowMatch < match < highMatch)
            // We use these to avoid unneccesary character comparisons at the beginnings of the strings
            int lowMatchLength = 0;
            int highMatchLength = 0;

            // Initialize the longest match length
            int longestMatchLength = 0;

            // Find matches
            // We look for the current string in the binary search tree and we rebuild the tree at the same time
            // The deeper a node is in the tree, the lower is its position, so the root is the string with the highest position (lowest offset)

            // We count the number of match attempts, and exit if it has reached a certain threshold
            int matchCount = 0;

            // Match candidates are matches which are longer than any previously encountered ones
            int matchCandidateCount = 0;

            for (;;)
            {
                // Check whether the current match position is valid
                if (matchPosition < minMatchPosition || matchCount == Common.MAX_MATCH_CANDIDATE_COUNT)
                {
                    // We have checked all valid matches, so finish the new tree and exit
                    children_[leftSubtreeLeaf] = INVALID_POSITION;
                    children_[rightSubtreeLeaf] = INVALID_POSITION;
                    break;
                }

                ++matchCount;

                // Compute the cyclic position of the current match in the dictionary
                int cyclicMatchPosition = matchPosition % Common.DICTIONARY_SIZE;

                // Use the match lengths of the low and high bounds to determine the number of characters that surely match
                int matchLength = Math.Min(lowMatchLength, highMatchLength);

                // Determine the match length
                while (matchLength < maxMatchLength && buffer_[bufferBase_ + position + matchLength] == buffer_[bufferBase_ + matchPosition + matchLength])
                {
                    ++matchLength;
                }

                // Check whether this match is the longest so far
                int matchOffset = position - matchPosition;

                if (matchLength > longestMatchLength && matchLength >= Common.MIN_MATCH_LENGTH)
                {
                    longestMatchLength = matchLength;

                    // Add the current best match to the list of good match candidates
                    if (matchCandidates != null)
                    {
                        matchCandidates[matchCandidateCount].length = matchLength;
                        matchCandidates[matchCandidateCount].offset = matchOffset;
                        ++matchCandidateCount;
                    }

                    // If the match length is the maximum allowed value, the current string is already inserted into the tree: the current node
                    if (matchLength == maxMatchLength)
                    {
                        // Since the current string is also the root of the tree, delete the current node
                        children_[leftSubtreeLeaf] = children_[cyclicMatchPosition * 2];
                        children_[rightSubtreeLeaf] = children_[cyclicMatchPosition * 2 + 1];
                        break;
                    }
                }

                // Compare the two strings
                if (buffer_[bufferBase_ + position + matchLength] < buffer_[bufferBase_ + matchPosition + matchLength])
                {
                    // Insert the matched string into the right subtree
                    children_[rightSubtreeLeaf] = matchPosition;

                    // Go left
                    rightSubtreeLeaf = cyclicMatchPosition * 2;
                    matchPosition = children_[rightSubtreeLeaf];

                    // Update the match length of the high bound
                    highMatchLength = matchLength;
                }
                else
                {
                    // Insert the matched string into the left subtree
                    children_[leftSubtreeLeaf] = matchPosition;

                    // Go right
                    leftSubtreeLeaf = cyclicMatchPosition * 2 + 1;
                    matchPosition = children_[leftSubtreeLeaf];

                    // Update the match length of the low bound
                    lowMatchLength = matchLength;
                }
            }

            // Slide the matching window with one character
            ++absolutePosition_;

            return matchCandidateCount;
        }

        // Slides the matching window to the next character without looking for matches, but it still has to update the dictionary
        public void Skip()
        {
            FindMatches(null);
        }

        public int Position
        {
            get
            {
                return absolutePosition_;
            }
        }

        private void Initialize()
        {
            // Create the hash table
            hashTable_ = new int[HASH_TABLE_SIZE];

            // Create the tree nodes
            // The number of nodes is equal to the size of the dictionary, and every node has two children
            children_ = new int[CHILD_COUNT];
        }

        // Increments the match window position with one character
        private int ComputeRelativePosition()
        {
            int position = (int)(absolutePosition_ - bufferBase_);

            // Check whether the current position has reached the rebase threshold
            if (position == REBASE_THRESHOLD)
            {
                // Rebase
                int rebaseDelta = REBASE_THRESHOLD - Common.DICTIONARY_SIZE;
                Debug.Assert(rebaseDelta % Common.DICTIONARY_SIZE == 0);

                bufferBase_ += rebaseDelta;
                position -= rebaseDelta;

                // Rebase the hash entries
                for (int i = 0; i < HASH_TABLE_SIZE; ++i)
                {
                    hashTable_[i] = (hashTable_[i] >= rebaseDelta) ? (hashTable_[i] - rebaseDelta) : INVALID_POSITION;
                }

                // Rebase the binary tree nodes
                for (int i = 0; i < CHILD_COUNT; ++i)
                {
                    children_[i] = (children_[i] >= rebaseDelta) ? (children_[i] - rebaseDelta) : INVALID_POSITION;
                }
            }

            return position;
        }

        private uint Hash(byte[] data, int pos)
        {
            // FNV-1a hash
            const uint prime = 16777619;
            uint result = 2166136261;

            result = (result ^ data[pos + 0]) * prime;
            result = (result ^ data[pos + 1]) * prime;
            result = (result ^ data[pos + 2]) * prime;

            return result;
        }

        private const int HASH_TABLE_SIZE = 1 << 20;
        private const int CHILD_COUNT = Common.DICTIONARY_SIZE * 2;
        private const int INVALID_POSITION = -1;
        private const int REBASE_THRESHOLD = (int.MaxValue - Common.DICTIONARY_SIZE + 1) / Common.DICTIONARY_SIZE * Common.DICTIONARY_SIZE; // must be a multiple of DICTIONARY_SIZE!

        // Buffer
        private byte[] buffer_; // pointer to the beginning of the buffer inside which we look for matches
        private int bufferBase_; // bufferBase_ > buffer_, relative positions are necessary to support > 2 GB buffers
        private int matchableBufferLength_;
        private int absolutePosition_; // position from the beginning of buffer_

        // Cyclic dictionary
        private int[] hashTable_; // relative match positions to bufferBase_
        private int[] children_; // children of the binary tree nodes (relative match positions to bufferBase_)
    }
}
