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

namespace CellDialer
{
    class Program
    {
        static void Main(string[] args)
        {
            // Main program loop
            try
            {
                while (true)
                {
                    // Display menu options to the user
                    Console.WriteLine("Choose an option:");
                    Console.WriteLine("1. List Audio Devices");
                    Console.WriteLine("2. Test Audio Streaming (Loopback)");
                    Console.WriteLine("3. Test Serial Audio Phone Call");
                    Console.WriteLine("4. Exit");
                    Console.Write("Enter your choice: ");

                    // Read user input and handle potential null input
                    string? choice = Console.ReadLine(); // 'choice' can be null, so it's marked as nullable

                    switch (choice)
                    {
                        case "1":
                            // Call the method to list available audio devices
                            TestAudioDeviceManager();
                            break;
                        case "2":
                            // Call the method to test audio streaming (loopback)
                            TestAudioStreamer();
                            break;
                        case "3":
                            // Call the method to test the serial audio phone call functionality
                            TestSerialAudioPhone();
                            break;
                        case "4":
                            // Exit the program
                            Console.WriteLine("Exiting...");
                            return;
                        default:
                            // Handle invalid menu options
                            Console.WriteLine("Invalid choice. Please try again.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle any unexpected errors that occur in the main program loop
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        // Method to list available audio devices
        static void TestAudioDeviceManager()
        {
            try
            {
                var manager = new AudioDeviceManager();
                manager.ListDevices(); // Display the list of audio devices
            }
            catch (Exception ex)
            {
                // Handle errors that may occur when listing audio devices
                Console.WriteLine("Error while listing audio devices: " + ex.Message);
            }
        }

        // Method to test audio streaming (loopback)
        static void TestAudioStreamer()
        {
            try
            {
                // Prompt the user to enter the input device index
                Console.Write("Enter input device index: ");
                if (!int.TryParse(Console.ReadLine(), out int inputDeviceIndex)) // Validate user input
                {
                    Console.WriteLine("Invalid input device index.");
                    return; // Exit the method if the input is invalid
                }

                // Prompt the user to enter the output device index
                Console.Write("Enter output device index: ");
                if (!int.TryParse(Console.ReadLine(), out int outputDeviceIndex)) // Validate user input
                {
                    Console.WriteLine("Invalid output device index.");
                    return; // Exit the method if the input is invalid
                }

                // Initialize the audio streamer with the specified input and output devices
                var streamer = new AudioStreamer(inputDeviceIndex, outputDeviceIndex);
                streamer.Start(); // Start audio streaming

                Console.WriteLine("Audio streaming started. Press any key to stop...");
                Console.ReadKey(); // Wait for the user to press a key to stop streaming

                streamer.Stop(); // Stop audio streaming
                Console.WriteLine("Audio streaming stopped.");
            }
            catch (Exception ex)
            {
                // Handle errors that may occur during audio streaming
                Console.WriteLine("Error during audio streaming: " + ex.Message);
            }
        }

        // Method to test making a phone call using the serial audio phone functionality
        static void TestSerialAudioPhone()
        {
            try
            {
                // Prompt the user to enter the phone number to dial
                Console.Write("Enter phone number to dial: ");
                string? phoneNumber = Console.ReadLine(); // Read user input

                // Validate that the phone number is not empty or whitespace
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    Console.WriteLine("Invalid phone number.");
                    return; // Exit the method if the input is invalid
                }

                // Initialize the SerialAudioPhone and start the call
                var phone = new SerialAudioPhone();
                phone.StartCall(phoneNumber); // Start the call with the provided phone number

                // Console.WriteLine("Call started. Press 'Esc' to end the call.");
            }
            catch (Exception ex)
            {
                // Handle errors that may occur during the phone call
                Console.WriteLine("Error during phone call: " + ex.Message);
            }
        }
    }
}

