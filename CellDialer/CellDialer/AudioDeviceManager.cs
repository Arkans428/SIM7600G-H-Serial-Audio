using NAudio.CoreAudioApi;

namespace CellDialer
{
    /// <summary>
    /// This code uses the NAudio library for handling audio devices in C#. 
    /// The ListDevices method enumerates all active audio devices and prints their index, input channels, output channels, and friendly name.
    /// </summary>
    public class AudioDeviceManager
    {
        // This method lists all active audio devices along with their input and output channel capabilities.
        public void ListDevices()
        {
            var enumerator = new MMDeviceEnumerator(); // Enumerator to get audio devices
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active); // Get all active devices

            Console.WriteLine("Index\tInput Channels\tOutput Channels\tName");
            int index = 0;

            // Loop through each device to display its properties
            foreach (var device in devices)
            {
                int inputChannels = device.DataFlow == DataFlow.Capture ? device.AudioEndpointVolume.Channels.Count : 0; // Get input channels if it's a capture device
                int outputChannels = device.DataFlow == DataFlow.Render ? device.AudioEndpointVolume.Channels.Count : 0; // Get output channels if it's a render device
                Console.WriteLine($"{index}\t{inputChannels}\t\t{outputChannels}\t\t{device.FriendlyName}");
                index++;
            }
        }
    }
}

// Usage example:
// var manager = new AudioDeviceManager();
// manager.ListDevices();

