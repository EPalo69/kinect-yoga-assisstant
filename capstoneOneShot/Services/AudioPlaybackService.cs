using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;

namespace capstoneOneShot.Services
{
    public class AudioPlaybackService
    {
        private readonly MediaPlayer _player;
        private readonly Queue<string> _queue = new Queue<string>();
        private readonly object _lock = new object();
        private bool _isPlaying = false;
        
        // We use this to enforce a 1-second gap between instructions
        private DateTime _lastPlayEndTime = DateTime.MinValue;
        private bool _isGapWait = false;

        private string _lastInstruction = string.Empty;
        private string _lastCorrection = string.Empty;

        public AudioPlaybackService()
        {
            _player = new MediaPlayer();
            _player.MediaEnded += OnMediaEnded;
            _player.MediaFailed += OnMediaEnded; // Proceed if failed
        }

        public void Play(string instructionAudio, string correctionAudio)
        {
            // Correction always takes priority
            if (!string.IsNullOrWhiteSpace(correctionAudio))
            {
                if (correctionAudio == _lastCorrection) return;
                _lastCorrection = correctionAudio;
                Enqueue(correctionAudio);
                return;
            }

            if (!string.IsNullOrWhiteSpace(instructionAudio))
            {
                if (instructionAudio == _lastInstruction) return;
                _lastInstruction = instructionAudio;
                Enqueue(instructionAudio);
            }
        }

        private void Enqueue(string relativePath)
        {
            lock (_lock)
            {
                if (_queue.Count > 0 && _queue.Peek() == relativePath) return;

                _queue.Enqueue(relativePath);
                TryPlayNext();
            }
        }

        public bool IsPlaying
        {
            get 
            { 
                lock (_lock) 
                { 
                    return _isPlaying || _queue.Count > 0 || _isGapWait; 
                } 
            }
        }

        private void TryPlayNext()
        {
            lock (_lock)
            {
                if (_isPlaying || _queue.Count == 0) return;

                // Check if we need to wait for the 1-second gap
                var timeSinceLast = DateTime.Now - _lastPlayEndTime;
                if (timeSinceLast.TotalSeconds < 1.0)
                {
                    _isGapWait = true;
                    // Schedule to try again after the gap
                    Task.Delay(TimeSpan.FromSeconds(1.0) - timeSinceLast).ContinueWith(_ => 
                    {
                        lock (_lock)
                        {
                            _isGapWait = false;
                            TryPlayNext();
                        }
                    });
                    return;
                }

                _isPlaying = true;
                var next = _queue.Dequeue();
                
                var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", next);
                if (File.Exists(fullPath))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _player.Open(new Uri(fullPath));
                        _player.Play();
                    });
                }
                else
                {
                    // Skip if file doesn't exist
                    _isPlaying = false;
                    TryPlayNext();
                }
            }
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            lock (_lock)
            {
                _isPlaying = false;
                _lastPlayEndTime = DateTime.Now;
                TryPlayNext();
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _queue.Clear();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _player.Stop();
                    _player.Close();
                });
                _isPlaying = false;
                _isGapWait = false;
                _lastInstruction = string.Empty;
                _lastCorrection = string.Empty;
                _lastPlayEndTime = DateTime.MinValue; // No gap wait on reset
            }
        }
    }
}
