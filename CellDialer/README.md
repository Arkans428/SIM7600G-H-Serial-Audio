
# Audio and Serial Communication with SIMCOM SIM7600G-H Modem

This project contains C# classes designed to manage audio streaming and serial communication with the SIMCOM SIM7600G-H modem. The modem is typically used in embedded systems or IoT devices for telecommunication and audio data processing.

## Overview

The code in this project is split into three main classes, each handling a different aspect of audio processing and serial communication:

1. **`AudioDeviceManager`**: Lists available audio devices and their input/output channel capabilities.
2. **`AudioStreamer`**: Captures audio input and plays it back through an output device in real-time (loopback).
3. **`SerialAudioPhone`**: Manages serial communication with the SIMCOM SIM7600G-H modem, handling AT commands and audio streaming for phone calls.

## Key Features

- **Audio Device Management**: Easily list and select audio devices for further processing.
- **Real-Time Audio Loopback**: Test audio input and output devices using real-time streaming with adjustable sample rate and channels.
- **SIMCOM SIM7600G-H Modem Integration**: Send AT commands and transmit/receive audio data over serial communication with support for automated call handling.

## Hardware Requirements

This project is designed for use with the **SIMCOM SIM7600G-H** modem. It assumes you have two serial interfaces configured for the modem:
- One serial interface for AT command communication.
- One serial interface for audio data transmission.

## Software Requirements

- **NAudio Library**: This project uses the NAudio library for handling audio device management and streaming in C#. You can install it via NuGet:
  ```bash
  dotnet add package NAudio
  ```
- **System.IO.Ports Library**: You'll also need to import the System.IO.Ports library to avoid an invalid reference error to the .NET Framework version of this library with the same name.
  ```bash
  dotnet add package System.IO.Ports
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

The `SerialAudioPhone` class is designed specifically for working with the SIMCOM SIM7600G-H modem. It handles sending AT commands, initiating calls, and streaming audio through the modem’s audio serial port. The call continues until the user presses the "Esc" key or the call ends due to a "NO CARRIER" response from the modem.

#### Usage Example:
```csharp
var phone = new SerialAudioPhone("COM3", "COM4");
phone.StartCall("10086");
```

### Important Notes:
- The critical AT command `AT+CPCMREG=1` is sent right after dialing the phone number to ensure that audio transmission is enabled on the modem.
- You can end the call manually by pressing the "Esc" key.

## How to Run

1. Clone this repository:
   ```bash
   git clone https://github.com/yourusername/yourproject.git
   ```
2. Install dependencies:
   ```bash
   dotnet add package NAudio
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

This project is licensed under the GNU General Publice License V3.
