
## SMSAudioTool(Flaaffy v.0.6.1)

### Summary

_SMSAudioTool(Flaaffy v.0.6.1)_ is a simple audio toolchain for Super Mario Sunshine.
At its core, it is a runtime library for loading, utilizing, and playing the various audio-related formats of the game.
There are also a series of tools and utilities to convert and create these formats.

### Compiling

To compile _SMSAudioTool(Flaaffy v.0.6.1)_, you'll need to have the following libraries compiled and/or installed:

- [arookas library](http://github.com/arookas/arookas)

The repository contains a [premake5](https://premake.github.io/) [script](premake5.lua).
Simply run the script with premake5 and build the resulting solution.

> **Note:** You might need to fill in any unresolved-reference errors by supplying your IDE with the paths to the dependencies listed above.

## Usage

As of now, _SMSAudioTool(Flaaffy v.0.6.1)_ toolkit contains a swiss-army-knife utility program called _SMSAudioTool(Flaaffy v.0.6.1)_.

### SMSAudioTool(Flaaffy v.0.6.1)

_SMSAudioTool(Flaaffy v.0.6.1)_ is utility program able to convert many audio-related formats.
It is a command-line interface, where each feature is implemented as an "errand".
The arguments follow this format:

```
SMSAudioTool [-help] -errand <errand> [...]
```

You may specify the `-help` parameter to show brief documentation for a given errand.
The available errands are as follows:

|Errand|Description|
|-------|-----------|
|[IBNK](IBNK.md)|Converts banks ("IBNK" or "bnk") to&#8209;and&#8209;fro XML and binary formats. Little&#8209;endian and big&#8209;endian are supported.|
|[WSYS](WSYS.md)|Converts WAVE banks ("WSYS" or "ws") to&#8209;and&#8209;fro XML and binary formats. Little&#8209;endian and big&#8209;endian are supported. Automatically extracts and repacks WAVE archives (.aw files). Includes PCM&nbsp;⇄&nbsp;ADPCM conversion.|
|[WAVE](WAVE.md)|Standalone errand to convert audio data between formats. Various raw and standard formats are supported.|
|[BMS](BMS.md)|A dedicated BMS assembler. Able to compile BMS files from no&#8209;holds&#8209;barred assembly text. Features relocation, named labels, variables, embedded POD, and various other directives.|
|[MIDI](MIDI.md)|Converts basic MIDI files to the BMS assembler language. Used to create custom music.|
|[DataSEQ](DataSEQ.md)|Basic utility to extract and replace data (sequences, banks, and WAVE banks) inside an AAF file.|
