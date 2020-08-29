
using arookas.IO.Binary;
using System;
using System.IO;

namespace arookas.WAVE {

	[Errand(Errand.WAVE)]
	class WAVEPerformer : IErrand {

		string mInput, mOutput;
		IOFormat mInputFormat, mOutputFormat;
		WAVEMixerMode mMixerMode;
		Mode mMode;

		// raw
		WAVEFormat mRawInputFormat, mRawOutputFormat;
		int mRawSampleRate;

		// streams
		bool mStreamLoop;
		int mStreamLoopStart;
		int mStreamFrameRate;
		StreamFormat mStreamFormat;

		public void LoadParams(string[] arguments) {
			var cmdline = new aCommandLine(arguments);
			aCommandLineParameter parameter;

			// input
			parameter = SMSAudioClass.GetLastCmdParam(cmdline, "-input");

			if (parameter == null) {
				SMSAudioClass.WriteError("WAVE: missing -input parameter.");
			} else if (parameter.Count == 0) {
				SMSAudioClass.WriteError("WAVE: missing argument for -input parameter.");
			}

			mInput = parameter[0];
			mInputFormat = GetFormat(Path.GetExtension(mInput));

			if (mInputFormat == IOFormat.Raw) {
				if (parameter.Count < 2) {
					SMSAudioClass.WriteError("WAVE: missing format for raw input.");
				}

				if (!Enum.TryParse(parameter[1], true, out mRawInputFormat)) {
					SMSAudioClass.WriteError("WAVE: bad format '{0}' for raw input.", parameter[1]);
				}
			}

			// output
			parameter = SMSAudioClass.GetLastCmdParam(cmdline, "-output");

			if (parameter == null) {
				SMSAudioClass.WriteError("WAVE: missing -output parameter.");
			} else if (parameter.Count == 0) {
				SMSAudioClass.WriteError("WAVE: missing argument for -output parameter.");
			}

			mOutput = parameter[0];
			mOutputFormat = GetFormat(Path.GetExtension(mOutput));

			if (mOutputFormat == IOFormat.Raw) {
				if (parameter.Count < 2) {
					SMSAudioClass.WriteError("WAVE: missing format for raw output.");
				}

				if (!Enum.TryParse(parameter[1], true, out mRawOutputFormat)) {
					SMSAudioClass.WriteError("WAVE: bad format '{0}' for raw output.", parameter[1]);
				}
			} else if (mOutputFormat == IOFormat.AfcStream) {
				if (parameter.Count < 2) {
					mStreamFormat = StreamFormat.Adpcm;
				} else if (!Enum.TryParse(parameter[1], true, out mStreamFormat)) {
					SMSAudioClass.WriteError("WAVE: bad stream format '{0}'.", parameter[1]);
				}
			}

			// mode
			if (mInputFormat == IOFormat.Raw && mOutputFormat == IOFormat.Raw) {
				mMode = Mode.RawToRaw;
			} else if (mInputFormat == IOFormat.Raw && mOutputFormat == IOFormat.MicrosoftWAVE) {
				mMode = Mode.RawToWav;
			} else if (mInputFormat == IOFormat.MicrosoftWAVE && mOutputFormat == IOFormat.Raw) {
				mMode = Mode.WavToRaw;
			} else if (mInputFormat == IOFormat.MicrosoftWAVE && mOutputFormat == IOFormat.AfcStream) {
				mMode = Mode.WavToStream;
			} else if (mInputFormat == IOFormat.AfcStream && mOutputFormat == IOFormat.MicrosoftWAVE) {
				mMode = Mode.StreamToWav;
			} else {
				SMSAudioClass.WriteError("WAVE: unsupported combination of input and output formats.");
			}

			// mix mode
			parameter = SMSAudioClass.GetLastCmdParam(cmdline, "-mix-mode");

			if (parameter != null) {
				if (parameter.Count < 1) {
					SMSAudioClass.WriteError("WAVE: bad -mix-mode parameter.");
				}

				if (!Enum.TryParse(parameter[0], true, out mMixerMode)) {
					SMSAudioClass.WriteError("WAVE: bad mixer mode '{0}' in -mix-mode parameter.", parameter[0]);
				}
			}

			// sample rate
			parameter = SMSAudioClass.GetLastCmdParam(cmdline, "-sample-rate");

			if (parameter != null) {
				if (parameter.Count < 1) {
					SMSAudioClass.WriteError("WAVE: missing argument for -sample-rate parameter.");
				}
				
				if (!Int32.TryParse(parameter[0], out mRawSampleRate) || mRawSampleRate < 0) {
					SMSAudioClass.WriteError("WAVE: bad sample rate '{0}'.", parameter[0]);
				}
			} else if (mInputFormat == IOFormat.Raw && mOutputFormat != IOFormat.Raw) {
				SMSAudioClass.WriteError("WAVE: missing -sample-rate parameter for raw input.");
			}

			// frame rate
			parameter = SMSAudioClass.GetLastCmdParam(cmdline, "-frame-rate");

			if (parameter != null) {
				if (parameter.Count < 1) {
					SMSAudioClass.WriteError("WAVE: missing argument for -frame-rate parameter.");
				}
				
				if (!Int32.TryParse(parameter[0], out mStreamFrameRate) || mStreamFrameRate < 0) {
					SMSAudioClass.WriteError("WAVE: bad frame rate '{0}'.", parameter[0]);
				}
			} else {
				mStreamFrameRate = 30;
			}

			// loop
			parameter = SMSAudioClass.GetLastCmdParam(cmdline, "-loop");

			if (parameter != null) {
				mStreamLoop = true;

				if (parameter.Count < 1) {
					SMSAudioClass.WriteError("WAVE: missing argument for -loop parameter.");
				}

				if (!Int32.TryParse(parameter[0], out mStreamLoopStart) || mStreamLoopStart < 0) {
					SMSAudioClass.WriteError("WAVE: bad loop value '{0}'.", parameter[0]);
				}
			}
		}

		public void ShowUsage() {
			SMSAudioClass.WriteMessage("USAGE: WAVE -input <file> [<fmt>] -output <file> [<fmt>] [...]\n");
			SMSAudioClass.WriteMessage("\n");
			SMSAudioClass.WriteMessage("FORMATS:\n");
			SMSAudioClass.WriteMessage("  pcm8     linear, 8-bit PCM, signed\n");
			SMSAudioClass.WriteMessage("  pcm16    linear, 16-bit PCM, signed\n");
			SMSAudioClass.WriteMessage("  adpcm2   non-linear, 2-bit ADPCM\n");
			SMSAudioClass.WriteMessage("  adpcm4   non-linear, 4-bit ADPCM\n");
			SMSAudioClass.WriteMessage("\n");
			SMSAudioClass.WriteMessage("OPTIONS:\n");
			SMSAudioClass.WriteMessage("  -sample-rate <rate>\n");
			SMSAudioClass.WriteMessage("    Specifies the sample rate (Hz) of the raw audio data.\n");
			SMSAudioClass.WriteMessage("    Used only when converting RAW > WAV.\n");
			SMSAudioClass.WriteMessage("  -frame-rate <rate>\n");
			SMSAudioClass.WriteMessage("    Specifies the frame rate of the stream; if omitted,\n");
			SMSAudioClass.WriteMessage("    defaults to 30. Ignored if not creating a stream.\n");
			SMSAudioClass.WriteMessage("  -loop <sample>\n");
			SMSAudioClass.WriteMessage("    Specifes the loop point for the stream; if omitted, the\n");
			SMSAudioClass.WriteMessage("    stream will not loop. Ignored if not creating a stream.\n");
			SMSAudioClass.WriteMessage("  -mix-mode <mode>\n");
			SMSAudioClass.WriteMessage("    Specifies how to mix stereo LPCM sounds to mono. <mode>\n");
			SMSAudioClass.WriteMessage("    may be MIX, LEFT, or RIGHT; if omitted, defaults to MIX.\n");
		}

		public void Perform() {
			using (var instream = SMSAudioClass.OpenFile(mInput)) {
				using (var outstream = SMSAudioClass.CreateFile(mOutput)) {
					SMSAudioClass.WriteMessage("Encoding '{0}' to '{1}'...\n", Path.GetFileName(mInput), Path.GetFileName(mOutput));

					switch (mMode) {
						case Mode.RawToRaw: PerformRawToRaw(instream, outstream); break;
						case Mode.RawToWav: PerformRawToWav(instream, outstream); break;
						case Mode.WavToRaw: PerformWavToRaw(instream, outstream); break;
						case Mode.WavToStream: PerformWavToStream(instream, outstream); break;
						case Mode.StreamToWav: PerformStreamToWav(instream, outstream); break;
						default: SMSAudioClass.WriteError("WAVE: unimplemented mode '{0}'.", mMode); break;
					}
				}
			}
		}

		void PerformRawToRaw(Stream instream, Stream outstream) {
			var mixer = new RawWAVEMixer(instream, mRawInputFormat);
			var writer = new aBinaryWriter(outstream, Endianness.Big);

			mixer.Write(mRawOutputFormat, writer);
		}

		void PerformRawToWav(Stream instream, Stream outstream) {
			var mixer = new RawWAVEMixer(instream, mRawInputFormat);
			var writer = new aBinaryWriter(outstream, Endianness.Little);
			var dataSize = (mixer.SampleCount * 2);
			
			writer.WriteString("RIFF");
			writer.WriteS32(36 + dataSize);
			writer.WriteString("WAVE");
			writer.WriteString("fmt ");
			writer.WriteS32(16);
			writer.WriteS16(1); // format
			writer.Write16(1); // channel count
			writer.WriteS32(mRawSampleRate);
			writer.WriteS32(mRawSampleRate * 2); // byte rate
			writer.Write16(2); // block align
			writer.Write16(16); // bit depth
			writer.WriteString("data");
			writer.WriteS32(dataSize);
			mixer.Write(WAVEFormat.Pcm16, writer);
		}

		void PerformWavToRaw(Stream instream, Stream outstream) {
			var mixer = new MicrosoftWAVEMixer(instream);
			var writer = new aBinaryWriter(outstream, Endianness.Big);

			mixer.MixerMode = mMixerMode;
			mixer.Write(mRawOutputFormat, writer);
		}

		void PerformStreamToWav(Stream instream, Stream outstream) {
			var reader = new aBinaryReader(instream, Endianness.Big);
			var writer = new aBinaryWriter(outstream, Endianness.Little);

			var streamDataSize = reader.ReadS32();
			var sampleCount = reader.ReadS32();
			var sampleRate = reader.Read16();
			var dataSize = (sampleCount * 4);
			var format = (StreamFormat)reader.Read16();

			writer.WriteString("RIFF");
			writer.WriteS32(36 + dataSize);
			writer.WriteString("WAVE");
			writer.WriteString("fmt ");
			writer.WriteS32(16);
			writer.WriteS16(1); // format
			writer.Write16(2); // channel count
			writer.WriteS32(sampleRate);
			writer.WriteS32(sampleRate * 4); // byte rate
			writer.Write16(4); // block align
			writer.Write16(16); // bit depth
			writer.WriteString("data");
			writer.WriteS32(dataSize);

			reader.Goto(32);

			switch (format) {
				case StreamFormat.Pcm: DecodeStreamPcm(reader, writer, sampleCount); break;
				case StreamFormat.Adpcm: DecodeStreamAdpcm(reader, writer, sampleCount); break;
				default: SMSAudioClass.WriteError("AFC: Unknown format '{0}' in header.", (int)format); break;
			}
		}

		void PerformWavToStream(Stream instream, Stream outstream) {
			var mixer = new MicrosoftWAVEMixer(instream);
			var writer = new aBinaryWriter(outstream, Endianness.Big);
			var dataSize = 0;

			switch (mStreamFormat) {
				case StreamFormat.Pcm: dataSize = (mixer.SampleCount * 4); break;
				case StreamFormat.Adpcm: dataSize = (SMSAudioClass.RoundUp16B(mixer.SampleCount) / 16 * 18); break;
			}

			writer.WriteS32(dataSize);
			writer.WriteS32(mixer.SampleCount);
			writer.Write16((ushort)mixer.SampleRate);
			writer.Write16((ushort)mStreamFormat);
			writer.Write16(0); // unused
			writer.Write16((ushort)mStreamFrameRate);
			writer.WriteS32(mStreamLoop ? 1 : 0); // loop flag
			writer.WriteS32(mStreamLoop ? mStreamLoopStart : 0); // loop start
			writer.WritePadding(32, 0);

			switch (mStreamFormat) {
				case StreamFormat.Pcm: EncodeStreamPcm(mixer, writer); break;
				case StreamFormat.Adpcm: EncodeStreamAdpcm(mixer, writer); break;
			}

			writer.WritePadding(32, 0);
		}

		static IOFormat GetFormat(string extension) {
			switch (extension.ToLowerInvariant()) {
				case ".raw": return IOFormat.Raw;
				case ".wav": return IOFormat.MicrosoftWAVE;
				case ".afc": return IOFormat.AfcStream;
			}

			return IOFormat.Unknown;
		}

		const int cMessageInterval = 8000;

		static void DecodeStreamPcm(aBinaryReader reader, aBinaryWriter writer, int sampleCount) {
			for (var i = 0; i < sampleCount; ++i) {
				writer.WriteS16(reader.ReadS16());
				writer.WriteS16(reader.ReadS16());

				if ((i % cMessageInterval) == 0 || i >= sampleCount) {
					SMSAudioClass.WriteMessage("\rSamples decoded: {0}/{1}", System.Math.Min((i + 1), sampleCount), sampleCount);
				}
			}
		}

		static void DecodeStreamAdpcm(aBinaryReader reader, aBinaryWriter writer, int sampleCount) {
			var left = new short[16];
			int left_last = 0, left_penult = 0;

			var right = new short[16];
			int right_last = 0, right_penult = 0;

			for (var i = 0; i < sampleCount; i += 16) {
				WAVEform.Adpcm4toPcm16(reader.Read8s(9), left, ref left_last, ref left_penult);
				WAVEform.Adpcm4toPcm16(reader.Read8s(9), right, ref right_last, ref right_penult);

				for (var j = 0; j < 16 && (i + j) < sampleCount; ++j) {
					writer.WriteS16(left[j]);
					writer.WriteS16(right[j]);
				}

				if ((i % cMessageInterval) == 0 || (i + 16) >= sampleCount) {
					SMSAudioClass.WriteMessage("\rSamples encoded: {0}/{1}", System.Math.Min((i + 16), sampleCount), sampleCount);
				}
			}
		}

		static void EncodeStreamPcm(MicrosoftWAVEMixer mixer, aBinaryWriter writer) {
			short left, right;

			for (var i = 0; i < mixer.SampleCount; ++i) {
				mixer.ReadPcm16(i, out left, out right);
				writer.WriteS16(left);
				writer.WriteS16(right);

				if ((i % cMessageInterval) == 0 || i >= mixer.SampleCount) {
					SMSAudioClass.WriteMessage("\rSamples encoded: {0}/{1}", System.Math.Min((i + 1), mixer.SampleCount), mixer.SampleCount);
				}
			}
		}

		static void EncodeStreamAdpcm(MicrosoftWAVEMixer mixer, aBinaryWriter writer) {
			var left_adpcm4 = new byte[9];
			int left_last = 0, left_penult = 0;

			var right_adpcm4 = new byte[9];
			int right_last = 0, right_penult = 0;

			var left = new short[16];
			var right = new short[16];

			for (var i = 0; i < mixer.SampleCount; i += 16) {
				for (var j = 0; j < 16; ++j) {
					if ((i + j) < mixer.SampleCount) {
						mixer.ReadPcm16((i + j), out left[j], out right[j]);
					} else {
						left[j] = 0;
						right[j] = 0;
					}
				}

				WAVEform.Pcm16toAdpcm4(left, left_adpcm4, ref left_last, ref left_penult);
				WAVEform.Pcm16toAdpcm4(right, right_adpcm4, ref right_last, ref right_penult);

				writer.Write8s(left_adpcm4);
				writer.Write8s(right_adpcm4);

				if ((i % cMessageInterval) == 0 || (i + 16) >= mixer.SampleCount) {
					SMSAudioClass.WriteMessage("\rSamples encoded: {0}/{1}", System.Math.Min((i + 16), mixer.SampleCount), mixer.SampleCount);
				}
			}
		}

		enum IOFormat {

			Unknown = -1,
			Raw,
			MicrosoftWAVE,
			AfcStream,

		}

		enum Mode {

			RawToRaw,
			RawToWav,
			WavToRaw,
			WavToStream,
			StreamToWav,

		}

		enum StreamFormat {

			Pcm = 2,
			Adpcm = 4,

		}

	}

}
