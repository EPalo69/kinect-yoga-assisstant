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

        private DateTime _lastSpokenTime = DateTime.MinValue;
        private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(1);

        private string _lastInstruction = string.Empty;
        private string _lastCorrection = string.Empty;

        public TextToSpeechService()
        {
            _synth = new SpeechSynthesizer();
            _synth.Rate = 0;   // adjust if needed
            _synth.Volume = 100;

            var preferred = _synth.GetInstalledVoices()
                .FirstOrDefault(v => v.VoiceInfo.Name.Contains("Zira"));
            if (preferred != null)
                _synth.SelectVoice(preferred.VoiceInfo.Name);
        }

        // 🔹 MAIN ENTRY POINT
        public void Speak(string instruction, string correction)
        {
            // Respect cooldown
            if (DateTime.Now - _lastSpokenTime < _cooldown)
                return;

            // 🔴 PRIORITY: Correction always overrides instruction
            if (!string.IsNullOrWhiteSpace(correction))
            {
                if (correction != _lastCorrection)
                {
                    SpeakInternal(correction);
                    _lastCorrection = correction;
                    _lastSpokenTime = DateTime.Now;
                }
                return;
            }

            // 🟢 Instruction (only if no correction active)
            if (!string.IsNullOrWhiteSpace(instruction))
            {
                if (instruction != _lastInstruction)
                {
                    SpeakInternal(instruction);
                    _lastInstruction = instruction;
                    _lastSpokenTime = DateTime.Now;
                }
            }
        }

        private void SpeakInternal(string text)
        {
            _synth.SpeakAsyncCancelAll(); // interrupt anything currently speaking
            _synth.SpeakAsync(text);
        }

        // Optional: reset state when pose changes
        public void Reset()
        {
            _lastInstruction = string.Empty;
            _lastCorrection = string.Empty;
        }
    }
}
