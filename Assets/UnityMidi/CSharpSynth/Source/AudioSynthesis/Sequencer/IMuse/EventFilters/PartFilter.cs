using AudioSynthesis.Midi.Event;
using System;
using System.Collections.Generic;
using System.Linq;
using static AudioSynthesis.Sequencer.IMuse.EventStream;

namespace AudioSynthesis.Sequencer.IMuse.EventFilters
{
    /// <summary>
    /// Tracks part related SysEx messages and applies any necessary filtering.
    /// </summary>
    internal class PartFilter : EventFilter
    {
        internal ChannelSettings[] ChannelSettings { get; } = new ChannelSettings[16];

        internal PartFilter()
        {
            for (int i = 0; i < ChannelSettings.Length; i++)
            {
                ChannelSettings[i] = new ChannelSettings(i);
            }
        }

        internal override IEnumerable<EventSampleOffset> ApplyFilter(IEnumerable<EventSampleOffset> input)
        {
            var channelFilteredInput = ChannelSettings.Aggregate(input, (filtered, nextFilter) => nextFilter.ApplyFilter(filtered));
            foreach (var eso in channelFilteredInput)
            {
                // respond to part control commands
                switch (eso.midiEvent)
                {
                    case SystemExclusiveEvent sysEx:
                        if ((SysExVendor)sysEx.Data[0] != SysExVendor.IMuse)
                        {
                            break;
                        }
                        switch ((IMuseSysExType)sysEx.Data[1])
                        {
                            case IMuseSysExType.AllocatePart:
                                throw new NotImplementedException();
                            case IMuseSysExType.ShutdownPart:
                                throw new NotImplementedException();
                            case IMuseSysExType.ParameterAdjust:
                                throw new NotImplementedException();
                            case IMuseSysExType.HookPartSetActive:
                                // Musically appropriate moment to turn part on/off.
                                var channel = sysEx.Data[2];
                                var hookId = Util.ReadPackedByteBe(sysEx.Data, 3);
                                var value = Util.ReadPackedByteBe(sysEx.Data, 5);

                                switch (hookId)
                                {
                                    case 0:
                                        // music composer signals default part status
                                        ChannelSettings[channel].Enabled = ChannelSettings[channel].wantEnable = (value != 0);
                                        break;
                                    default:
                                        // normally when 0 < hookId < 128, this means one-shot hook, and hookId >= 128 means persistent hook.
                                        // but until I find a use-case to track individual hooks IDs, I'm just treating everything as persistent.
                                        ChannelSettings[channel].Enabled = ChannelSettings[channel].wantEnable;
                                        break;
                                }
                                break;
                            default:
                                yield return eso;
                                break;
                        }
                        break;
                    default:
                        yield return eso;
                        break;
                }
            }
        }

        void DecodePartDeclaration(SystemExclusiveEvent declaration)
        {
            var id = declaration.Data[2];
            ChannelSettings[id].Enabled = (declaration.Data[4] | 0x1) > 0;
            ChannelSettings[id].reverb = (declaration.Data[4] | 0x2) > 0;
            ChannelSettings[id].priority = declaration.Data[5];
            ChannelSettings[id].volume = Util.ReadPackedByteBe(declaration.Data, 6);
            ChannelSettings[id].pan = Util.ReadPackedByteBe(declaration.Data, 8);
            // TODO: flags2
            ChannelSettings[id].pitchBend = Util.ReadPackedByteBe(declaration.Data, 11);
            ChannelSettings[id].program = Util.ReadPackedByteBe(declaration.Data, 13);
        }
    }
}
