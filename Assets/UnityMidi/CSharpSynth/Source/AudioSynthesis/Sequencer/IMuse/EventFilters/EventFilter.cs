using System.Collections.Generic;

namespace AudioSynthesis.Sequencer.IMuse.EventFilters
{
    internal abstract class EventFilter
    {
        /// <summary>
        /// Modify MIDI events
        /// </summary>
        /// <param name="e">The original event</param>
        /// <returns>Modified copy of event</returns>
        internal abstract IEnumerable<EventStream.EventSampleOffset> ApplyFilter(IEnumerable<EventStream.EventSampleOffset> e);
    }
}
