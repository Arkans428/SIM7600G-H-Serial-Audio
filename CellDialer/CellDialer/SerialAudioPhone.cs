/*
MIT License

Copyright (c) 2024 Kiernan Verhagen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

#if WINDOWS
using NAudio.Wave;
#elif LINUX
using Alsa.Net;
using Alsa.Net.Audio;
#endif

namespace ModemTool
{
    public class SerialAudioPhone : IDisposable
    {
        #region Fields and Configuration

        // Serial ports for AT commands and audio data
        private SerialPort? atPort; // Serial port for sending AT commands
        private SerialPort? audioPort; // Serial port for transmitting audio data

#if WINDOWS
        private WaveInEvent? waveIn; // Handles capturing audio input (Windows)
        private WaveOutEvent? waveOut; // Handles playing audio output (Windows)
        private BufferedWaveProvider? buffer; // Buffers the captured audio data (Windows)
#elif LINUX
        private AudioCapture? waveIn; // Handles capturing audio input (Linux)
        private AudioPlayback? waveOut; // Handles playing audio output (Linux)
        private RingBuffer? buffer; // Buffers the captured audio data (Linux)
#endif

        private Thread? smsMonitoringThread; // Background thread for monitoring incoming SMS messages
        private bool isCallActive; // Flag indicating whether a call is currently active
        private bool isSmsMonitoringActive; // Flag indicating whether SMS monitoring is active
        private bool disposed = false; // Tracks whether the object has been disposed
        private bool isEchoSuppressionEnabled = true; // Flag to enable/disable echo suppression
        private float echoSuppressionFactor = 0.5f; // Echo suppression level (range 0 to 1)
        private bool verboseOutput = false; // Flag to control verbose logging for debugging

        // Device identifiers for the AT port and the Audio port on different platforms
        private const string WindowsAtPortDeviceId = "USB\\VID_1E0E&PID_9001&MI_02";
        private const string WindowsAudioPortDeviceId = "USB\\VID_1E0E&PID_9001&MI_04";
        private const string LinuxVendorId = "1e0e";
        private const string LinuxProductId = "9001";

        #endregion

        #region Constructor
        public SerialAudioPhone(int baudRate = 115200, int sampleRate = 8000, int channels = 1, bool verbose = false)
        {
            // Enable verbose output if requested
            verboseOutput = verbose;

            // Locate the serial ports based on their device IDs
            var (atPortName, audioPortName) = FindSerialPorts();

            // If either port is not found, throw an exception
            if (atPortName is null || audioPortName is null)
            {
                throw new InvalidOperationException("Unable to locate one or both required serial ports.");
            }

            // Initialize the serial ports using the detected port names
            atPort = new SerialPort(atPortName, baudRate);
            audioPort = new SerialPort(audioPortName, baudRate);

#if WINDOWS
            // Configure the input device for capturing audio (microphone) on Windows
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(sampleRate, channels),
                BufferMilliseconds = 30
            };

            // Configure the output device for playing audio (speakers) on Windows
            waveOut = new WaveOutEvent
            {
                DesiredLatency = 50,
                NumberOfBuffers = 4
            };

            // Initialize the buffer for storing audio data (Windows)
            buffer = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferLength = 4096,
                DiscardOnBufferOverflow = true
            };

            waveOut.Volume = 0.7f;

            // Attach event handler for capturing and processing audio data on Windows
            waveIn.DataAvailable += (sender, e) =>
            {
                try
                {
                    float adjustedVolume = waveOut.PlaybackState == PlaybackState.Playing ? echoSuppressionFactor : 1.0f;
                    byte[] adjustedBuffer = AdjustAudioVolume(e.Buffer, e.BytesRecorded, adjustedVolume);
                    audioPort.Write(adjustedBuffer, 0, e.BytesRecorded);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Audio error: " + ex.Message);
                }
            };

            waveOut.Init(buffer);

#elif LINUX
            // Configure audio input and output for Linux using Alsa.Net
            waveIn = new AudioCapture(new AudioDevice(), sampleRate, channels);
            waveOut = new AudioPlayback(new AudioDevice(), sampleRate, channels);

            buffer = new RingBuffer(4096); // Adjust buffer size as needed

            waveIn.DataAvailable += (sender, e) =>
            {
                try
                {
                    float adjustedVolume = waveOut.Playing ? echoSuppressionFactor : 1.0f;
                    byte[] adjustedBuffer = AdjustAudioVolume(e.Buffer, e.BytesRecorded, adjustedVolume);
                    audioPort.Write(adjustedBuffer, 0, e.BytesRecorded);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Audio error: " + ex.Message);
                }
            };
#endif

            isCallActive = false;
        }

        #endregion

        #region Platform-Specific Methods for Serial Port Detection

        // Detect and return serial ports for the AT and Audio ports based on the operating system
        private (string? AtPort, string? AudioPort) FindSerialPorts()
        {
            if (IsWindows())
            {
                return FindPortsWindows();
            }
            else if (IsLinux())
            {
                return FindPortsLinux();
            }
            else
            {
                Console.WriteLine("Unsupported operating system.");
                return (null, null);
            }
        }

        // Check if the current platform is Windows
        private bool IsWindows() => Environment.OSVersion.Platform == PlatformID.Win32NT;

        // Check if the current platform is Linux or macOS
        private bool IsLinux() => Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX;

        // Find the appropriate serial ports for Windows systems
        private (string? AtPort, string? AudioPort) FindPortsWindows()
        {
            string? atPort = null;
            string? audioPort = null;

            try
            {
                // Query the system for USB devices using WMI (Windows Management Instrumentation)
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity"))
                {
                    foreach (var device in searcher.Get())
                    {
                        string? deviceId = device["DeviceID"]?.ToString();
                        string? name = device["Name"]?.ToString();
                        if (deviceId != null && name != null)
                        {
                            if (deviceId.Contains(WindowsAtPortDeviceId))
                            {
                                atPort = name.Split('(').LastOrDefault()?.Replace(")", ""); // Extract COM port name for AT port
                            }
                            else if (deviceId.Contains(WindowsAudioPortDeviceId))
                            {
                                audioPort = name.Split('(').LastOrDefault()?.Replace(")", ""); // Extract COM port name for Audio port
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting ports on Windows: {ex.Message}");
            }

            return (atPort, audioPort);
        }

        // Find the appropriate serial ports for Linux systems
        private (string? AtPort, string? AudioPort) FindPortsLinux()
        {
            string? atPort = null;
            string? audioPort = null;

            try
            {
                // List all top-level USB device folders (those without a colon in their name)
                var devices = System.IO.Directory.GetDirectories("/sys/bus/usb/devices/")
                    .Where(d => !d.Contains(":")); // Filter out subfolders like ":1.2", ":1.4"

                foreach (var device in devices)
                {
                    // Check for matching vendor and product IDs
                    if (System.IO.File.Exists($"{device}/idVendor") && System.IO.File.Exists($"{device}/idProduct"))
                    {
                        string vendorId = System.IO.File.ReadAllText($"{device}/idVendor").Trim();
                        string productId = System.IO.File.ReadAllText($"{device}/idProduct").Trim();

                        if (vendorId == LinuxVendorId && productId == LinuxProductId)
                        {
                            // Look for the specific child folders ending in ":1.2" and ":1.4"
                            var interfaceFolders = System.IO.Directory.GetDirectories(device)
                                .Where(f => f.EndsWith(":1.2") || f.EndsWith(":1.4"));

                            foreach (var interfaceFolder in interfaceFolders)
                            {
                                // Look for a ttyUSB* device name in the folder
                                var ttyDevice = System.IO.Directory.GetDirectories(interfaceFolder, "ttyUSB*").FirstOrDefault();
                                if (ttyDevice != null)
                                {
                                    // Extract the ttyUSB* name (e.g., ttyUSB2, ttyUSB4)
                                    string ttyName = System.IO.Path.GetFileName(ttyDevice);

                                    // Check if this device exists in the /dev/ folder
                                    string devPath = $"/dev/{ttyName}";
                                    if (System.IO.File.Exists(devPath))
                                    {
                                        // Determine if this is the AT port or Audio port based on the folder name
                                        if (interfaceFolder.EndsWith(":1.2"))
                                        {
                                            atPort = devPath;
                                        }
                                        else if (interfaceFolder.EndsWith(":1.4"))
                                        {
                                            audioPort = devPath;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting ports on Linux: {ex.Message}");
            }

            return (atPort, audioPort);
        }

        #endregion

        #region Core Call Management Methods

        // Start a phone call to a specified phone number
        public void StartCall(string phoneNumber)
        {
            try
            {
                // Validate the phone number using a custom validation function
                if (!IsValidPhoneNumber(phoneNumber))
                {
                    Console.WriteLine("Invalid phone number.");
                    return;
                }

                isCallActive = true; // Set the call as active

                // Open the AT command port if it's not already open
                if (atPort?.IsOpen == false)
                {
                    atPort.Open();
                }

                // Open the audio port if it's not already open
                if (audioPort?.IsOpen == false)
                {
                    audioPort.Open();
                }

                // Clear any leftover data in the serial buffers before starting the call
                atPort?.DiscardInBuffer();
                atPort?.DiscardOutBuffer();
                audioPort?.DiscardInBuffer();
                audioPort?.DiscardOutBuffer();

                Thread.Sleep(300); // Short delay to ensure the ports are fully initialized

                // Send necessary AT commands to configure the call settings
                SendCommand("AT+CGREG=0"); // Disable automatic gain control
                SendCommand("AT+CECM=7");
                SendCommand("AT+CECWB=0x0800");
                SendCommand("AT+CMICGAIN=3"); // Set microphone gain
                SendCommand("AT+COUTGAIN=4"); // Set output gain
                SendCommand("AT+CNSN=0x1000");

                // Send basic AT command to ensure modem readiness and dial the phone number
                SendCommand("AT");
                SendCommand($"ATD{phoneNumber};"); // Dial the phone number

                // Enable audio transmission over the serial port
                SendCommand("AT+CPCMREG=1");

#if WINDOWS
                waveIn?.StartRecording(); // Start capturing audio from the microphone
                waveOut?.Play(); // Start playing received audio
#elif LINUX
                waveIn.Start();
                waveOut.Start();
#endif

                // Start a thread to monitor keyboard input for user interaction
                Thread inputThread = new Thread(MonitorKeyboardInput)
                {
                    Priority = ThreadPriority.Highest
                };
                inputThread.Start();

                // Main loop to manage call activity
                while (isCallActive)
                {
                    try
                    {
                        // Check for responses from the AT command port (e.g., "NO CARRIER" indicating the call ended)
                        if (atPort != null && atPort.BytesToRead > 0)
                        {
                            string response = atPort.ReadExisting();
                            if (verboseOutput) Console.WriteLine(response);
                            if (response.Contains("NO CARRIER"))
                            {
                                isCallActive = false;
                            }
                        }

                        // Read and play incoming audio data from the audio port
                        if (audioPort != null && audioPort.BytesToRead > 0)
                        {
                            byte[] audioData = new byte[audioPort.BytesToRead];
                            audioPort.Read(audioData, 0, audioData.Length);

                            // Prevent buffer overflow by limiting the amount of buffered audio
#if WINDOWS
                            if (buffer != null && buffer.BufferedDuration.TotalMilliseconds < 100)
                            {
                                buffer.AddSamples(audioData, 0, audioData.Length); // Add received audio to the playback buffer
                            }
                            else
                            {
                                if (verboseOutput) Console.WriteLine("Skipping audio data to avoid buffer overflow.");
                            }
#elif LINUX
                            if (buffer != null && buffer.AvailableWrite > audioData.Length)
                            {
                                buffer.Write(audioData, 0, audioData.Length); // Add received audio to the playback buffer
                            }
                            else
                            {
                                if (verboseOutput) Console.WriteLine("Skipping audio data to avoid buffer overflow.");
                            }
#endif
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

                    Thread.Sleep(10); // Short delay to reduce CPU usage while maintaining low latency
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error starting the call: " + ex.Message);
            }
            finally
            {
                EndCall(); // Ensure the call is properly ended and resources are released
            }
        }

        // Method to send a DTMF (Dual-Tone Multi-Frequency) tone during a call
        public void SendDtmfTone(char tone)
        {
            // Validate that the tone is a valid DTMF character (0-9, *, #, A-D)
            if ("0123456789*#ABCD".IndexOf(tone) >= 0)
            {
                // Send the AT command to generate the specified DTMF tone
                SendCommand($"AT+VTS={tone}");

                // Optionally display the sent tone if verbose output is enabled
                if (verboseOutput) Console.WriteLine($"Sent DTMF tone: {tone}");
            }
            else
            {
                // Display an error message if the input tone is not valid
                Console.WriteLine($"Invalid DTMF tone: {tone}");
            }
        }

        // Monitor user input for ending the call or sending DTMF tones
        private void MonitorKeyboardInput()
        {
            try
            {
                Console.WriteLine("Press 'Esc' to end the call. Press any number key, *, #, A, B, C, or D to send DTMF tones.");
                while (isCallActive)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;

                        if (key == ConsoleKey.Escape)
                        {
                            isCallActive = false; // End the call if 'Esc' is pressed
                        }
                        else
                        {
                            // Check if the key corresponds to a valid DTMF tone
                            char dtmfTone = key switch
                            {
                                ConsoleKey.D1 => '1',
                                ConsoleKey.D2 => '2',
                                ConsoleKey.D3 => '3',
                                ConsoleKey.D4 => '4',
                                ConsoleKey.D5 => '5',
                                ConsoleKey.D6 => '6',
                                ConsoleKey.D7 => '7',
                                ConsoleKey.D8 => '8',
                                ConsoleKey.D9 => '9',
                                ConsoleKey.D0 => '0',
                                ConsoleKey.A => 'A',
                                ConsoleKey.B => 'B',
                                ConsoleKey.C => 'C',
                                ConsoleKey.D => 'D',
                                ConsoleKey.Oem1 => '*',
                                ConsoleKey.OemPlus => '#',
                                _ => '\0'
                            };

                            if (dtmfTone != '\0')
                            {
                                SendDtmfTone(dtmfTone); // Send the DTMF tone if it's valid
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error monitoring keyboard input: " + ex.Message);
            }
        }

        // End the call and release resources
        public void EndCall()
        {
            try
            {
                SendCommand("AT+CHUP"); // Hang up the call
                SendCommand("AT+CPCMREG=0,1"); // Disable the audio channel on the modem

#if WINDOWS
                waveIn?.StopRecording(); // Stop capturing audio (Windows)
                waveOut?.Stop(); // Stop playing audio (Windows)
#elif LINUX
                waveIn.Stop(); // Stop capturing audio (Linux)
                waveOut.Stop(); // Stop playing audio (Linux)
#endif

                if (verboseOutput) Console.WriteLine("Call ended.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error ending the call: " + ex.Message);
            }
        }

        #endregion

        #region SMS Management Methods

        // Send a text message to a specified phone number
        public void SendTextMessage(string phoneNumber, string message)
        {
            try
            {
                if (atPort?.IsOpen == false)
                {
                    atPort.Open(); // Ensure the AT port is open
                }

                SendCommand("AT+CMGF=1"); // Set SMS mode to text mode
                SendCommand($"AT+CMGS=\"{phoneNumber}\""); // Specify the recipient

                atPort?.Write($"{message}{char.ConvertFromUtf32(26)}"); // Send the message followed by Ctrl+Z (end of message)
                if (verboseOutput) Console.WriteLine($"Message sent to {phoneNumber}: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending text message: {ex.Message}");
            }
        }

        // Read and display all stored SMS messages
        public void ReadTextMessages()
        {
            try
            {
                if (atPort?.IsOpen == false)
                {
                    atPort.Open(); // Ensure the AT port is open
                }

                SendCommand("AT+CMGF=1"); // Set SMS mode to text mode
                SendCommand("AT+CMGL=\"ALL\""); // Retrieve all stored messages

                if (atPort != null && atPort.BytesToRead > 0)
                {
                    string response = atPort.ReadExisting();
                    if (verboseOutput) Console.WriteLine("Received Messages:");
                    Console.WriteLine(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading text messages: {ex.Message}");
            }
        }

        // Start monitoring for incoming SMS messages in the background
        public void StartSmsMonitoring()
        {
            if (atPort?.IsOpen == false)
            {
                atPort.Open(); // Ensure the AT port is open
            }

            isSmsMonitoringActive = true;
            smsMonitoringThread = new Thread(MonitorIncomingSms)
            {
                IsBackground = true // Run as a background thread
            };
            smsMonitoringThread.Start();
        }

        // Monitor and handle incoming SMS messages
        private void MonitorIncomingSms()
        {
            try
            {
                SendCommand("AT+CNMI=2,1,0,0,0"); // Enable new message notifications

                while (isSmsMonitoringActive)
                {
                    if (atPort != null && atPort.BytesToRead > 0)
                    {
                        string response = atPort.ReadExisting();

                        // Check for SMS notifications
                        if (response.Contains("+CMTI:"))
                        {
                            // Extract the message index from the notification
                            var match = Regex.Match(response, @"\+CMTI: "".*?"",(\d+)");
                            if (match.Success)
                            {
                                int messageIndex = int.Parse(match.Groups[1].Value);

                                // Read the message content
                                SendCommand($"AT+CMGR={messageIndex}");
                                string messageContent = atPort.ReadExisting();

                                if (verboseOutput) Console.WriteLine("New SMS Received:");
                                Console.WriteLine(messageContent);

                                // Optionally delete the message after reading
                                SendCommand($"AT+CMGD={messageIndex}");
                            }
                        }
                    }

                    Thread.Sleep(500); // Sleep to reduce CPU usage
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error monitoring SMS: {ex.Message}");
            }
        }

        // Stop monitoring SMS messages
        public void StopSmsMonitoring()
        {
            isSmsMonitoringActive = false;
            smsMonitoringThread?.Join(); // Wait for the monitoring thread to finish
        }

        // Delete a specific SMS message by index
        public void DeleteSms(int messageIndex)
        {
            try
            {
                if (atPort?.IsOpen == false)
                {
                    atPort.Open(); // Ensure the AT port is open
                }

                SendCommand($"AT+CMGD={messageIndex}"); // Send the command to delete the message
                if (verboseOutput) Console.WriteLine($"Deleted message at index {messageIndex}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting SMS message: {ex.Message}");
            }
        }

        // Delete all stored SMS messages
        public void DeleteAllSms()
        {
            try
            {
                if (atPort?.IsOpen == false)
                {
                    atPort.Open(); // Ensure the AT port is open
                }

                SendCommand("AT+CMGDA=\"DEL ALL\""); // Send the command to delete all messages
                if (verboseOutput) Console.WriteLine("Deleted all SMS messages.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting all SMS messages: {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods

        // Send an AT command through the serial port
        private void SendCommand(string command)
        {
            try
            {
                atPort?.WriteLine($"{command}\r"); // Send the command followed by a carriage return
                Thread.Sleep(60); // Short delay for command processing

                if (verboseOutput) Console.WriteLine($"Sent Command: {command}");
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

        // Validate a phone number (simple validation)
        private bool IsValidPhoneNumber(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return false; // Invalid if null or empty
            }

            return true; // Valid if it passes basic checks
        }

        // Adjust the volume of an audio buffer by scaling the samples
        private byte[] AdjustAudioVolume(byte[] buffer, int length, float volumeFactor)
        {
            for (int i = 0; i < length; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                sample = (short)(sample * volumeFactor); // Scale the audio sample by the volume factor
                byte[] adjustedSample = BitConverter.GetBytes(sample);
                buffer[i] = adjustedSample[0];
                buffer[i + 1] = adjustedSample[1];
            }
            return buffer;
        }

        #endregion

        #region Disposal Methods

        // Dispose pattern implementation to release resources
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // Suppress finalization since manual disposal is handled
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return; // If already disposed, exit

            if (disposing)
            {
                StopSmsMonitoring(); // Stop SMS monitoring if active

#if WINDOWS
                waveIn?.Dispose(); // Dispose managed resources (Windows)
                waveOut?.Dispose(); // Dispose managed resources (Windows)
#elif LINUX
                waveIn.Dispose(); // Dispose managed resources (Linux)
                waveOut.Dispose(); // Dispose managed resources (Linux)
#endif

                // Close and dispose serial ports if they are open
                if (atPort?.IsOpen == true)
                {
                    atPort.Close();
                }
                atPort?.Dispose();

                if (audioPort?.IsOpen == true)
                {
                    audioPort.Close();
                }
                audioPort?.Dispose();
            }

            disposed = true; // Mark as disposed
        }

        ~SerialAudioPhone()
        {
            Dispose(false); // Destructor calls Dispose(false) for unmanaged resource cleanup
        }

        #endregion
    }
}

