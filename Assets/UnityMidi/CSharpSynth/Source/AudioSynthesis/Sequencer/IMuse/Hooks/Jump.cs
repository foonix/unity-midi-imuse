using AudioSynthesis.Midi.Event;

namespace AudioSynthesis.Sequencer.IMuse.Hooks
{
    internal class Jump : Hook
    {
        internal int Id { get; set; }
        internal int Track { get; set; }
        internal int Beat { get; set; }
        internal int Tick { get; set; }


        internal Jump(SystemExclusiveEvent e) : base(e)
        {
            Id = Util.ReadPackedByteBe(e.Data, 3);
            Track = Util.ReadPackedInt16Be(e.Data, 5);
            Beat = Util.ReadPackedInt16Be(e.Data, 9);
            Tick = Util.ReadPackedInt16Be(e.Data, 13);
        }

        internal override void Execute(Sequencer sequencer)
        {
            sequencer.SequenceNextEventStream(Track, Beat, Tick);
        }
    }
}
