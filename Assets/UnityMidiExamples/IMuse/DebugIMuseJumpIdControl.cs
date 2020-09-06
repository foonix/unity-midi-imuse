using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AudioSynthesis.Sequencer.IMuse
{
    public class DebugIMuseJumpIdControl : MonoBehaviour
    {
        public int id;

        public TextMeshProUGUI label;
        public Toggle enableToggle;
        public Toggle persistToggle;

        internal IMuseDirector director;

        public void Start()
        {
            label.text = id.ToString();
            if(director.Sequencer.HookJumpsEnabled.TryGetValue(id, out var persists))
            {
                enableToggle.isOn = true;
                persistToggle.isOn = persists;
            }
        }

        public void Update()
        {
            var hooks = director?.Sequencer?.HookJumpsEnabled;
            if(!(hooks is null))
            {
                var enabled = hooks.TryGetValue(id, out var persists);
                enableToggle.isOn = enabled;
                if (enabled)
                {
                    persistToggle.isOn = persists;
                }
            }
        }

        #region toggle input handlers
        public void ToggleEnabled(bool selected)
        {
            if (selected)
            {
                if(!director.Sequencer.HookJumpsEnabled.ContainsKey(id))
                {
                    director.Sequencer.HookJumpsEnabled.Add(id, persistToggle);
                }
            }
            else
            {
                director.Sequencer.HookJumpsEnabled.Remove(id);
            }

            // changing persistance only makes sense if jump is enabled
            persistToggle.interactable = selected;
        }

        public void TogglePersists(bool selected)
        {
            director.Sequencer.HookJumpsEnabled[id] = selected;
        }
        #endregion
    }
}
