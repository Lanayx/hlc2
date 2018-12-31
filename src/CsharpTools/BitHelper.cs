using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace CsharpTools
{
    public static class BitHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetBufferLength(BitArray bitArray)
        {
            return (bitArray.Count >> 5) + 1;
        }

        public static Int32 GetCardinality(BitArray bitArray)
        {

            Int32[] ints = new Int32[GetBufferLength(bitArray)];

            bitArray.CopyTo(ints, 0);

            Int32 count = 0;

            // fix for not truncated bits in last integer that may have been set to true with SetAll()
            ints[ints.Length - 1] &= ~(-1 << (bitArray.Count % 32));

            for (Int32 i = 0; i < ints.Length; i++)
            {

                Int32 c = ints[i];

                // magic (http://graphics.stanford.edu/~seander/bithacks.html#CountBitsSetParallel)
                unchecked
                {
                    c = c - ((c >> 1) & 0x55555555);
                    c = (c & 0x33333333) + ((c >> 2) & 0x33333333);
                    c = ((c + (c >> 4) & 0xF0F0F0F) * 0x1010101) >> 24;
                }

                count += c;

            }

            return count;

        }
    }
}
