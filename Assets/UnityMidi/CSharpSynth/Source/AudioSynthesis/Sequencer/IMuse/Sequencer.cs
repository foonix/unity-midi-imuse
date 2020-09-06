using System;
using System.Collections.Generic;
using System.Linq;
using AudioSynthesis.Sequencer.IMuse.EventFilters;
using AudioSynthesis.Bank;
using AudioSynthesis.Midi;
using AudioSynthesis.Midi.Event;
using AudioSynthesis.Synthesis;
using UnityEngine;

namespace AudioSynthesis.Sequencer.IMuse
{
    internal class Sequencer
    {
        /// <summary>
        /// Hook ID -> stays enabled after use
        /// </summary>
        public Dictionary<int, bool> HookJumpsEnabled { get; private set; } = new Dictionary<int, bool>();

        public PartFilter PartFilter { get; private set; } = new PartFilter();
        public EventStream HeadEventStream { get; private set; }

        /// <summary>
        /// Called when an iMUSE marker is "played" by the sequencer.
        /// Usually markers are at the end of a track to tell the game when to load another midi file or change scene.
        /// This is called from the unity audio thread so beware race conditions when interacting from the main thread.
        /// </summary>
        internal Action<int> OnMarkerSequenced { get; set; }

        MidiFile midiFile;

        internal Synthesizer Synthesizer { get; set; }

        internal Sequencer(Synthesizer synthesizer)
        {
            Synthesizer = synthesizer;
        }

        internal void LoadMidi(MidiFile file)
        {
            midiFile = file;
            if (midiFile.TimingStandard == MidiFile.TimeFormat.FamesPerSecond)
                throw new ArgumentException("MIDI SMPTE time not supported");

            Reset();
        }

        /// <summary>
        /// Enqueue one synthesizer.WorkingBufferSize worth of midi commands.
        /// </summary>
        internal void FillMidiEventQueue()
        {
            if (midiFile is null)
                return;

            int samplesPerChannel = Synthesizer.WorkingBufferSize / Synthesizer.AudioChannels;

            var filteredEventStream = HeadEventStream.MoveNext(samplesPerChannel, Synthesizer.SampleRate).FilterEvents(PartFilter);

            foreach (var eso in filteredEventStream)
            {
                DispatchEvent(eso.midiEvent, eso.sampleOffset / Synthesizer.MicroBufferSize);
            }
        }

        #region Event dispatch
        void DispatchEvent(MidiEvent midiEvent, int microbuffer)
        {
            switch (midiEvent)
            {
                case SystemExclusiveEvent e:
                    // SystemExclusiveEvent.ManufacturerId seems broken
                    if ((SysExVendor)e.Data[0] == SysExVendor.IMuse)
                        DispatchIMuseEvent(e);
                    break;
                default:
                    DispatchToSynthesizer(midiEvent, microbuffer);
                    break;
            }
        }

        private void DispatchIMuseEvent(SystemExclusiveEvent e)
        {
            switch ((IMuseSysExType)e.Data[1])
            {
                case (IMuseSysExType.HookJump):
                    var jump = new Hooks.Jump(e);
                    var enabled = HookJumpsEnabled.TryGetValue(jump.Id, out var persists);

                    if (enabled)
                    {
                        jump.Execute(this);
                    }
                    else
                    {
                        break;
                    }

                    if (!persists)
                    {
                        HookJumpsEnabled.Remove(jump.Id);
                    }

                    break;
                case IMuseSysExType.SetInstrument:
                    // I think these would normally be translated into bank select commands depending on if the underlying synthesizer supports them.
                    var channel = e.Data[2];
                    var program = Util.ReadPackedByteBe(e.Data, 3);
                    var bank = channel == MidiHelper.DrumChannel ? (byte)PatchBank.DrumBank : Util.ReadPackedByteBe(e.Data, 5);
                    if (Synthesizer.SoundBank.IsBankLoaded(bank) && !(Synthesizer.SoundBank.GetBank(bank)[program] is null))
                    {
                        Synthesizer.synthChannels[channel].bankSelect = bank;
                        Synthesizer.synthChannels[channel].program = program;
                    }
                    break;
                case IMuseSysExType.Marker:
                    if (OnMarkerSequenced is null)
                    {
                        goto default;
                    }
                    OnMarkerSequenced.Invoke(Util.ReadPackedByteBe(e.Data, 2));
                    break;
                default:
                    Debug.Log(String.Format("Unhandled iMUSE SysEx {0} at Track:{1} Event:{2}", ((IMuseSysExType)e.Data[1]).ToString(), HeadEventStream.PlayingTrack, HeadEventStream.PlayingEvent));
                    break;
            }
        }

        void DispatchToSynthesizer(MidiEvent midiEvent, int microbuffer)
        {
            Synthesizer.midiEventQueue.Enqueue(
                new MidiMessage((byte)midiEvent.Channel, (byte)midiEvent.Command, (byte)midiEvent.Data1, (byte)midiEvent.Data2)
            );
            Synthesizer.midiEventCounts[microbuffer]++;
        }
        #endregion

        // Rewinds to beginning, preserves hook / channel states
        internal void Stop()
        {
            Reset();
        }

        void Reset()
        {
            HeadEventStream = new EventStream(midiFile);
            HookJumpsEnabled.Clear();
            HookJumpsEnabled.Add(0, true);  // jump ID 0 is almost always needed.
        }

        /// <summary>
        /// Dovetail to location in another midi file
        /// </summary>
        /// <param name="file"></param>
        /// <param name="track"></param>
        /// <param name="beat"></param>
        /// <param name="tick"></param>
        internal void SequenceNextEventStream(MidiFile file, int track, int beat, int tick)
        {
            HeadEventStream = new EventStream(file, track, beat, tick, HeadEventStream);
        }

        /// <summary>
        /// Dovetail to location in currently playing file
        /// </summary>
        /// <param name="track"></param>
        /// <param name="beat"></param>
        /// <param name="tick"></param>
        internal void SequenceNextEventStream(int track, int beat, int tick)
        {
            SequenceNextEventStream(midiFile, track, beat, tick);
        }

        public override string ToString()
        {
            return string.Format("Sequencer: Tracks:{0}, HeadEventStream:{1}", midiFile?.Tracks?.Length, HeadEventStream?.ToString());
        }

        /// <summary>
        /// Enumerate all iMUSE jump commands in file
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Hooks.Jump> FindAllJumpCommands()
        {
            return midiFile.Tracks
                .SelectMany(t => t.MidiEvents)
                .OfType<SystemExclusiveEvent>()
                .Where(e => (SysExVendor)e.Data[0] == SysExVendor.IMuse)
                .Where(e => (IMuseSysExType)e.Data[1] == IMuseSysExType.HookJump)
                .Select(e => new Hooks.Jump(e));
        }
    }
}
