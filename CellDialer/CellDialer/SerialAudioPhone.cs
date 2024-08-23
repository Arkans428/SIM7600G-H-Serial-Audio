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
using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;
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
        private Thread? smsMonitoringThread; // Thread for monitoring incoming SMS
        private bool isSmsMonitoringActive; // Flag to control SMS monitoring

        // Device identifiers for the AT port and the Audio port
        private const string AtPortDeviceId = "USB\\VID_1E0E&PID_9001&MI_02";
        private const string AudioPortDeviceId = "USB\\VID_1E0E&PID_9001&MI_04";

        public SerialAudioPhone(int baudRate = 115200, int sampleRate = 8000, int channels = 1)
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
                WaveFormat = new WaveFormat(sampleRate, channels), // Use standard telephony sample rate of 8000 Hz
                BufferMilliseconds = 30 // Moderate buffer size for stable audio
            };

            // Configure the output device for playing audio
            waveOut = new WaveOutEvent
            {
                DesiredLatency = 50, // Increased latency for smoother playback
                NumberOfBuffers = 4 // Use a moderate number of buffers to handle data flow
            };

            buffer = new BufferedWaveProvider(waveIn.WaveFormat)
            {
                BufferLength = 4096, // Reduced buffer length to avoid delays and stretching
                DiscardOnBufferOverflow = true // Discard old data if buffer overflows to avoid full buffer issues
            };

            // Set the output volume to maximum
            waveOut.Volume = 1.0f; // Maximize the volume to ensure the output is loud enough

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

        // Method to find the correct serial port based on the device identifier
        private string? FindSerialPortByDeviceId(string deviceId)
        {
            try
            {
#pragma warning disable CS8602, CA1416 // Dereference of possibly null reference. We know this isn't going to happen, but the compiler seems to think otherwise...
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
#pragma warning restore CS8602, CA1416 // Re-enable warnings
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
                // Validate the phone number against the North American Numbering Plan (NANP)
                if (!IsValidPhoneNumber(phoneNumber))
                {
                    Console.WriteLine("Invalid phone number. Please enter a 10- or 11-digit phone number.");
                    return; // Exit the method if the input is invalid
                }

                isCallActive = true;
                atPort?.Open(); // Open the AT command serial port
                audioPort?.Open(); // Open the audio serial port

                // Add a short delay to ensure ports are fully initialized
                Thread.Sleep(300);

                // Resend critical audio commands to ensure proper setup
                SendCommand("AT+CGREG=0"); // Disable AGC for more consistent volume
                SendCommand("AT+CECM=7");
                SendCommand("AT+CECWB=0x0800");
                SendCommand("AT+CMICGAIN=3");
                SendCommand("AT+COUTGAIN=5");
                SendCommand("AT+CNSN=0x1000");

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

        // Method to send a DTMF tone during the call
        public void SendDtmfTone(char tone)
        {
            // Validate that the tone is a valid DTMF character (0-9, *, #, A-D)
            if ("0123456789*#ABCD".IndexOf(tone) >= 0)
            {
                SendCommand($"AT+VTS={tone}"); // Send the DTMF tone
                Console.WriteLine($"Sent DTMF tone: {tone}");
            }
            else
            {
                Console.WriteLine($"Invalid DTMF tone: {tone}");
            }
        }

        // Monitor keyboard input to end the call or send DTMF tones
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
                            // Check for DTMF tones (0-9, *, #, A, B, C, D)
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
                                _ => '\0' // Invalid character, ignored
                            };

                            if (dtmfTone != '\0')
                            {
                                SendDtmfTone(dtmfTone);
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

        // Send an AT command through the serial port
        private void SendCommand(string command)
        {
            try
            {
                atPort?.WriteLine($"{command}\r");
                Thread.Sleep(80); // Reduced delay to speed up command processing
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
                //if (atPort?.IsOpen == true) atPort.Close(); // Close the AT command serial port
                if (audioPort?.IsOpen == true) audioPort.Close(); // Close the audio serial port
                Console.WriteLine("Call ended.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error ending the call: " + ex.Message);
            }
        }

        // Validate a phone number according to the North American Numbering Plan (NANP)
        private bool IsValidPhoneNumber(string? phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return false; // Return false if the phone number is null or whitespace
            }

            // Regular expression pattern for validating 10- or 11-digit NANP numbers
            string pattern = @"^(1?)([2-9][0-9]{2})([2-9][0-9]{2})([0-9]{4})$";

            // Validate the phone number against the pattern
            return Regex.IsMatch(phoneNumber, pattern);
        }

        // Method to send a text message
        public void SendTextMessage(string phoneNumber, string message)
        {
            try
            {
                if (atPort?.IsOpen == false)
                {
                    atPort.Open(); // Open the port if not already open
                }

                // Set the SMS mode to Text Mode
                SendCommand("AT+CMGF=1");

                // Specify the recipient phone number
                SendCommand($"AT+CMGS=\"{phoneNumber}\"");

                // Send the message text followed by the Ctrl+Z character to send the message
                atPort?.Write($"{message}{char.ConvertFromUtf32(26)}");
                Console.WriteLine($"Message sent to {phoneNumber}: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending text message: {ex.Message}");
            }
        }

        // Method to read and display incoming text messages
        public void ReadTextMessages()
        {
            try
            {
                if (atPort?.IsOpen == false)
                {
                    atPort.Open(); // Open the port if not already open
                }

                // Set the SMS mode to Text Mode
                SendCommand("AT+CMGF=1");

                // Read all messages from the storage
                SendCommand("AT+CMGL=\"ALL\"");

                // Read and display the incoming messages
                if (atPort != null && atPort.BytesToRead > 0)
                {
                    string response = atPort.ReadExisting();
                    Console.WriteLine("Received Messages:");
                    Console.WriteLine(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading text messages: {ex.Message}");
            }
        }

        // Method to start monitoring for incoming SMS messages
        public void StartSmsMonitoring()
        {
            if (atPort?.IsOpen == false)
            {
                atPort.Open(); // Ensure the AT port is open
            }

            isSmsMonitoringActive = true;
            smsMonitoringThread = new Thread(MonitorIncomingSms)
            {
                IsBackground = true
            };
            smsMonitoringThread.Start();
        }

        // Method to stop SMS monitoring
        public void StopSmsMonitoring()
        {
            isSmsMonitoringActive = false;
            smsMonitoringThread?.Join(); // Wait for the thread to finish
        }

        // Method to monitor for incoming SMS messages
        private void MonitorIncomingSms()
        {
            try
            {
                // Enable new message notifications
                SendCommand("AT+CNMI=2,1,0,0,0");

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

                                // Display the incoming message
                                Console.WriteLine("New SMS Received:");
                                Console.WriteLine(messageContent);

                                // Optional: Delete the message after reading
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

        // Method to delete a specific SMS message by index
        public void DeleteSms(int messageIndex)
        {
            try
            {
                if (atPort?.IsOpen == false)
                {
                    atPort.Open(); // Ensure the AT port is open
                }

                // Send the AT command to delete the message at the specified index
                SendCommand($"AT+CMGD={messageIndex}");
                Console.WriteLine($"Deleted message at index {messageIndex}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting SMS message: {ex.Message}");
            }
        }

        // Method to delete all SMS messages
        public void DeleteAllSms()
        {
            try
            {
                if (atPort?.IsOpen == false)
                {
                    atPort.Open(); // Ensure the AT port is open
                }

                // Send the AT command to delete all messages (index 1 to 4, which includes all typical storage slots)
                SendCommand("AT+CMGDA=\"DEL ALL\"");
                Console.WriteLine("Deleted all SMS messages.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting all SMS messages: {ex.Message}");
            }
        }
    }
}

