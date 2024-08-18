using NAudio.Wave;

namespace CellDialer
{
    public class AudioStreamer
    {
        private WaveInEvent waveIn; // Handles capturing audio input
        private WaveOutEvent waveOut; // Handles playing audio output
        private BufferedWaveProvider buffer; // Buffers the captured audio data

        public AudioStreamer(int inputDeviceIndex, int outputDeviceIndex, int sampleRate = 8000, int channels = 1)
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
            waveIn.DataAvailable += OnDataAvailable;
            waveOut.Init(buffer); // Initialize the output device with the buffered audio data
        }

        // Callback when audio data is captured, adding it to the playback buffer
        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        // Start capturing and playing audio
        public void Start()
        {
            waveIn.StartRecording();
            waveOut.Play();
        }

        // Stop capturing and playing audio
        public void Stop()
        {
            waveIn.StopRecording();
            waveOut.Stop();
        }
    }
}

// Usage example:
// var streamer = new AudioStreamer(1, 0);
// streamer.Start();
