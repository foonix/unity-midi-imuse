using System;
using System.Collections.Generic;
using System.Linq;
using AudioSynthesis.Midi;
using AudioSynthesis.Midi.Event;
using UnityEngine;

namespace AudioSynthesis.Sequencer.IMuse
{
    /// <summary>
    /// Correlates the time of a midi event to a specific sample in the output's time stream where it should be played.
    ///
    /// This can only move forward along the current track.  To jump, create a new object and start playing it.
    /// </summary>
    internal class EventStream
    {
        // playing position in absolute output sample space
        long lastEventStartPos = 0;
        long nextBufferStartPos = 0;

        const int oneMinuteInMicroseconds = 60000000;
        int timeSignatureNumerator = 4;
        int timeSignatureDenominator = 4;
        int tempoMicrosecondsPerQuarterNote = 500000;

        readonly MidiFile midiFile;
        internal int PlayingTrack { get; set; }
        internal int PlayingEvent { get; set; }

        string trackName = "";

        /// <summary>
        /// Tick time delay to be added to next event's DeltaTime.  Normally negative after jump.
        /// </summary>
        int nextEventTickDelayAdjustment = 0;

        private EventStreamState state;
        internal EventStreamState State
        {
            get { return state; }
            private set
            {
                if (value < state)
                {
                    throw new ArgumentException("Can't move State backwards!");
                }
                if (value == EventStreamState.WindDown)
                {
                    windDownFilter.WindDownStarted = true;
                }
                state = value;
            }
        }

        internal enum EventStreamState
        {
            Playing,
            WindDown,
            Finished
        }

        private readonly EventFilters.WindDown windDownFilter = new EventFilters.WindDown();

        /// <summary>
        /// v--- dovetail timestream first sample
        /// |--------j----|         <-- source track sample space
        ///        |-t-----------|  <-- target track sample space
        ///        ^--- this timestream first sample
        /// j = Jump command location in source's time space
        /// t = Target time in target's time space
        /// </summary>
        internal EventStream Dovetail { get; private set; }

        #region Helper data structures
        /// <summary>
        /// Return type for enumerator that moves time forward
        /// </summary>
        internal struct EventSampleOffset
        {
            internal readonly int sampleOffset;
            internal readonly MidiEvent midiEvent;
            internal EventSampleOffset(int sampleOffset, MidiEvent midiEvent)
            {
                this.sampleOffset = sampleOffset;
                this.midiEvent = midiEvent;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Creates new stream at beginning of file
        /// </summary>
        /// <param name="file"></param>
        internal EventStream(MidiFile file)
        {
            midiFile = file;
            // default to beginning of first track
        }

        /// <summary>
        /// Creates new stream dovetailing from a prior stream.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="track"></param>
        /// <param name="beat"></param>
        /// <param name="tick"></param>
        /// <param name="dovetail"></param>
        internal EventStream(MidiFile file, int track, int beat, int tick, EventStream dovetail)
        {
            midiFile = file;
            SetPosition(track, beat, tick);
            if (dovetail.State != EventStreamState.Finished)
            {
                Dovetail = dovetail;
                Dovetail.State = EventStreamState.WindDown;
            }
            tempoMicrosecondsPerQuarterNote = dovetail.tempoMicrosecondsPerQuarterNote;
            timeSignatureNumerator = dovetail.timeSignatureNumerator;
            timeSignatureDenominator = dovetail.timeSignatureDenominator;
            if (file == dovetail.midiFile && track == dovetail.PlayingTrack)
            {
                trackName = dovetail.trackName;
            }
        }
        #endregion

        /// <summary>
        /// Moves MIDI time forward by buffer time
        /// </summary>
        /// <param name="samples"></param>
        /// <param name="sampleRate"></param>
        /// <returns></returns>
        internal IEnumerable<EventSampleOffset> MoveNext(int samples, int sampleRate)
        {
            // cleanup dovetail stream when it is done
            if (!(Dovetail is null) && Dovetail.State == EventStreamState.Finished)
            {
                Dovetail = null;
            }

            if (Dovetail is null)
            {
                var mne = MoveNextInternal(samples, sampleRate);
                return InternalEventFilter(mne)
                    .FilterEvents(windDownFilter);
            }
            else
            {
                // Cross the streams.
                return Dovetail.MoveNext(samples, sampleRate)
                    .Concat(InternalEventFilter(MoveNextInternal(samples, sampleRate)).FilterEvents(windDownFilter))
                    .OrderBy(et => et.sampleOffset);
            }
        }

        private IEnumerable<EventSampleOffset> MoveNextInternal(int samples, int sampleRate)
        {
            while (!(State == EventStreamState.Finished))
            {
                // there should be an end of file meta event, but just in case..
                if (PlayingEvent >= midiFile.Tracks[PlayingTrack].MidiEvents.Length)
                {
                    State = EventStreamState.Finished;
                    yield break;
                }

                if (State == EventStreamState.WindDown && windDownFilter.NoteOffPendingCount == 0)
                {
                    State = EventStreamState.Finished;
                    yield break;
                }

                MidiEvent nextEvent = midiFile.Tracks[PlayingTrack].MidiEvents[PlayingEvent];

                int eventStartOffset = (int)(TickTimeToSeconds(nextEvent.DeltaTime + nextEventTickDelayAdjustment) * sampleRate);

                // next event is beyond current buffer process
                if (lastEventStartPos + eventStartOffset >= nextBufferStartPos + samples)
                {
                    break;
                }

                int eventStartSampleInBuffer = (int)(lastEventStartPos + eventStartOffset - nextBufferStartPos);

                yield return new EventSampleOffset(eventStartSampleInBuffer, nextEvent);

                PlayingEvent++;
                lastEventStartPos += eventStartOffset;
                nextEventTickDelayAdjustment = 0;
            }

            // accumulate time for buffer fills where no events happen
            nextBufferStartPos += samples;
        }

        // Information purposes only
        float Bpm { get => (oneMinuteInMicroseconds / tempoMicrosecondsPerQuarterNote) * (timeSignatureDenominator / 4); }

        #region Time helpers
        double TickTimeToSeconds(int ticks) => TickTimeToSeconds(ticks, tempoMicrosecondsPerQuarterNote);

        double TickTimeToSeconds(int ticks, int tempo)
        {
            // simplified: ticksToWait * (quarter / tick) * (microsecond / quarter) * (seconds / microseconds)
            //                  ^-- event Δt      ^-- file header        ^-- current tempo     ^-- unit conversion
            return ticks * ((double)tempo / (midiFile.Division * 1000000));
        }

        /// <summary>
        /// Set stream position
        /// </summary>
        /// <param name="track">track to jump to</param>
        /// <param name="beat">beat (actually, quarter notes) offset from track start</param>
        /// <param name="tick">tick offset from beat start</param>
        /// <returns></returns>
        private void SetPosition(int track, int beat, int tick)
        {
            PlayingTrack = track;
            int targetTick = (beat - 1) * midiFile.Division + tick;

            // find first event after jump
            int eventNumber = 0;
            int ticksSeen = 0;
            foreach (var midiEvent in midiFile.Tracks[PlayingTrack].MidiEvents)
            {
                if (ticksSeen + midiEvent.DeltaTime >= targetTick)
                    break;

                ticksSeen += midiEvent.DeltaTime;
                eventNumber++;
            }

            if (eventNumber >= midiFile.Tracks[PlayingTrack].MidiEvents.Length)
            {
                throw new ArgumentException(string.Format("Jump target time Track:{0} Beat:{1} Tick:{2} is past end of track!", track, beat, tick));
            }

            // When jumping into a gap between two events or directly to time event should occur,
            // the delay on the next event must be reduced.
            nextEventTickDelayAdjustment = ticksSeen - targetTick;

            PlayingEvent = eventNumber;
        }
        #endregion

        IEnumerable<EventSampleOffset> InternalEventFilter(IEnumerable<EventSampleOffset> input)
        {
            foreach (var eso in input)
            {
                switch (eso.midiEvent)
                {
                    case MetaTextEvent e:
                        switch ((MetaEventTypeEnum)e.Data1)
                        {
                            case MetaEventTypeEnum.EndOfTrack:
                                State = EventStreamState.Finished;
                                break;
                            case MetaEventTypeEnum.SequenceOrTrackName:
                                trackName = e.Text;
                                break;
                            case MetaEventTypeEnum.TimeSignature:
                                string[] values = e.Text.Split(':');
                                timeSignatureNumerator = int.Parse(values[0]);
                                timeSignatureDenominator = Mathf.RoundToInt(Mathf.Pow(2, int.Parse(values[1])));
                                break;
                            default:
                                yield return eso;
                                break;
                        }
                        break;
                    case MetaNumberEvent e:
                        switch ((MetaEventTypeEnum)e.Data1)
                        {
                            case MetaEventTypeEnum.Tempo:
                                tempoMicrosecondsPerQuarterNote = e.Value;
                                break;
                        }
                        break;
                    default:
                        yield return eso;
                        break;
                }
            }
        }

        public override string ToString()
        {
            return string.Format("Event Stream: Track:{0} (name:{1}) Event:{2} SampleOffset:{3} TimeSignature:{4}/{5}, {6}BPM, State:{7},\nDovetail: {8}", new object[] {
                PlayingTrack, trackName, PlayingEvent, nextBufferStartPos, timeSignatureNumerator, timeSignatureDenominator, Bpm, State, (Dovetail is null ? "none" : Dovetail.ToString())
            });
        }
    }
}
