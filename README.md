
## flaaffy v.0.3

### Summary

_flaaffy_ is a simple audio toolchain for Super Mario Sunshine.
At its core, it is a runtime library for loading, utilizing, and playing the various audio-related formats of the game. There are also a series of tools and utilities to convert and create these formats.

### Compiling

To compile _flaaffy_, you'll need to have the following libraries compiled and/or installed:

- [arookas library](http://github.com/arookas/arookas)

The repository contains a [premake5](https://premake.github.io/) [script](premake5.lua).
Simply run the script with premake5 and build the resulting solution.

> **Note:** You might need to fill in any unresolved-reference errors by supplying your IDE with the paths to the dependencies listed above.

## Usage

As of now, _flaaffy_ toolkit contains a swiss-army-knife utility program called _mareep_.

### mareep

_mareep_ is utility program able to convert many audio-related formats.
It is a command-line interface, where each feature is implemented as an "action".
The arguments follow this format:

```
mareep -action <name> [<arguments>]
```

The available actions are as follows:

|Action|Description|
|------|-----------|
|[shock](shock.md)|Converts instrument banks ("IBNK" or "bnk") to&#8209;and&#8209;fro XML and binary formats. Little endian and big endian are supported.|
|[whap](whap.md)|Converts wave banks ("WSYS" or "ws") to&#8209;and&#8209;fro XML and binary formats. Little endian and big endian are supported. Automatically extracts and repacks the wave archives (.aw files). Includes PCM&nbsp;⇄&nbsp;ADPCM conversion.|
|[wave](wave.md)|Standalone action to convert raw audio data PCM&nbsp;⇄&nbsp;ADPCM. Any combination of input and output formats is supported.|
|[cotton](cotton.md)|A dedicated BMS assembler. Able to compile BMS files from no&#8209;holds&#8209;barred assembly text. Features relocation, named labels, variables, embedded POD, and various other directives.|
|[jolt](jolt.md)|Converts basic MIDI files to the cotton assembler language. Used to create custom music.|
