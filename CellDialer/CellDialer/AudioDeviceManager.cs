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

using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;

namespace ModemTool
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
