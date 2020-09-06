using AudioSynthesis.Synthesis;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AudioSynthesis.Sequencer.IMuse
{
    public class DebugIMuseConsole : MonoBehaviour
    {
        public string midiFileDirectory;
        public TMP_Dropdown songSelectDropdown;
        public TextMeshProUGUI playingFile;
        public TextMeshProUGUI sequencerStatus;
        public RectTransform jumpIdsEnabledPanel;
        public RectTransform partsPanel;
        public RectTransform voiceStatusPanel;
        public IMuseDirector director;

        public GameObject prefabJumpIdControl;

        public void Start()
        {
            songSelectDropdown.ClearOptions();
            songSelectDropdown.AddOptions(
                Directory.EnumerateFiles(midiFileDirectory).Select(
                    file => new TMP_Dropdown.OptionData()
                    {
                        text = Path.GetFileName(file)
                    }
                ).ToList()
            );
        }

        public void ChangeFile()
        {
            director.LoadFromPath(Path.Combine(midiFileDirectory, songSelectDropdown.options[songSelectDropdown.value].text));
            Reset();
        }

        public void Reset()
        {
            GenerateJumpIdControlls();
        }

        public void Update()
        {
            if (director.Sequencer is null)
            {
                // director not yet initialized
                return;
            }
            playingFile.text = director.resource.ToString();
            sequencerStatus.text = string.Format("Font Name: '{0}' Font Comments: '{1}'\n{2}",
                director.Synthesizer.SoundBank.Name,
                director.Synthesizer.SoundBank.Comments,
                director.Sequencer.ToString()
            );

            // voice status
            var activeVoices = director.Synthesizer.voiceManager.activeVoices.ToList();
            activeVoices.Sort(new SortVoicesByChannel());

            for (int i = 0; i < activeVoices.Count; i++)
            {

                TextMeshProUGUI voiceBoxText;
                if (voiceStatusPanel.transform.childCount <= i)
                {
                    voiceBoxText = GenerateSynthChannelDisplay(voiceStatusPanel, i).GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    voiceBoxText = voiceStatusPanel.transform.GetChild(i).GetComponent<TextMeshProUGUI>();
                }

                voiceBoxText.text = activeVoices[i].VoiceParams.ToString();
            }

            // cleanup inactive voice status lines
            for (int i = voiceStatusPanel.transform.childCount; i > activeVoices.Count; i--)
            {
                voiceStatusPanel.transform.GetChild(i - 1).GetComponent<TextMeshProUGUI>().text = "";
            }
        }

        GameObject GenerateSynthChannelDisplay(RectTransform parent, int channel)
        {
            var display = new GameObject("Voice " + channel, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            display.transform.SetParent(parent);
            var text = display.GetComponent<TextMeshProUGUI>();
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.fontSize = 12;
            return display;
        }

        private void GenerateJumpIdControlls()
        {
            foreach (RectTransform control in jumpIdsEnabledPanel.transform)
            {
                Destroy(control.gameObject);
            }
            foreach (var jumpId in director.Sequencer.FindAllJumpCommands().Select(c => c.Id).Distinct())
            {
                var control = Instantiate(prefabJumpIdControl).GetComponent<DebugIMuseJumpIdControl>();
                control.transform.SetParent(jumpIdsEnabledPanel);
                control.director = director;
                control.id = jumpId;
            }
        }

        private class SortVoicesByChannel : IComparer<Voice>
        {
            public int Compare(Voice x, Voice y)
            {
                return x.VoiceParams.channel.CompareTo(y.VoiceParams.channel);
            }
        }

        public void OnConcourseButtonClick()
        {
            //GeneralManager.setBusyCursor();
            //SceneHelper.Load("concourseScene");
        }
    }
}
