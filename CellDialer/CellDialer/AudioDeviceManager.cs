using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;

namespace CellDialer
{
    public class AudioDeviceManager
    {
        // Method to list all active audio devices, including input and output channels
        public void ListDevices()
        {
            try
            {
                // Create an enumerator to access the audio devices
                var enumerator = new MMDeviceEnumerator();

                // Get a list of all active audio endpoints (input and output devices)
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active);

                // Display a header for the device information
                Console.WriteLine("Index\tInput Channels\tOutput Channels\tName");
                int index = 0;

                // Iterate over each device and display its details
                foreach (var device in devices)
                {
                    // Determine the number of input channels if the device is an input (capture) device
                    int inputChannels = device.DataFlow == DataFlow.Capture ? device.AudioEndpointVolume.Channels.Count : 0;

                    // Determine the number of output channels if the device is an output (render) device
                    int outputChannels = device.DataFlow == DataFlow.Render ? device.AudioEndpointVolume.Channels.Count : 0;

                    // Display the device information in a formatted manner
                    Console.WriteLine($"{index}\t{inputChannels}\t\t{outputChannels}\t\t{device.FriendlyName}");
                    index++;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // Handle access-related errors, such as permissions issues
                Console.WriteLine("Access error while listing devices: " + ex.Message);
            }
            catch (COMException ex)
            {
                // Handle errors related to COM interactions, which may occur when accessing audio devices
                Console.WriteLine("COM error while accessing audio devices: " + ex.Message);
            }
            catch (Exception ex)
            {
                // Handle any other unexpected errors
                Console.WriteLine("An unexpected error occurred while listing audio devices: " + ex.Message);
            }
        }
    }
}

// Usage example:
// var manager = new AudioDeviceManager();
// manager.ListDevices();
