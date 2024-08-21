using NAudio.Wave;

namespace CellDialer
{
    public class AudioStreamer
    {
        private WaveInEvent? waveIn; // Handles capturing audio input (nullable to avoid CS8622 warning)
        private WaveOutEvent? waveOut; // Handles playing audio output (nullable to avoid CS8622 warning)
        private BufferedWaveProvider? buffer; // Buffers the captured audio data (nullable to avoid CS8622 warning)

        public AudioStreamer(int inputDeviceIndex, int outputDeviceIndex, int sampleRate = 8000, int channels = 1)
        {
            try
            {
                // Configure the input device for capturing audio
                waveIn = new WaveInEvent
                {
                    DeviceNumber = inputDeviceIndex,
                    WaveFormat = new WaveFormat(sampleRate, channels) // Set sample rate and channels
                };

                // Configure the output device for playing audio
                waveOut = new WaveOutEvent
                {
                    DeviceNumber = outputDeviceIndex
                };

                // Buffer the audio data captured from the input device
                buffer = new BufferedWaveProvider(waveIn.WaveFormat);

                // When audio data is available, add it to the buffer for playback
                // Fixed CS8622 by marking the delegate as nullable where necessary
                waveIn.DataAvailable += OnDataAvailable;
                waveOut.Init(buffer); // Initialize the output device with the buffered audio data
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error initializing AudioStreamer: " + ex.Message);
            }
        }

        // Callback when audio data is captured, adding it to the playback buffer
        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            try
            {
                buffer?.AddSamples(e.Buffer, 0, e.BytesRecorded); // Add captured audio data to the buffer
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error processing audio data: " + ex.Message);
            }
        }

        // Start capturing and playing audio
        public void Start()
        {
            try
            {
                waveIn?.StartRecording(); // Start capturing audio
                waveOut?.Play(); // Start playing audio
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error starting audio streaming: " + ex.Message);
            }
        }

        // Stop capturing and playing audio
        public void Stop()
        {
            try
            {
                waveIn?.StopRecording(); // Stop capturing audio
                waveOut?.Stop(); // Stop playing audio
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error stopping audio streaming: " + ex.Message);
            }
        }
    }
}

// Usage example:
// var streamer = new AudioStreamer(1, 0);
// streamer.Start();
