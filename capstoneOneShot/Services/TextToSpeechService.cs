using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Speech.Synthesis;

namespace capstoneOneShot.Services
{
    public class TextToSpeechService
    {
        private readonly SpeechSynthesizer _synth;
        private readonly Queue<string> _queue = new Queue<string>();
        private readonly object _lock = new object();
        private bool _isSpeaking = false;
        private DateTime _lastSpokenTime = DateTime.MinValue;
        private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(1);

        private string _lastInstruction = string.Empty;
        private string _lastCorrection = string.Empty;

        public TextToSpeechService()
        {
            _synth = new SpeechSynthesizer();
            _synth.Rate = 0;   // adjust if needed
            _synth.Volume = 100;
            _synth.SpeakCompleted += OnSpeakCompleted;

            // Pick best available voice
            var preferred = _synth.GetInstalledVoices()
                .FirstOrDefault(v => v.VoiceInfo.Name.Contains("Zira")
                                  || v.VoiceInfo.Name.Contains("Aria"));
            if (preferred != null)
                _synth.SelectVoice(preferred.VoiceInfo.Name);
        }

        // 🔹 MAIN ENTRY POINT
        public void Speak(string instruction, string correction)
        {
            // Correction always takes priority
            if (!string.IsNullOrWhiteSpace(correction))
            {
                if (correction == _lastCorrection) return;
                _lastCorrection = correction;
                Enqueue(correction);
                return;
            }

            if (!string.IsNullOrWhiteSpace(instruction))
            {
                if (instruction == _lastInstruction) return;
                _lastInstruction = instruction;
                Enqueue(instruction);
            }
        }

        private void Enqueue(string text)
        {
            lock (_lock)
            {
                // Don't queue the same message twice in a row
                if (_queue.Count > 0 && _queue.Peek() == text) return;

                _queue.Enqueue(text);
                TryPlayNext();
            }
        }

        public bool IsSpeaking
        {
            get { lock (_lock) { return _isSpeaking || _queue.Count > 0; } }
        }

        private void TryPlayNext()
        {
            // Must be called inside _lock
            if (_isSpeaking || _queue.Count == 0) return;

            _isSpeaking = true;
            var next = _queue.Dequeue();
            // SpeakAsync on a thread so it doesn't block UI
            Task.Run(() => _synth.SpeakAsync(next));
        }

        private void OnSpeakCompleted(object sender, SpeakCompletedEventArgs e)
        {
            lock (_lock)
            {
                _isSpeaking = false;
                TryPlayNext();
            }
        }

        // Optional: reset state when pose changes
        public void Reset()
        {
            lock (_lock)
            {
                _queue.Clear();
                _synth.SpeakAsyncCancelAll();
                _isSpeaking = false;
                _lastInstruction = string.Empty;
                _lastCorrection = string.Empty;
            }
        }
    }
}
