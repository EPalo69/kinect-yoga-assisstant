using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace capstoneOneShot.Services
{
    public enum BodyDetectionStatus
    {
        Detected,        // skeleton fully tracked
        PartialDetect,   // skeleton seen but not fully tracked
        NotDetected      // no skeleton at all
    }

    public class KinectManager
    {
        private KinectSensor _sensor;
        public KinectSensor Sensor => _sensor;

        /// <summary>True when real Kinect hardware was found and successfully started.</summary>
        public bool IsConnected => _sensor != null && _sensor.IsRunning;

        // Skeleton event — fires with the first fully tracked skeleton
        public event Action<Skeleton[]> SkeletonFrameReady;

        // Color camera event — fires with each new camera frame
        public event Action<BitmapSource> ColorFrameReady;

        // Body detection status event — fires whenever tracking status changes
        public event Action<BodyDetectionStatus> BodyStatusChanged;

        // Voice command event — fires when a recognized phrase is spoken
        public event Action<string> VoiceCommandHeard;

        // Connection status event — fires when Kinect connects or disconnects
        public event Action<bool> ConnectionStatusChanged;

        private BodyDetectionStatus _lastStatus = BodyDetectionStatus.NotDetected;
        private object _speechEngine; // Holds either Microsoft.Speech or System.Speech engine

        public KinectManager()
        {
            try
            {
                KinectSensor.KinectSensors.StatusChanged += KinectSensors_StatusChanged;
            }
            catch { }
        }

        private void KinectSensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (e.Status == KinectStatus.Disconnected)
                {
                    if (_sensor == e.Sensor || _sensor == null)
                    {
                        Shutdown();
                        _sensor = null;
                        ConnectionStatusChanged?.Invoke(false);
                    }
                }
                else if (e.Status == KinectStatus.Connected)
                {
                    if (_sensor == null)
                    {
                        Initialize();
                        ConnectionStatusChanged?.Invoke(IsConnected);
                    }
                }
            });
        }


        // ---------------------------------------------------------------
        // Initialize and start the sensor
        // ---------------------------------------------------------------
        public bool Initialize()
        {
            // Support both Kinect SDK v1 (KinectSensor.KinectSensors) and v2 (KinectSensor.GetDefault)
            try
            {
                // Attempt v1 API
                var sensorsProp = typeof(KinectSensor).GetProperty("KinectSensors");
                if (sensorsProp != null)
                {
                    var sensors = sensorsProp.GetValue(null) as System.Collections.IEnumerable;
                    if (sensors != null)
                    {
                        foreach (var s in sensors)
                        {
                            var statusProp = s.GetType().GetProperty("Status");
                            if (statusProp != null)
                            {
                                var status = statusProp.GetValue(s);
                                if (status != null && status.ToString() == "Connected")
                                {
                                    _sensor = s as KinectSensor;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (_sensor == null)
            {
                try
                {
                    // Attempt v2 API
                    var getDefault = typeof(KinectSensor).GetMethod("GetDefault");
                    if (getDefault != null)
                    {
                        _sensor = getDefault.Invoke(null, null) as KinectSensor;
                    }
                }
                catch { }
            }

            if (_sensor == null) return false;

            // Set smoothing parameters for skeleton tracking
            var smoothing = new TransformSmoothParameters
            {
                Smoothing = 0.7f,   // higher = smoother, more lag (0.0–1.0)
                Correction = 0.3f,   // how fast to snap back to raw data
                Prediction = 1.0f,   // predict 1 frame ahead
                JitterRadius = 0.05f,  // ignore movement smaller than 5cm
                MaxDeviationRadius = 0.04f   // clamp jumps to 4cm per frame
            };

            // Enable skeleton stream
            _sensor.SkeletonStream.Enable(smoothing);

            // Enable color stream at 640x480 @ 30fps
            _sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);

            // Hook events
            _sensor.SkeletonFrameReady += OnSkeletonFrameReady;
            _sensor.ColorFrameReady += OnColorFrameReady;

            _sensor.Start();

            // Initialize Speech Recognition
            InitializeSpeechRecognition();

            return true;
        }

        // ---------------------------------------------------------------
        // Speech Recognition Setup
        // ---------------------------------------------------------------
        private void InitializeSpeechRecognition()
        {
            try
            {
                var engine = new System.Speech.Recognition.SpeechRecognitionEngine();

                var choices = new System.Speech.Recognition.Choices();
                choices.Add(new string[] { "pause", "resume" });

                var gb = new System.Speech.Recognition.GrammarBuilder();
                gb.Append(choices);

                var g = new System.Speech.Recognition.Grammar(gb);
                engine.LoadGrammar(g);

                engine.SpeechRecognized += (s, e) =>
                {
                    if (e.Result.Confidence > 0.6f)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            VoiceCommandHeard?.Invoke(e.Result.Text.ToLower());
                        });
                    }
                };

                // Try to use the Kinect Microphone array first
                bool kinectMicSuccess = false;
                try
                {
                    if (_sensor != null && _sensor.IsRunning)
                    {
                        _sensor.AudioSource.EchoCancellationMode = EchoCancellationMode.None;
                        _sensor.AudioSource.AutomaticGainControlEnabled = false;
                        var kinectStream = _sensor.AudioSource.Start();
                        
                        var audioFormat = new System.Speech.AudioFormat.SpeechAudioFormatInfo(
                            System.Speech.AudioFormat.EncodingFormat.Pcm, 
                            16000, 16, 1, 32000, 2, null);
                            
                        engine.SetInputToAudioStream(kinectStream, audioFormat);
                        kinectMicSuccess = true;
                        System.Diagnostics.Debug.WriteLine("Using Kinect Microphone stream for voice commands.");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to bind Kinect Audio stream to System.Speech: {ex.Message}");
                }

                // Fallback to default user mic
                if (!kinectMicSuccess)
                {
                    engine.SetInputToDefaultAudioDevice();
                    System.Diagnostics.Debug.WriteLine("Using Default System Microphone for voice commands.");
                }

                engine.RecognizeAsync(System.Speech.Recognition.RecognizeMode.Multiple);
                _speechEngine = engine;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Speech recognition failed to initialize completely: {ex.Message}");
            }
        }


        // ---------------------------------------------------------------
        // Skeleton frame handler
        // ---------------------------------------------------------------
        private void OnSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (var frame = e.OpenSkeletonFrame())
            {
                if (frame != null)
                {
                    skeletons = new Skeleton[frame.SkeletonArrayLength];
                    frame.CopySkeletonDataTo(skeletons);
                }
            }

            // Determine body detection status
            var tracked = skeletons.Where(
                s => s.TrackingState == SkeletonTrackingState.Tracked).ToArray();
            var partial = skeletons.Where(
                s => s.TrackingState == SkeletonTrackingState.PositionOnly).ToArray();

            BodyDetectionStatus newStatus;
            if (tracked.Length > 0)
                newStatus = BodyDetectionStatus.Detected;
            else if (partial.Length > 0)
                newStatus = BodyDetectionStatus.PartialDetect;
            else
                newStatus = BodyDetectionStatus.NotDetected;

            // Only fire if status changed
            if (newStatus != _lastStatus)
            {
                _lastStatus = newStatus;
                BodyStatusChanged?.Invoke(newStatus);
            }

            // Fire skeleton event only if someone is fully tracked
            if (tracked.Length > 0)
                SkeletonFrameReady?.Invoke(tracked);
        }

        // ---------------------------------------------------------------
        // Color frame handler — converts raw bytes to a WPF BitmapSource
        // ---------------------------------------------------------------
        private void OnColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (var frame = e.OpenColorImageFrame())
            {
                if (frame == null) return;

                byte[] pixels = new byte[frame.PixelDataLength];
                frame.CopyPixelDataTo(pixels);

                // Create a WPF-compatible bitmap from the raw pixel data
                var bitmap = BitmapSource.Create(
                    frame.Width,
                    frame.Height,
                    96, 96,
                    PixelFormats.Bgr32,
                    null,
                    pixels,
                    frame.Width * frame.BytesPerPixel);

                bitmap.Freeze(); // required to pass across threads
                ColorFrameReady?.Invoke(bitmap);
            }
        }

        // ---------------------------------------------------------------
        // Shutdown
        // ---------------------------------------------------------------
        public void Shutdown()
        {
            if (_speechEngine != null)
            {
                if (_speechEngine is IDisposable disposableEngine)
                {
                    disposableEngine.Dispose();
                }
                _speechEngine = null;
            }

            if (_sensor != null && _sensor.IsRunning)
            {
                if (_sensor.AudioSource != null)
                {
                    _sensor.AudioSource.Stop();
                }

                _sensor.SkeletonFrameReady -= OnSkeletonFrameReady;
                _sensor.ColorFrameReady -= OnColorFrameReady;
                _sensor.Stop();
            }
        }
    }
}