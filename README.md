# SIM7600G-H Modem Tool

This project contains C# classes designed to manage audio streaming and serial communication with the SIMCOM SIM7600G-H modem. The modem is typically used in embedded systems or IoT devices for telecommunication and remote access.

## Overview

The code in this project is split into three classes. Two are meant for testing, and the third contains the working code for this project:

1. **`AudioDeviceManager`**: Lists available audio devices and their input/output channel capabilities.
2. **`AudioStreamer`**: Captures audio input and plays it back through an output device in real-time (loopback).
3. **`SerialAudioPhone`**: Manages serial communication with the SIMCOM SIM7600G-H modem, handling AT commands and audio streaming for phone calls. It now also includes the ability to send DTMF tones during a call and platform-specific support for both Windows and Linux.

## Key Features

- **Audio Device Management**: Easily list and select audio devices for further processing.
- **Real-Time Audio Loopback**: Test audio input and output devices using real-time streaming with adjustable sample rate and channels.
- **SIMCOM SIM7600G-H Modem Integration**: Send AT commands over the AT Command Port and transmit/receive audio data over the serial audio port. The program can send DTMF tones during an active call, including the standard tones (0-9, *, #) and extended tones (A-D). The class also has methods to send, read, and delete SMS messages.
- **Platform-Specific Support**: The code now dynamically handles platform differences:
  - **Windows**: Uses NAudio for audio capture and playback.
  - **Linux**: Uses Alsa.Net for audio handling, allowing the code to work seamlessly on Raspberry Pi and other Linux systems.

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

- **NAudio Library (for Windows)**: This project uses the NAudio library for handling audio device management and streaming in C#. You can install the necessary packages via NuGet:
  ```bash
  dotnet add package NAudio.WinMM 
  dotnet add package NAudio.Wasapi
  ```
  NOTE: The NAudio.WinMM package will automatically install NAudio.Core as a prerequisite. You do not need to install it separately.

- **Alsa.Net Library (for Linux)**: The project uses Alsa.Net for audio handling on Linux systems. Install the package via NuGet:
  ```bash
  dotnet add package Alsa.Net
  ```
  NOTE: You will need to install the libasound2-dev package on the target linux system you are going to use.

- **System.IO.Ports Library**: You'll also need to import the System.IO.Ports library to avoid an invalid reference error to the .NET Framework version of this library with the same name.
  ```bash
  dotnet add package System.IO.Ports
  ```
- **System.Management Library (for Windows)**: You'll also need to import the System.Management library so the program can search for the COM ports using the device ID of the modem.
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

#### Key Features:
- **Platform-Specific Audio Handling**: Automatically handles audio differently for Windows and Linux:
  - On Windows: Uses NAudio for audio input/output.
  - On Linux: Uses Alsa.Net for audio input/output.
- **Improved DTMF Tone Support**: The program now properly detects and sends all DTMF tones (including `*` and `#`) regardless of whether they are entered using Shift + 8/3 or the number pad.
- **SMS Management**: Send, read, and delete SMS messages using the modem’s serial port.
- **Automatic Port Detection**: The program automatically finds the correct serial ports for AT commands and audio, whether running on Windows or Linux.

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
   dotnet add package NAudio.WinMM
   dotnet add package NAudio.Wasapi
   dotnet add package Alsa.Net
   dotnet add package System.IO.Ports
   dotnet add package System.Management
   ```
3. Build and run the project (You'll need to be in the directory with the csproj file):
   ```bash
   dotnet build 'SIM7600G-H Modem Tool.csproj' --os <windows/linux>
   dotnet run
   ```

4. Follow the console prompts to test the different functionalities.

## Compatibility

- The project is compatible with the SIMCOM SIM7600G-H modem and runs on both Windows and Linux systems with .NET Core (or later versions) installed.

## Contributing

If you have any suggestions or improvements, feel free to open an issue or submit a pull request.

## License

This project is licensed under the MIT License.
