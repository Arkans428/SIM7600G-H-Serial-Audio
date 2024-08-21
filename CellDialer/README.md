# Audio and Serial Communication with SIMCOM SIM7600G-H Modem

This project contains C# classes designed to manage audio streaming and serial communication with the SIMCOM SIM7600G-H modem. The modem is typically used in embedded systems or IoT devices for telecommunication and remote access.

## Overview

The code in this project is split into three main classes. Two are meant for testing, and the third contains the working code for this project:

1. **`AudioDeviceManager`**: Lists available audio devices and their input/output channel capabilities.
2. **`AudioStreamer`**: Captures audio input and plays it back through an output device in real-time (loopback).
3. **`SerialAudioPhone`**: Manages serial communication with the SIMCOM SIM7600G-H modem, handling AT commands and audio streaming for phone calls. It now also includes the ability to send DTMF tones during a call.

## Key Features

- **Audio Device Management**: Easily list and select audio devices for further processing.
- **Real-Time Audio Loopback**: Test audio input and output devices using real-time streaming with adjustable sample rate and channels.
- **SIMCOM SIM7600G-H Modem Integration**: Send AT commands over the AT Command Port and transmit/receive audio data over the serial audio port. The program can now send DTMF tones during an active call, including the standard tones (0-9, *, #) and extended tones (A-D).

## Hardware Requirements

This project is designed for use with the **SIMCOM SIM7600G-H** modem. It assumes you have two serial interfaces configured for the modem:
- One serial interface for AT command communication.
- One serial interface for audio data transmission.

If you encounter errors stating it can't find the serial ports, you may need to change your USB PID configuration of the modem. Open the AT Command or modem port if available and enter:
```bash
AT+CUSBPIDSWITCH=9001,1,1
```
The modem will reboot automatically after receiving the command, and you should see both ports now.

## Software Requirements

- **NAudio Library**: This project uses the NAudio library for handling audio device management and streaming in C#. You can install it via NuGet:
  ```bash
  dotnet add package NAudio
  ```
- **System.IO.Ports Library**: You'll also need to import the System.IO.Ports library to avoid an invalid reference error to the .NET Framework version of this library with the same name.
  ```bash
  dotnet add package System.IO.Ports
  ```
- **System.Management Library**: You'll also need to import the System.Management library so the program can search for the COM ports using the device ID of the modem.
  ```bash
  dotnet add package System.Management
  ```

## Project Structure

The project consists of the following classes:

### `AudioDeviceManager`

This class allows you to list and inspect all active audio devices on your system. It displays the device index, input channels, output channels, and the device name.

#### Usage Example:
```csharp
var manager = new AudioDeviceManager();
manager.ListDevices();
```

### `AudioStreamer`

The `AudioStreamer` class captures audio input from a specified device and plays it back in real-time through another device. This is useful for testing audio loopback scenarios.

#### Usage Example:
```csharp
var streamer = new AudioStreamer(inputDeviceIndex: 1, outputDeviceIndex: 0);
streamer.Start();

// Press any key to stop the streaming
Console.ReadKey();
streamer.Stop();
```

### `SerialAudioPhone`

The `SerialAudioPhone` class is designed specifically for working with the SIMCOM SIM7600G-H modem. It handles sending AT commands, initiating calls, and streaming audio through the modem’s serial audio port. The call continues until the user presses the "Esc" key or the call ends due to a "NO CARRIER" response from the modem.

#### Usage Example:
```csharp
var phone = new SerialAudioPhone();
phone.StartCall("17805555555");
```

### Important Notes:
- The critical AT command `AT+CPCMREG=1` is sent right after dialing the phone number to ensure that audio transmission is enabled on the modem.
- You can end the call manually by pressing the "Esc" key.
- DTMF tones, including A, B, C, and D, can be sent during an active call by pressing the corresponding keys on the keyboard.

## How to Run

1. Clone this repository:
   ```bash
   git clone https://github.com/Arkans428/SIM7600G-H-Serial-Audio.git
   ```
2. Install dependencies:
   ```bash
   dotnet add package NAudio
   dotnet add package System.IO.Ports
   dotnet add package System.Management
   ```
3. Build and run the project:
   ```bash
   dotnet build
   dotnet run
   ```

4. Follow the console prompts to test the different functionalities.

## Compatibility

- The project is compatible with the SIMCOM SIM7600G-H modem and any Windows PC with .NET Core (or later versions) installed.

## Contributing

If you have any suggestions or improvements, feel free to open an issue or submit a pull request.

## License

This project is licensed under the MIT License.
