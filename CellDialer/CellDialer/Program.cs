using CellDialer;
using System;

namespace CellDialer
{
    class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("Choose an option:");
                Console.WriteLine("1. List Audio Devices");
                Console.WriteLine("2. Test Audio Streaming (Loopback)");
                Console.WriteLine("3. Test Serial Audio Phone Call");
                Console.WriteLine("4. Exit");
                Console.Write("Enter your choice: ");
                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        TestAudioDeviceManager();
                        break;
                    case "2":
                        TestAudioStreamer();
                        break;
                    case "3":
                        TestSerialAudioPhone();
                        break;
                    case "4":
                        Console.WriteLine("Exiting...");
                        return;
                    default:
                        Console.WriteLine("Invalid choice. Please try again.");
                        break;
                }
            }
        }

        static void TestAudioDeviceManager()
        {
            var manager = new AudioDeviceManager();
            manager.ListDevices();
        }

        static void TestAudioStreamer()
        {
            Console.Write("Enter input device index: ");
            int inputDeviceIndex = int.Parse(Console.ReadLine());

            Console.Write("Enter output device index: ");
            int outputDeviceIndex = int.Parse(Console.ReadLine());

            var streamer = new AudioStreamer(inputDeviceIndex, outputDeviceIndex);
            streamer.Start();

            Console.WriteLine("Audio streaming started. Press any key to stop...");
            Console.ReadKey();

            streamer.Stop();
            Console.WriteLine("Audio streaming stopped.");
        }

        static void TestSerialAudioPhone()
        {
            Console.Write("Enter phone number to dial: ");
            string phoneNumber = Console.ReadLine();

            var phone = new SerialAudioPhone();
            phone.StartCall(phoneNumber);

            Console.WriteLine("Call started. Press 'Esc' to end the call.");
        }
    }

}
