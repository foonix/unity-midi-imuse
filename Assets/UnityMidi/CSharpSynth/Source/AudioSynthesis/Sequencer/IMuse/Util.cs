using System.Collections.Generic;
using static AudioSynthesis.Sequencer.IMuse.EventStream;
using AudioSynthesis.Sequencer.IMuse.EventFilters;

namespace AudioSynthesis.Sequencer.IMuse
{
    internal static class Util
    {
        /// <summary>
        /// Read 16 bit integer packed as one nibble per byte in big endian order
        /// </summary>
        /// <param name="bytes">byte[] to read from</param>
        /// <param name="offset">offset from start of array</param>
        /// <returns>converted value</returns>
        internal static short ReadPackedInt16Be(byte[] bytes, int offset)
        {
            // Negatives?
            return (short)(bytes[offset] << 12 | bytes[offset + 1] << 8 | bytes[offset + 2] << 4 | bytes[offset + 3]);
        }

        /// <summary>
        /// Read byte packed as one nibble per byte in big endian order
        /// </summary>
        /// <param name="bytes">byte[] to read from</param>
        /// <param name="offset">offset from start of array</param>
        /// <returns>converted value</returns>
        internal static byte ReadPackedByteBe(byte[] bytes, int offset)
        {
            return (byte)(bytes[offset] << 4 | bytes[offset + 1]);
        }

        /// <summary>
        /// Apply an EventFilter to EventSampleOffset stream
        /// </summary>
        /// <param name="input">The stream to filter</param>
        /// <param name="filter">The filter to apply</param>
        /// <returns></returns>
        internal static IEnumerable<EventSampleOffset> FilterEvents(this IEnumerable<EventSampleOffset> input, EventFilter filter)
        {
            return filter.ApplyFilter(input);
        }
    }
}
