using System;
using System.IO;
using System.Linq;
using AudioSynthesis;
using AudioSynthesis.Bank;
using AudioSynthesis.Midi;
using AudioSynthesis.Synthesis;
using UnityEngine;
using UnityMidi;

namespace AudioSynthesis.Sequencer.IMuse
{
    /// <summary>
    /// The directoris responsible for correlating what is going on in game with what happens in the music.
    /// 
    /// It:
    ///  - Loads the song(s) appropriate for the scene into the sequencer.
    ///  - Listens to events to events in the scene that should affect music playback
    ///  - Is responsible for knowing what hooks/triggers IDs in the song its self to activate/deactivate in response to those events
    ///  - Listens to callbacks from the sequencer (triggered by hooks/triggers in the song) and defines what the sequencer should do.
    /// 
    /// In terms of patent US5315057A, this fills the purpose of (fig 3) 104, 120, and 106.
    /// 
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class IMuseDirector : MonoBehaviour
    {
        public StreamingAssetResouce defaultBankSource;
        public string externalSoundFontPath = "";
        public int synthBufferSize = 1024;

        internal Synthesizer Synthesizer { get; private set; }

        /// <summary>
        /// The last sample in currentBuffer sent to the AudioSource
        /// </summary>
        int bufferHead;

        /// <summary>
        /// Current/leftover buffer data from previous AudioSource fill
        /// </summary>
        float[] currentBuffer;

        public AudioSource AudioSource { get; private set; }

        internal Sequencer Sequencer { get; private set; }

        public PatchBank Bank { get; private set; }

        internal Song PlayingSong { get; set; }

        /// <summary>
        /// Proxy for Sequencer.OnMarkerSequenced() that is preserved when sequencer is destroyed.
        /// </summary>
        public Action<int> OnMarkerSequenced { get => onMarkerSequenced; set => Sequencer.OnMarkerSequenced = onMarkerSequenced = value; }
        private Action<int> onMarkerSequenced;

        // just saveing here temporarily for the song file name in the debugger until Song is implemented
        public ResourceFromFile resource;

        public IResource BankSource
        {
            get
            {
                if (!string.IsNullOrEmpty(externalSoundFontPath))
                {
                    return new ResourceFromFile(externalSoundFontPath);
                }
                else
                {
                    return defaultBankSource;
                }
            }
        }

        #region Adapters
        /// <summary>
        /// Adapter to wrap file outside of unity
        /// </summary>
        public class ResourceFromFile : IResource
        {
            readonly string path;

            public ResourceFromFile(string path)
            {
                this.path = path;
            }

            public Stream OpenResourceForRead()
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read);
            }

            public bool DeleteAllowed() => false;
            public void DeleteResource() => throw new System.NotImplementedException();
            public string GetName() => path;
            public Stream OpenResourceForWrite() => throw new NotImplementedException();
            public bool ReadAllowed() => true;
            public bool WriteAllowed() => false;
            public override string ToString() => path;
        }
        #endregion

        #region Play control interface
        public void LoadBank(PatchBank bank)
        {
            Bank = bank;
            Synthesizer.UnloadBank();
            Synthesizer.LoadBank(bank);
            Synthesizer.ResetSynthControls();
        }

        public void PlayMidi(bool pause = false)
        {
            if (pause)
            {
                AudioSource.Stop();
            }
            else
            {
                AudioSource.Play();
            }
        }

        public void LoadFromPath(string path)
        {
            resource = new ResourceFromFile(path);
            Reset();
        }

        public void Reset()
        {
            AudioSource = GetComponent<AudioSource>();

            ResetSynthesizer();

            Sequencer = new Sequencer(Synthesizer);
            Sequencer.OnMarkerSequenced = onMarkerSequenced;

            LoadBank(new PatchBank(BankSource));

            var midiFile = new MidiFile(resource);
            Sequencer.LoadMidi(midiFile);

            Debug.Log(string.Format("IMuseDirector: target mode {0} synth channels {1} synth buffer size {2}", AudioSettings.speakerMode.ToString(), Synthesizer.AudioChannels, synthBufferSize));
        }

        public void Stop()
        {
            AudioSource.Stop();
            Sequencer.Stop();
            ResetSynthesizer();
            LoadBank(new PatchBank(BankSource));
            Sequencer.Synthesizer = Synthesizer;
        }
        #endregion

        void ResetSynthesizer()
        {
            // synth sample rate must match the output audio device
            var sampleRate = AudioSettings.outputSampleRate;

            // midi events will only register at the beginning of a microBuffer generation
            // So note timing precision is limited by microbuffer size
            var microBuffers = 4;

            // synthesizer only supports mono or stereo
            var synthChannels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;

            // Already buffering on AudioSource level, so no need to buffer at midi synth level.
            Synthesizer = new Synthesizer(sampleRate, synthChannels, synthBufferSize, microBuffers);
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            int count = 0;

            while (count < data.Length)
            {
                if (currentBuffer == null || bufferHead >= currentBuffer.Length)
                {
                    // Delay filling event queue or rendering buffers
                    // as long as possible to reduce iMUSE response lag.
                    Sequencer.FillMidiEventQueue();
                    Synthesizer.GetNext();
                    currentBuffer = Synthesizer.WorkingBuffer;
                    bufferHead = 0;
                }

                var length = Mathf.Min(currentBuffer.Length - bufferHead, data.Length - count);

                switch (channels)
                {
                    case (1):
                    case (2):  // stereo or mono
                        Array.Copy(currentBuffer, bufferHead, data, count, length);
                        break;
                    case (4):  // Quad surround mux
                        for (int i = 0; i < length; i += 2)
                        {
                            int sourcePos = i + bufferHead;
                            int targetPos = (channels / 2) * i + count;
                            data[targetPos] = currentBuffer[sourcePos];  // left to front left
                            data[targetPos + 1] = currentBuffer[sourcePos + 1];  // right to front right
                            data[targetPos + 2] = currentBuffer[sourcePos];  // left to surround left
                            data[targetPos + 3] = currentBuffer[sourcePos + 1];  // right to surround right
                        }
                        break;
                    case (5):  // 5 or 5.1 mux
                    case (6):
                        for (int i = 0; i < length; i += 2)
                        {
                            int sourcePos = i + bufferHead;
                            int targetPos = (channels / 2) * i + count;
                            data[targetPos] = currentBuffer[sourcePos];  // left to front left
                            data[targetPos + 1] = currentBuffer[sourcePos + 1];  // right to front right
                            data[targetPos + 4] = currentBuffer[sourcePos];  // left to surround left
                            data[targetPos + 5] = currentBuffer[sourcePos + 1];  // right to surround right
                        }
                        break;
                    case (8):  // 7.1 surround mux
                        for (int i = 0; i < length; i += 2)
                        {
                            int sourcePos = i + bufferHead;
                            int targetPos = (channels / 2) * i + count;
                            data[targetPos] = currentBuffer[sourcePos];  // left to front left
                            data[targetPos + 1] = currentBuffer[sourcePos + 1];  // right to front right
                            data[targetPos + 4] = currentBuffer[sourcePos];  // left to rear left
                            data[targetPos + 5] = currentBuffer[sourcePos + 1];  // right to rear right
                            data[targetPos + 6] = currentBuffer[sourcePos];  // left to surround left
                            data[targetPos + 7] = currentBuffer[sourcePos + 1];  // right to surround right
                        }
                        break;
                    default:  // something else.. assume first two channels are left/right.
                        for (int i = 0; i < length; i += 2)
                        {
                            int sourcePos = i + bufferHead;
                            int targetPos = (channels / 2) * i + count;
                            data[targetPos] = currentBuffer[sourcePos];  // left to left
                            data[targetPos + 1] = currentBuffer[sourcePos + 1];  // right to right
                        }
                        break;
                }

                bufferHead += length;
                count += length * channels / 2;
            }
        }
    }
}
