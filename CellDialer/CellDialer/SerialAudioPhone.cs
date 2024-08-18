using System.IO.Ports;
using NAudio.Wave;

namespace CellDialer
{
    public class SerialAudioPhone
    {
        private SerialPort atPort; // Serial port for sending AT commands
        private SerialPort audioPort; // Serial port for transmitting audio data
        private WaveInEvent waveIn; // Handles capturing audio input
        private WaveOutEvent waveOut; // Handles playing audio output
        private BufferedWaveProvider buffer; // Buffers the captured audio data
        private bool isCallActive; // Flag to track if the call is active

        public SerialAudioPhone(string atPortName, string audioPortName, int baudRate = 115200, int sampleRate = 8000, int channels = 1)
        {
            // Initialize the serial ports with the given parameters
            atPort = new SerialPort(atPortName, baudRate);
            audioPort = new SerialPort(audioPortName, baudRate);

            // Configure the input device for capturing audio
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(sampleRate, channels)
            };

            // Configure the output device for playing audio
            waveOut = new WaveOutEvent();
            buffer = new BufferedWaveProvider(waveIn.WaveFormat);

            // Send captured audio data to the audio serial port
            waveIn.DataAvailable += (sender, e) => audioPort.Write(e.Buffer, 0, e.BytesRecorded);
            waveOut.Init(buffer); // Initialize the output device with the buffered audio data

            isCallActive = true; // Set the call as active
        }

        // Start the phone call
        public void StartCall(string phoneNumber)
        {
            try
            {
                atPort.Open(); // Open the AT command serial port
                audioPort.Open(); // Open the audio serial port

                // Send initial AT commands to set up the call
                SendCommand("AT");
                SendCommand($"ATD{phoneNumber};");

                // Critical AT command to enable audio over the serial port
                SendCommand("AT+CPCMREG=1");

                waveIn.StartRecording(); // Start capturing audio
                waveOut.Play(); // Start playing audio

                // Start a thread to monitor keyboard input for ending the call
                Thread inputThread = new Thread(MonitorKeyboardInput);
                inputThread.Start();

                while (isCallActive)
                {
                    // Check for responses from the AT command serial port
                    if (atPort.BytesToRead > 0)
                    {
                        string response = atPort.ReadExisting();
                        Console.WriteLine(response);
                        if (response.Contains("NO CARRIER"))
                        {
                            isCallActive = false; // End the call if "NO CARRIER" is detected
                        }
                    }

                    // Read and play incoming audio data from the audio serial port
                    if (audioPort.BytesToRead > 0)
                    {
                        byte[] audioData = new byte[audioPort.BytesToRead];
                        audioPort.Read(audioData, 0, audioData.Length);
                        buffer.AddSamples(audioData, 0, audioData.Length);
                    }

                    Thread.Sleep(100); // Brief delay to prevent high CPU usage
                }
            }
            finally
            {
                EndCall(); // Ensure the call is ended cleanly
            }
        }

        // Monitor keyboard input to end the call when 'Esc' is pressed
        private void MonitorKeyboardInput()
        {
            Console.WriteLine("Press 'Esc' to end the call.");
            while (isCallActive)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                {
                    isCallActive = false; // End the call if 'Esc' is pressed
                }
            }
        }

        // Send an AT command through the serial port
        private void SendCommand(string command)
        {
            atPort.WriteLine($"{command}\r");
            Thread.Sleep(100); // Brief delay to ensure the command is processed
        }

        // End the phone call and clean up resources
        public void EndCall()
        {
            SendCommand("AT+CHUP"); // Hang up the call
            SendCommand("AT+CPCMREG=0,1"); // Close the audio serial port
            waveIn.StopRecording(); // Stop capturing audio
            waveOut.Stop(); // Stop playing audio
            if (atPort.IsOpen) atPort.Close(); // Close the AT command serial port
            if (audioPort.IsOpen) audioPort.Close(); // Close the audio serial port
            Console.WriteLine("Call ended.");
        }
    }
}

// Usage example:
// var phone = new SerialAudioPhone("COM3", "COM4");
// phone.StartCall("10086");
