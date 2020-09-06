using System.Collections.Generic;
using System.Linq;
using AudioSynthesis.Midi;
using AudioSynthesis.Midi.Event;
using static AudioSynthesis.Sequencer.IMuse.EventStream;

namespace AudioSynthesis.Sequencer.IMuse.EventFilters
{
    /// <summary>
    /// Used for winding down an event stream.  Must be used even when not winding down for not tracking.
    /// </summary>
    internal class WindDown : EventFilter
    {
        internal bool WindDownStarted { get; set; } = false;

        internal int NoteOffPendingCount { get => windDownEntries.Count(); }

        // Track voices that will need to be stopped if we are set to wind down.
        private struct WindDownEntry
        {
            internal int channel;
            internal int pitch;
            internal WindDownEntry(int channel, int pitch)
            {
                this.channel = channel;
                this.pitch = pitch;
            }
        }
        readonly HashSet<WindDownEntry> windDownEntries = new HashSet<WindDownEntry>();

        internal override IEnumerable<EventSampleOffset> ApplyFilter(IEnumerable<EventSampleOffset> input)
        {
            foreach (var eso in input)
            {
                if (WindDownStarted)
                {
                    if (windDownEntries.Count == 0)
                    {
                        yield break;
                    }
                    var wde = new WindDownEntry(eso.midiEvent.Channel, eso.midiEvent.Data1);
                    if (eso.midiEvent is MidiEvent && (MidiEventTypeEnum)eso.midiEvent.Command == MidiEventTypeEnum.NoteOff && windDownEntries.Contains(wde))
                    {
                        windDownEntries.Remove(wde);
                        yield return eso;
                    }
                    // Filter all other event types
                }
                else
                {
                    // track playing notes for wind down
                    switch (eso.midiEvent)
                    {
                        case MidiEvent e:
                            switch ((MidiEventTypeEnum)e.Command)
                            {
                                case MidiEventTypeEnum.NoteOn:
                                    windDownEntries.Add(new WindDownEntry(eso.midiEvent.Channel, eso.midiEvent.Data1));
                                    break;
                                case MidiEventTypeEnum.NoteOff:
                                    windDownEntries.Remove(new WindDownEntry(eso.midiEvent.Channel, eso.midiEvent.Data1));
                                    break;
                            }
                            break;
                    }

                    yield return eso;
                }
            }
        }
    }
}
