using System.Collections.Generic;
using static AudioSynthesis.Sequencer.IMuse.EventStream;

namespace AudioSynthesis.Sequencer.IMuse.EventFilters
{

    internal class ChannelSettings : EventFilter
    {
        internal ChannelIsEnabled EnableFilter { get; private set; }
        internal bool Enabled { get => EnableFilter.Enabled; set => EnableFilter.Enabled = value; }
        internal bool wantEnable = true;

        // These will probably also be filters
        internal bool reverb = false;
        internal int priority;
        internal byte volume = 0x7F;
        internal int pan;
        internal int pitchBend;
        internal int program;

        internal ChannelSettings(int channel)
        {
            EnableFilter = new ChannelIsEnabled(channel);
        }

        internal override IEnumerable<EventSampleOffset> ApplyFilter(IEnumerable<EventSampleOffset> input)
        {
            return EnableFilter.ApplyFilter(input);
        }
    }
}
