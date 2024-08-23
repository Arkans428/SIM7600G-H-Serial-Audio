using System;

namespace ModemTool
{
    class Program
    {
        static void Main(string[] args)
        {
            SerialAudioPhone phone;

            try
            {
                // Attempt to initialize SerialAudioPhone
                phone = new SerialAudioPhone();
            }
            catch (Exception ex)
            {
                // If initialization fails, display the error and wait for a keypress before exiting
                Console.WriteLine("Failed to initialize SerialAudioPhone:");
                Console.WriteLine(ex.Message);
                Console.WriteLine("Press any key to close the program...");
                Console.ReadKey();
                return; // Exit the program since initialization failed
            }

            // Start SMS monitoring as soon as the program starts
            phone.StartSmsMonitoring();

            try
            {
                while (true)
                {
                    // Display menu options to the user
                    Console.WriteLine("======SIM7600G-H Modem Tool======");
                    Console.WriteLine("Choose an option:");
                    Console.WriteLine("1. List System Audio Devices");
                    Console.WriteLine("2. Test Audio Streaming (Loopback)");
                    Console.WriteLine("3. Serial Audio Phone Call");
                    Console.WriteLine("4. Send Text Message");
                    Console.WriteLine("5. Read Text Messages");
                    Console.WriteLine("6. Delete a Specific SMS");
                    Console.WriteLine("7. Delete All SMS Messages");
                    Console.WriteLine("8. Exit");
                    Console.Write("Enter your choice: ");

                    // Read user input
                    string? choice = Console.ReadLine();

                    // Process user choice
                    switch (choice)
                    {
                        case "1":
                            TestAudioDeviceManager();
                            break;
                        case "2":
                            TestAudioStreamer();
                            break;
                        case "3":
                            TestSerialAudioPhone(phone);
                            break;
                        case "4":
                            SendTextMessage(phone);
                            break;
                        case "5":
                            ReadTextMessages(phone);
                            break;
                        case "6":
                            DeleteSpecificSms(phone);
                            break;
                        case "7":
                            DeleteAllSmsMessages(phone);
                            break;
                        case "8":
                            // Properly dispose of resources before exiting
                            phone.Dispose();
                            Console.WriteLine("Exiting...");
                            return;
                        default:
                            Console.WriteLine("Invalid choice. Please try again.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle any unexpected errors during the program execution
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
        static void TestSerialAudioPhone(SerialAudioPhone phone)
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

                // Start the call with the provided phone number
                phone.StartCall(phoneNumber);
            }
            catch (Exception ex)
            {
                // Handle errors that may occur during the phone call
                Console.WriteLine("Error during phone call: " + ex.Message);
            }
        }

        // Method to send a text message
        static void SendTextMessage(SerialAudioPhone phone)
        {
            try
            {
                // Prompt the user to enter the recipient's phone number
                Console.Write("Enter recipient's phone number: ");
                string? phoneNumber = Console.ReadLine();

                // Validate that the phone number is not empty or whitespace
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    Console.WriteLine("Invalid phone number.");
                    return; // Exit the method if the input is invalid
                }

                // Prompt the user to enter the text message
                Console.Write("Enter your message: ");
                string? message = Console.ReadLine();

                // Validate that the message is not empty or whitespace
                if (string.IsNullOrWhiteSpace(message))
                {
                    Console.WriteLine("Invalid message.");
                    return; // Exit the method if the input is invalid
                }

                // Send the text message
                phone.SendTextMessage(phoneNumber, message);
            }
            catch (Exception ex)
            {
                // Handle errors that may occur when sending the text message
                Console.WriteLine("Error sending text message: " + ex.Message);
            }
        }

        // Method to read text messages
        static void ReadTextMessages(SerialAudioPhone phone)
        {
            try
            {
                // Read and display the stored text messages
                phone.ReadTextMessages();
            }
            catch (Exception ex)
            {
                // Handle errors that may occur when reading the text messages
                Console.WriteLine("Error reading text messages: " + ex.Message);
            }
        }

        // Method to delete a specific SMS
        static void DeleteSpecificSms(SerialAudioPhone phone)
        {
            try
            {
                // Prompt the user to enter the index of the SMS to delete
                Console.Write("Enter the index of the SMS to delete: ");
                if (int.TryParse(Console.ReadLine(), out int messageIndex))
                {
                    phone.DeleteSms(messageIndex);
                }
                else
                {
                    Console.WriteLine("Invalid index.");
                }
            }
            catch (Exception ex)
            {
                // Handle errors that may occur when deleting the SMS
                Console.WriteLine($"Error deleting SMS: {ex.Message}");
            }
        }

        // Method to delete all SMS messages
        static void DeleteAllSmsMessages(SerialAudioPhone phone)
        {
            try
            {
                // Confirm deletion with the user
                Console.Write("Are you sure you want to delete all SMS messages? (y/n): ");
                string? confirmation = Console.ReadLine();
                if (confirmation?.ToLower() == "y")
                {
                    phone.DeleteAllSms();
                }
                else
                {
                    Console.WriteLine("Deletion canceled.");
                }
            }
            catch (Exception ex)
            {
                // Handle errors that may occur when deleting all SMS messages
                Console.WriteLine($"Error deleting all SMS messages: {ex.Message}");
            }
        }
    }
}
