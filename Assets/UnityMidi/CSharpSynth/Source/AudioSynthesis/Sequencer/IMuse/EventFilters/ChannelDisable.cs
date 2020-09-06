using AudioSynthesis.Midi;
using AudioSynthesis.Midi.Event;
using System.Collections.Generic;
using static AudioSynthesis.Sequencer.IMuse.EventStream;

namespace AudioSynthesis.Sequencer.IMuse.EventFilters
{
    /// <summary>
    /// Filters NoteStart events for channels that are disabled.
    /// 
    /// NoteStop events are allowed so that notes don't "stick" after disabling the channel.
    /// </summary>
    class ChannelIsEnabled : EventFilter
    {
        internal bool Enabled { get; set; } = true;
        internal int Channel { get; private set; }

        internal ChannelIsEnabled(int channel)
        {
            Channel = channel;
        }

        internal override IEnumerable<EventSampleOffset> ApplyFilter(IEnumerable<EventSampleOffset> e)
        {
            foreach (var eso in e)
            {
                if (Enabled == false && eso.midiEvent.Channel == Channel && eso.midiEvent is MidiEvent && (MidiEventTypeEnum)eso.midiEvent.Command == MidiEventTypeEnum.NoteOn)
                {
                    continue;
                }
                else
                {
                    yield return eso;
                }
            }
        }
    }
}
