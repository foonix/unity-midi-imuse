using AudioSynthesis.Sequencer.IMuse;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Debug
{
    public class DebugIMuseChanelControl : MonoBehaviour
    {
        public int id;
        public TextMeshProUGUI label;
        public Toggle isEnabled;
        public Toggle wantEnable;
        public TextMeshProUGUI status;

        DebugIMuseConsole console;
        public IMuseDirector director;

        public void Awake()
        {
            console = GetComponentInParent<DebugIMuseConsole>();
            label.text = "Channel " + id;
        }

        public void Update()
        {
            var channelStatus = console.director?.Sequencer?.PartFilter;
            if (!(channelStatus is null))
            {
                isEnabled.isOn = channelStatus.ChannelSettings[id].Enabled;
                wantEnable.isOn = channelStatus.ChannelSettings[id].wantEnable;
            }

            var synthChannelStatus = console.director?.Synthesizer?.synthChannels[id];
            if (!(synthChannelStatus is null))
            {
                var patch = console?.director?.Synthesizer?.SoundBank.GetPatch(synthChannelStatus.bankSelect, synthChannelStatus.program);
                status.text = string.Format("{0}\n{1}\n{2}\n{3}",
                    string.Format("{0} ({1}/{2})", patch.Name, synthChannelStatus.bankSelect, synthChannelStatus.program),
                    string.Format("Volume: {0}", synthChannelStatus.volume.Coarse),
                    string.Format("CurVolume: {0:0.00}", synthChannelStatus.currentVolume),
                    string.Format("Pan: {0}", synthChannelStatus.pan.Coarse)
                );
            }
            else
            {
                status.text = "Not Available";
            }
        }

        public void EnableToggleChange(bool enabled)
        {
            var channelStatus = console.director?.Sequencer?.PartFilter;
            if (!(channelStatus is null))
            {
                channelStatus.ChannelSettings[id].Enabled = enabled;
            }
        }

        public void WantEnableToggleChange(bool wantEnable)
        {
            var channelStatus = console.director?.Sequencer?.PartFilter;
            if (!(channelStatus is null))
            {
                channelStatus.ChannelSettings[id].wantEnable = wantEnable;
            }
        }
    }
}
