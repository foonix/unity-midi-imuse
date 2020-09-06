using AudioSynthesis.Midi.Event;
using AudioSynthesis.Synthesis;
using System.Collections.Generic;

namespace AudioSynthesis.Sequencer.IMuse.Hooks
{
    internal abstract class Hook : SystemExclusiveEvent
    {

        /// <summary>
        /// Do actions specificed by the hook
        /// </summary>
        /// <param name="sequencer">sequencer executing the hook</param>
        internal abstract void Execute(Sequencer sequencer);

        // Working around protected member access limitations here, but ick.
        internal Hook(SystemExclusiveEvent e) : base(e.DeltaTime, (byte)(e.Channel|e.Command), (short)e.Data1, e.Data)
        {
        }
    }
}
