using System.IO.Ports;
using System.Management;
using NAudio.Wave;

namespace CellDialer
{
    public class SerialAudioPhone
    {
        private SerialPort? atPort; // Serial port for sending AT commands, initialized later
        private SerialPort? audioPort; // Serial port for transmitting audio data, initialized later
        private WaveInEvent? waveIn; // Handles capturing audio input, initialized later
        private WaveOutEvent? waveOut; // Handles playing audio output, initialized later
        private BufferedWaveProvider? buffer; // Buffers the captured audio data, initialized later
        private bool isCallActive; // Flag to track if the call is active

        // Device identifiers for the AT port and the Audio port
        private const string AtPortDeviceId = "USB\\VID_1E0E&PID_9001&MI_02";
        private const string AudioPortDeviceId = "USB\\VID_1E0E&PID_9001&MI_04";

        public SerialAudioPhone(int baudRate = 115200, int sampleRate = 8000, int channels = 1)
        {
            try
            {
                // Locate the serial ports based on their device IDs
                string? atPortName = FindSerialPortByDeviceId(AtPortDeviceId);
                string? audioPortName = FindSerialPortByDeviceId(AudioPortDeviceId);

                // If either port is not found, throw an exception
                if (atPortName is null || audioPortName is null)
                {
                    throw new InvalidOperationException("Unable to locate one or both required serial ports.");
                }

                // Initialize the serial ports with the located COM port names
                atPort = new SerialPort(atPortName, baudRate);
                audioPort = new SerialPort(audioPortName, baudRate);

                // Configure the input device for capturing audio
                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(sampleRate, channels),
                    BufferMilliseconds = 30 // Moderate buffer size for stable audio
                };

                // Configure the output device for playing audio
                waveOut = new WaveOutEvent
                {
                    DesiredLatency = 30, // Balanced latency for smooth playback
                    NumberOfBuffers = 4 // Use a moderate number of buffers to handle data flow
                };

                buffer = new BufferedWaveProvider(waveIn.WaveFormat)
                {
                    BufferLength = 8192, // Balanced buffer length for smoother playback
                    DiscardOnBufferOverflow = true // Discard old data if buffer overflows to avoid full buffer issues
                };

                // Adjust the volume of the audio playback if needed
                waveOut.Volume = 0.8f; // Slightly reduce volume to avoid clipping

                // Send captured audio data to the audio serial port
                waveIn.DataAvailable += (sender, e) =>
                {
                    try
                    {
                        audioPort.Write(e.Buffer, 0, e.BytesRecorded);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine("I/O Error while writing audio data: " + ex.Message);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Console.WriteLine("Access Error while writing audio data: " + ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Unexpected Error while writing audio data: " + ex.Message);
                    }
                };

                waveOut.Init(buffer); // Initialize the output device with the buffered audio data

                isCallActive = false; // Set the call as inactive initially
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error initializing SerialAudioPhone: " + ex.Message);
            }
        }

       
        // Method to find the correct serial port based on the device identifier
        private string? FindSerialPortByDeviceId(string deviceId)
        {
            try
            {
#pragma warning disable CS8602 // Derefrence of possible null reference 
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity"))
                {
                    foreach (var device in searcher.Get())
                    {
                        string? deviceID = device["DeviceID"]?.ToString();
                        if (deviceID != null && deviceID.Contains(deviceId))
                        {
                            string? portName = device["Name"]?.ToString().Split('(').LastOrDefault()?.Replace(")", "");
                            return portName;
                        }
                    }
                }
#pragma warning restore CS8602 // Re-enable warnings 
            }
            catch (ManagementException ex)
            {
                Console.WriteLine("Management Error while searching for device: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected Error while searching for device: " + ex.Message);
            }
            return null; // Return null if the device is not found or if an error occurs
        }

        // Start the phone call
        public void StartCall(string phoneNumber)
        {
            try
            {
                isCallActive = true;
                atPort?.Open(); // Open the AT command serial port
                audioPort?.Open(); // Open the audio serial port

                // Send initial AT commands to set up the call
                SendCommand("AT");
                SendCommand($"ATD{phoneNumber};");

                // Critical AT command to enable audio over the serial port
                SendCommand("AT+CPCMREG=1");

                // Start capturing and playing audio
                waveIn?.StartRecording();
                waveOut?.Play();

                // Start a thread to monitor keyboard input for ending the call
                Thread inputThread = new Thread(MonitorKeyboardInput)
                {
                    Priority = ThreadPriority.Highest // Set thread priority to highest to reduce latency
                };
                inputThread.Start();

                while (isCallActive)
                {
                    try
                    {
                        // Check for responses from the AT command serial port
                        if (atPort != null && atPort.BytesToRead > 0)
                        {
                            string response = atPort.ReadExisting();
                            Console.WriteLine(response);
                            if (response.Contains("NO CARRIER"))
                            {
                                isCallActive = false; // End the call if "NO CARRIER" is detected
                            }
                        }

                        // Read and play incoming audio data from the audio serial port
                        if (audioPort != null && audioPort.BytesToRead > 0)
                        {
                            byte[] audioData = new byte[audioPort.BytesToRead];
                            audioPort.Read(audioData, 0, audioData.Length);

                            // Prevent buffer overflow by checking available space
                            if (buffer != null && buffer.BufferedDuration.TotalMilliseconds < 100)
                            {
                                buffer.AddSamples(audioData, 0, audioData.Length); // Add the received audio data to the playback buffer
                            }
                            else
                            {
                                // Skip adding data to avoid overflow if the buffer is too full
                                Console.WriteLine("Skipping audio data to avoid buffer overflow.");
                            }
                        }
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine("I/O Error during call handling: " + ex.Message);
                        isCallActive = false;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Console.WriteLine("Access Error during call handling: " + ex.Message);
                        isCallActive = false;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Unexpected Error during call handling: " + ex.Message);
                        isCallActive = false;
                    }

                    Thread.Sleep(10); // Short delay to prevent high CPU usage while keeping latency low
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error starting the call: " + ex.Message);
            }
            finally
            {
                EndCall(); // Ensure the call is ended cleanly and resources are released
            }
        }

        // Monitor keyboard input to end the call when 'Esc' is pressed
        private void MonitorKeyboardInput()
        {
            try
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
            catch (Exception ex)
            {
                Console.WriteLine("Error monitoring keyboard input: " + ex.Message);
            }
        }

        // Send an AT command through the serial port
        private void SendCommand(string command)
        {
            try
            {
                atPort?.WriteLine($"{command}\r");
                Thread.Sleep(50); // Reduced delay to speed up command processing
            }
            catch (IOException ex)
            {
                Console.WriteLine("I/O Error sending AT command: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine("Access Error sending AT command: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unexpected Error sending AT command: " + ex.Message);
            }
        }

        // End the phone call and clean up resources
        public void EndCall()
        {
            try
            {
                SendCommand("AT+CHUP"); // Hang up the call
                SendCommand("AT+CPCMREG=0,1"); // Disable the audio channel on the modem
                waveIn?.StopRecording(); // Stop capturing audio from the input device
                waveOut?.Stop(); // Stop playing audio to the output device
                if (atPort?.IsOpen == true) atPort.Close(); // Close the AT command serial port
                if (audioPort?.IsOpen == true) audioPort.Close(); // Close the audio serial port
                Console.WriteLine("Call ended.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error ending the call: " + ex.Message);
            }
        }
    }
}
