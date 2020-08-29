
using arookas.IO.Binary;
using arookas.Xml;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System;

namespace arookas.IBNK {

	[Errand(Errand.IBNK)]
	class IBNKErrand : SimpleConverterErrand {

		public override void ShowUsage() {
			SMSAudioClass.WriteMessage("USAGE: IBNK -input <file> <fmt> -output <file> <fmt>\n");
			SMSAudioClass.WriteMessage("\n");
			SMSAudioClass.WriteMessage("FORMATS:\n");
			SMSAudioClass.WriteMessage("  xml  text-based XML format\n");
			SMSAudioClass.WriteMessage("  be   big-endian binary format\n");
			SMSAudioClass.WriteMessage("  le   little-endian binary format\n");
		}

		public override void Perform() {
			Transformer<InstrumentIBNK> chain = null;
			
			string inputName = Path.GetFileName(mInputFile);
			
			SMSAudioClass.WriteMessage("Opening input file '{0}'...\n", inputName);

			using (var instream = SMSAudioClass.OpenFile(mInputFile)) {
				SMSAudioClass.WriteMessage("Creating output file '{0}'...\n", Path.GetFileName(mOutputFile));

				using (var outstream = SMSAudioClass.CreateFile(mOutputFile)) {
					switch (mInputFormat) {
						case IOFormat.Xml: chain = new XmlIBNKDeserializer(CreateXmlInput(instream).Root, inputName); break;
						case IOFormat.LE: chain = new BinaryIBNKDeserializer(CreateLittleBinaryInput(instream), inputName); break;
						case IOFormat.BE: chain = new BinaryIBNKDeserializer(CreateBigBinaryInput(instream), inputName); break;
						default: SMSAudioClass.WriteError("IBNK: unimplemented input format '{0}'.", mInputFormat); break;
					}

					switch (mOutputFormat) {
						case IOFormat.Xml: chain.AppendLink(new XmlIBNKSerializer(CreateXmlOutput(outstream))); break;
						case IOFormat.LE: chain.AppendLink(new BinaryIBNKSerializer(CreateLittleBinaryOutput(outstream))); break;
						case IOFormat.BE: chain.AppendLink(new BinaryIBNKSerializer(CreateBigBinaryOutput(outstream))); break;
						case IOFormat.SoundFont: chain.AppendLink(new SoundFontIBNKSerializer(CreateLittleBinaryOutput(outstream))); break;
						default: SMSAudioClass.WriteError("IBNK: unimplemented output format '{0}'.", mOutputFormat); break;
					}

					chain.Transform(null);
				}
			}
		}

	}

}

namespace arookas {

	abstract class BinaryIBNKTransformer : Transformer<InstrumentIBNK> {

		protected const uint IBNK = 0x49424E4Bu;
		protected const uint IBNK = 0x42414E4Bu;
		protected const uint INST = 0x494E5354u;
		protected const uint PERC = 0x50455243u;
		protected const uint PER2 = 0x50455232u;

		protected const int cIBNKCount = 240;
		protected const int cOscillatorCount = 2;
		protected const int cRandomEffectCount = 2;
		protected const int cSenseEffectCount = 2;

		protected bool CheckIBNK(InstrumentIBNK IBNK) {
			var valid = true;

			for (var i = 0; i < cIBNKCount; ++i) {
				if (IBNK[i] == null) {
					continue;
				}

				var type = IBNK[i].Type;

				if (type == InstrumentType.Melodic) {
					var instrument = (IBNK[i] as MelodicInstrument);

					if (instrument == null) {
						SMSAudioClass.WriteWarning("IBNK: #{0} bad instrument type\n", i);
						valid = false;
						continue;
					}

					if (instrument.OscillatorCount > 2) {
						SMSAudioClass.WriteWarning("IBNK: #{0} instrument has more than two oscillators\n", i);
						valid = false;
					}

					if (instrument.Effects.Count(effect => effect is RandomInstrumentEffect) > 2) {
						SMSAudioClass.WriteWarning("IBNK: #{0} instrument has more than two random effects\n", i);
						valid = false;
					}

					if (instrument.Effects.Count(effect => effect is SenseInstrumentEffect) > 2) {
						SMSAudioClass.WriteWarning("IBNK: #{0} instrument has more than two sense effects\n", i);
						valid = false;
					}
				} else if (type == InstrumentType.DrumSet) {
					var drumset = (IBNK[i] as DrumSet);

					if (drumset == null) {
						SMSAudioClass.WriteWarning("IBNK: #{0} bad instrument type\n", i);
						valid = false;
						continue;
					}

					for (var key = 0; key < drumset.Capacity; ++key) {
						var percussion = drumset[key];

						if (percussion == null) {
							continue;
						}

						if (percussion.Effects.Count(effect => effect is RandomInstrumentEffect) > 2) {
							SMSAudioClass.WriteWarning("IBNK: #{0} {1} percussion has more than two random effects\n", i, SMSAudioClass.ConvertKey(key));
							valid = false;
						}

						if (percussion.Effects.Count(effect => effect is SenseInstrumentEffect) > 0) {
							SMSAudioClass.WriteWarning("IBNK: #{0} {1} percussion has sense effects\n", i, SMSAudioClass.ConvertKey(key));
							valid = false;
						}
					}
				} else {
					SMSAudioClass.WriteWarning("IBNK: #{0} bad instrument type\n", i);
					valid = false;
				}
			}

			return valid;
		}

	}

	class BinaryIBNKDeserializer : BinaryIBNKTransformer {

		aBinaryReader mReader;
		string mName;

		public BinaryIBNKDeserializer(aBinaryReader reader, string name) {
			mReader = reader;
			mName = name;
		}

		protected override InstrumentIBNK DoTransform(InstrumentIBNK obj) {
			if (obj != null) {
				return obj;
			}

			mReader.Keep();
			mReader.PushAnchor();

			if (mReader.Read32() != IBNK) {
				SMSAudioClass.WriteError("IBNK: could not find header.");
			}

			var size = mReader.ReadS32();
			var virtualNumber = mReader.ReadS32();

			SMSAudioClass.WriteMessage("IBNK: header found, size {0:F1} KB, virtual number {1}\n", ((double)size / 1024.0d), virtualNumber);

			var IBNK = new InstrumentIBNK(virtualNumber, 256);
			
			IBNK.Name = mName;

			mReader.Goto(32);

			if (mReader.Read32() != IBNK) {
				SMSAudioClass.WriteError("IBNK: could not find IBNK.\n");
			}

			var instrumentOffsets = mReader.ReadS32s(cIBNKCount);

			SMSAudioClass.WriteMessage("IBNK: IBNK found, {0} instrument(s)\n", instrumentOffsets.Count(offset => offset != 0));

			for (var i = 0; i < cIBNKCount; ++i) {
				if (instrumentOffsets[i] == 0) {
					continue;
				}

				mReader.Goto(instrumentOffsets[i]);

				var instrumentType = mReader.Read32();
				IInstrument instrument = null;

				SMSAudioClass.WriteMessage("IBNK: #{0,-3} ", i);

				switch (instrumentType) {
					case INST: instrument = LoadMelodic(); break;
					case PERC: instrument = LoadDrumSet(1); break;
					case PER2: instrument = LoadDrumSet(2); break;
				}

				if (instrument == null) {
					SMSAudioClass.WriteMessage("(null)\n");
					continue;
				}

				IBNK.Add(i, instrument);
			}

			mReader.PopAnchor();
			mReader.Back();

			return IBNK;
		}

		MelodicInstrument LoadMelodic() {
			var instrument = new MelodicInstrument();

			mReader.Step(4); // unused

			instrument.Volume = mReader.ReadF32();
			instrument.Pitch = mReader.ReadF32();

			var oscillatorOffsets = mReader.ReadS32s(cOscillatorCount);
			var randomEffectOffsets = mReader.ReadS32s(cRandomEffectCount);
			var senseEffectOffsets = mReader.ReadS32s(cSenseEffectCount);
			var keyRegionOffsets = mReader.ReadS32s(mReader.ReadS32());

			SMSAudioClass.WriteMessage(
				"INST: volume {0:F1} pitch {1:F1} oscillators {2} effects {3} key regions {4}\n",
				instrument.Volume,
				instrument.Pitch,
				oscillatorOffsets.Count(offset => offset != 0),
				(randomEffectOffsets.Count(offset => offset != 0) + senseEffectOffsets.Count(offset => offset != 0)),
				keyRegionOffsets.Length
			);

			foreach (var offset in oscillatorOffsets) {
				if (offset == 0) {
					continue;
				}

				mReader.Goto(offset);
				var osc = LoadOscillator();
				if (osc != null) {
					instrument.AddOscillator(osc);
				}
			}

			foreach (var offset in randomEffectOffsets) {
				if (offset == 0) {
					continue;
				}
				mReader.Goto(offset);
				var effect = LoadRandomEffect();
				if (effect != null) {
					instrument.AddEffect(effect);
				}
			}

			foreach (var offset in senseEffectOffsets) {
				if (offset == 0) {
					continue;
				}

				mReader.Goto(offset);
				var effect = LoadSenseEffect();
				if (effect != null) {
					instrument.AddEffect(effect);
				}
			}

			foreach (var keyRegionOffset in keyRegionOffsets) {
				mReader.Goto(keyRegionOffset);

				var key = mReader.Read8();

				if (key > 127) {
					SMSAudioClass.WriteWarning("IBNK: bad key region key number '{0}' at 0x{1:X6}\n", key, (mReader.Position - 1));
					continue;
				}

				mReader.Step(3); // alignment

				var velRegionOffsets = mReader.ReadS32s(mReader.ReadS32());
				var keyRegion = instrument.AddRegion(key);

				foreach (var velRegionOffset in velRegionOffsets) {
					mReader.Goto(velRegionOffset);

					var velocity = mReader.Read8();

					if (velocity > 127) {
						SMSAudioClass.WriteWarning("IBNK: bad velocity region velocity '{0}' at 0x{1:X6}\n", velocity, (mReader.Position - 1));
						continue;
					}

					mReader.Step(3); // alignment

					var waveid = mReader.Read32();
					var volume = mReader.ReadF32();
					var pitch = mReader.ReadF32();

					keyRegion.AddRegion(velocity, (int)(waveid & 0xFFFF), volume, pitch);
				}
			}

			return instrument;
		}

		DrumSet LoadDrumSet(int version) {
			var drumset = new DrumSet();

			mReader.Step(4); // unused
			mReader.Step(128); // unused 128-item byte array

			var percussionOffsets = mReader.ReadS32s(128);
			sbyte[] panTable = null;
			ushort[] releaseTable = null;

			if (version == 2) {
				panTable = mReader.ReadS8s(128);
				releaseTable = mReader.Read16s(128);
			}

			SMSAudioClass.WriteMessage(
				"PER{0}: {1} percussions\n",
				(version == 2 ? '2' : 'C'),
				percussionOffsets.Count(offset => offset != 0)
			);

			for (var i = 0; i < 128; ++i) {
				if (percussionOffsets[i] == 0) {
					continue;
				}

				mReader.Goto(percussionOffsets[i]);

				var percussion = drumset.AddPercussion(i);

				percussion.Volume = mReader.ReadF32();
				percussion.Pitch = mReader.ReadF32();
				var randomEffectOffsets = mReader.ReadS32s(2);
				var velRegionOffsets = mReader.ReadS32s(mReader.ReadS32());

				if (version == 2) {
					percussion.Pan = ((float)panTable[i] / 127.0f);
					percussion.Release = releaseTable[i];
				}

				foreach (var offset in randomEffectOffsets) {
					if (offset == 0) {
						continue;
					}

					mReader.Goto(offset);
					var effect = LoadRandomEffect();
					if (effect != null) {
						percussion.AddEffect(effect);
					}
				}

				foreach (var velRegionOffset in velRegionOffsets) {
					mReader.Goto(velRegionOffset);

					var velocity = mReader.Read8();

					if (velocity > 127) {
						SMSAudioClass.WriteWarning("Bad velocity region velocity '{0}' at 0x{1:X6}\n", velocity, (mReader.Position - 1));
						return null;
					}

					mReader.Step(3); // alignment

					var waveid = mReader.Read32();
					var volume = mReader.ReadF32();
					var pitch = mReader.ReadF32();

					var velRegion = percussion.AddRegion(velocity, (int)(waveid & 0xFFFF));
					velRegion.Volume = volume;
					velRegion.Pitch = pitch;
				}
			}

			return drumset;
		}

		InstrumentOscillatorInfo LoadOscillator() {
			var oscillator = new InstrumentOscillatorInfo();

			var target = (InstrumentEffectTarget)mReader.Read8();

			if (!target.IsDefined()) {
				SMSAudioClass.WriteWarning("IBNK: bad oscillator target '{0}' at 0x{1:X6}\n", (int)target, (mReader.Position - 1));
				return null;
			}

			oscillator.Target = target;
			mReader.Step(3); // alignment
			oscillator.Rate = mReader.ReadF32();
			var startTableOffset = mReader.ReadS32();
			var releaseTableOffset = mReader.ReadS32();
			oscillator.Width = mReader.ReadF32();
			oscillator.Base = mReader.ReadF32();

			if (startTableOffset != 0) {
				mReader.Goto(startTableOffset);
				InstrumentOscillatorTableMode mode;

				do {
					mode = (InstrumentOscillatorTableMode)mReader.ReadS16();

					if (!mode.IsDefined()) {
						SMSAudioClass.WriteWarning("IBNK: bad oscillator table mode '{0}' at 0x{1:X6}\n", (int)mode, (mReader.Position - 2));
						break;
					}

					var time = mReader.ReadS16();
					var amount = mReader.ReadS16();

					oscillator.AddStartTable(mode, time, amount);
				} while ((int)mode <= 10);
			}

			if (releaseTableOffset != 0) {
				mReader.Goto(releaseTableOffset);
				InstrumentOscillatorTableMode mode;

				do {
					mode = (InstrumentOscillatorTableMode)mReader.ReadS16();

					if (!mode.IsDefined()) {
						SMSAudioClass.WriteWarning("IBNK: bad oscillator table mode '{0}' at 0x{1:X6}\n", (int)mode, (mReader.Position - 2));
						break;
					}

					var time = mReader.ReadS16();
					var amount = mReader.ReadS16();

					oscillator.AddReleaseTable(mode, time, amount);
				} while ((int)mode <= 10) ;
			}

			return oscillator;
		}

		RandomInstrumentEffect LoadRandomEffect() {
			var target = (InstrumentEffectTarget)mReader.Read8();

			if (!target.IsDefined()) {
				SMSAudioClass.WriteWarning("IBNK: bad random effect target '{0}' at 0x{1:X6}\n", (byte)target, (mReader.Position - 1));
				return null;
			}

			mReader.Step(3); // alignment

			var randomBase = mReader.ReadF32();
			var randomDistance = mReader.ReadF32();

			return new RandomInstrumentEffect(target, randomBase, randomDistance);
		}

		SenseInstrumentEffect LoadSenseEffect() {
			var target = (InstrumentEffectTarget)mReader.Read8();

			if (!target.IsDefined()) {
				SMSAudioClass.WriteWarning("IBNK: bad sense effect target '{0}' at 0x{1:X6}\n", (byte)target, (mReader.Position - 1));
				return null;
			}

			var source = (SenseInstrumentEffectTrigger)mReader.Read8();

			if (!source.IsDefined()) {
				source = SenseInstrumentEffectTrigger.None;
			}

			var centerKey = mReader.Read8();

			if (centerKey > 127) {
				SMSAudioClass.WriteWarning("IBNK: bad sense effect center key '{0}' at 0x{1:X6}\n", centerKey, (mReader.Position - 1));
				return null;
			}

			mReader.Step(1); // alignment

			var rangeLo = mReader.ReadF32();
			var rangeHi = mReader.ReadF32();

			return new SenseInstrumentEffect(target, source, centerKey, rangeLo, rangeHi);
		}

	}

	class BinaryIBNKSerializer : BinaryIBNKTransformer {

		aBinaryWriter mWriter;
		InstrumentIBNK mIBNK;
		IEnumerable<InstrumentOscillatorInfo> mOscTable;
		int mIBNKSize, mOscTableSize;

		public BinaryIBNKSerializer(aBinaryWriter writer) {
			mWriter = writer;
		}

		protected override InstrumentIBNK DoTransform(InstrumentIBNK obj) {
			if (obj == null) {
				return null;
			}

			mWriter.PushAnchor();

			WriteInit(obj);
			WriteHeader();
			WriteIBNK();
			WriteOscTable();
			WriteInstruments();

			mWriter.PopAnchor();

			return obj;
		}

		void WriteInit(InstrumentIBNK IBNK) {
			if (!CheckIBNK(IBNK)) {
				SMSAudioClass.WriteError("IBNK: instrument IBNK is incompatible with BNK format.");
			}

			mIBNK = IBNK;
			mOscTable = mIBNK.GenerateOscillatorTable();
			mOscTableSize = mOscTable.Sum(osc => CalculateOscillatorSize(osc));
			mIBNKSize = (cDataStart + mOscTableSize + mIBNK.Sum(instrument => CalculateInstrumentSize(instrument)));
		}

		void WriteHeader() {
			mWriter.Write32(IBNK);
			mWriter.Keep();
			mWriter.WriteS32(mIBNKSize);
			mWriter.WriteS32(mIBNK.VirtualNumber);
			mWriter.WritePadding(32, 0);
		}

		void WriteIBNK() {
			var offset = (cDataStart + mOscTableSize);

			mWriter.Write32(IBNK);

			for (var i = 0; i < cIBNKCount; ++i) {
				if (mIBNK[i] != null) {
					mWriter.WriteS32(offset);
					offset += CalculateInstrumentSize(mIBNK[i]);
				} else {
					mWriter.WriteS32(0);
				}
			}

			mWriter.WritePadding(32, 0);
		}

		void WriteOscTable() {
			foreach (var oscillator in mOscTable) {
				var offset = ((int)mWriter.Position + 32);

				mWriter.Write8((byte)oscillator.Target);
				mWriter.WritePadding(4, 0);
				mWriter.WriteF32(oscillator.Rate);

				if (oscillator.StartTableCount > 0) {
					mWriter.WriteS32(offset);
					offset += SMSAudioClass.RoundUp32B(oscillator.StartTableCount * 6);
				} else {
					mWriter.WriteS32(0);
				}

				if (oscillator.ReleaseTableCount > 0) {
					mWriter.WriteS32(offset);
					offset += SMSAudioClass.RoundUp32B(oscillator.ReleaseTableCount * 6);
				} else {
					mWriter.WriteS32(0);
				}

				mWriter.WriteF32(oscillator.Width);
				mWriter.WriteF32(oscillator.Base);
				mWriter.WritePadding(32, 0);

				for (var i = 0; i < oscillator.StartTableCount; ++i) {
					var table = oscillator.GetStartTable(i);
					mWriter.WriteS16((short)table.mode);
					mWriter.WriteS16((short)table.time);
					mWriter.WriteS16((short)table.amount);
				}

				mWriter.WritePadding(32, 0);

				for (var i = 0; i < oscillator.ReleaseTableCount; ++i) {
					var table = oscillator.GetReleaseTable(i);
					mWriter.WriteS16((short)table.mode);
					mWriter.WriteS16((short)table.time);
					mWriter.WriteS16((short)table.amount);
				}

				mWriter.WritePadding(32, 0);
			}

			mWriter.WritePadding(32, 0);
		}

		void WriteInstruments() {
			for (var i = 0; i < cIBNKCount; ++i) {
				if (mIBNK[i] == null) {
					continue;
				}

				var type = mIBNK[i].Type;

				if (type == InstrumentType.Melodic) {
					var instrument = (mIBNK[i] as MelodicInstrument);

					if (instrument == null) {
						continue;
					}

					WriteMelodic(instrument);
				} else if (type == InstrumentType.DrumSet) {
					var drumset = (mIBNK[i] as DrumSet);

					if (drumset == null) {
						continue;
					}

					WriteDrumSet(drumset);
				}
			}
		}

		void WriteMelodic(MelodicInstrument instrument) {
			var offset = ((int)mWriter.Position + SMSAudioClass.RoundUp16B(44 + 4 * instrument.Count));

			mWriter.Write32(INST);
			mWriter.WriteS32(0); // unused
			mWriter.WriteF32(instrument.Volume);
			mWriter.WriteF32(instrument.Pitch);

			for (var i = 0; i < cOscillatorCount; ++i) {
				if (i < instrument.OscillatorCount) {
					mWriter.WriteS32(CalculateOscillatorOffset(instrument.GetOscillatorAt(i)));
				} else {
					mWriter.WriteS32(0);
				}
			}

			var randomEffects = instrument.Effects.OfType<RandomInstrumentEffect>().ToArray();

			for (var i = 0; i < cRandomEffectCount; ++i) {
				if (i < randomEffects.Length) {
					mWriter.WriteS32(offset);
					offset += 16;
				} else {
					mWriter.WriteS32(0);
				}
			}

			var senseEffects = instrument.Effects.OfType<SenseInstrumentEffect>().ToArray();

			for (var i = 0; i < cSenseEffectCount; ++i) {
				if (i < senseEffects.Length) {
					mWriter.WriteS32(offset);
					offset += 16;
				} else {
					mWriter.WriteS32(0);
				}
			}

			mWriter.WriteS32(instrument.Count);

			foreach (var keyregion in instrument) {
				mWriter.WriteS32(offset);
				offset += CalculateKeyRegionSize(keyregion);
			}

			mWriter.WritePadding(16, 0);

			for (var i = 0; i < 2 && i < randomEffects.Length; ++i) {
				WriteRandomEffect(randomEffects[i]);
			}

			for (var i = 0; i < 2 && i < senseEffects.Length; ++i) {
				WriteSenseEffect(senseEffects[i]);
			}

			foreach (var keyregion in instrument) {
				WriteKeyRegion(keyregion);
			}

			mWriter.WritePadding(32, 0);
		}

		void WriteDrumSet(DrumSet drumset) {
			var offset = ((int)mWriter.Position + 1056);

			mWriter.Write32(PER2);
			mWriter.WriteS32(0); // unused
			mWriter.Write8s(new byte[128]); // unused

			foreach (var percussion in drumset) {
				if (percussion != null) {
					mWriter.WriteS32(offset);
					offset += CalculatePercussionSize(percussion);
				} else {
					mWriter.WriteS32(0);
				}
			}

			foreach (var percussion in drumset) {
				if (percussion != null) {
					mWriter.WriteS8((sbyte)(percussion.Pan * 127.0f));
				} else {
					mWriter.WriteS8(0);
				}
			}

			foreach (var percussion in drumset) {
				if (percussion != null) {
					mWriter.Write16((ushort)percussion.Release);
				} else {
					mWriter.Write16(0);
				}
			}

			mWriter.WritePadding(32, 0);

			foreach (var percussion in drumset) {
				if (percussion == null) {
					continue;
				}

				WritePercussion(percussion);
			}

			mWriter.WritePadding(32, 0);
		}

		void WriteKeyRegion(MelodicKeyRegion keyregion) {
			var offset = ((int)mWriter.Position + SMSAudioClass.RoundUp16B(8 + 4 * keyregion.Count));

			mWriter.Write8((byte)keyregion.Key);
			mWriter.WritePadding(4, 0);
			mWriter.WriteS32(keyregion.Count);

			foreach (var velregion in keyregion) {
				mWriter.WriteS32(offset);
				offset += 16;
			}

			mWriter.WritePadding(16, 0);

			foreach (var velregion in keyregion) {
				WriteVelocityRegion(velregion);
			}
		}

		void WritePercussion(Percussion percussion) {
			var offset = ((int)mWriter.Position + SMSAudioClass.RoundUp16B(20 + 4 * percussion.Count));

			mWriter.WriteF32(percussion.Volume);
			mWriter.WriteF32(percussion.Pitch);

			var randomEffects = percussion.Effects.OfType<RandomInstrumentEffect>().ToArray();

			for (var i = 0; i < cRandomEffectCount; ++i) {
				if (i < randomEffects.Length) {
					mWriter.WriteS32(offset);
					offset += 16;
				} else {
					mWriter.WriteS32(0);
				}
			}

			mWriter.WriteS32(percussion.Count);

			for (var i = 0; i < percussion.Count; ++i) {
				mWriter.WriteS32(offset);
				offset += 16;
			}

			mWriter.WritePadding(16, 0);

			for (var i = 0; i < 2 && i < randomEffects.Length; ++i) {
				WriteRandomEffect(randomEffects[i]);
			}

			foreach (var velregion in percussion) {
				WriteVelocityRegion(velregion);
			}
		}

		void WriteVelocityRegion(InstrumentVelocityRegion velregion) {
			mWriter.Write8((byte)velregion.Velocity);
			mWriter.WritePadding(4, 0);
			mWriter.Write32((uint)(velregion.WaveId & 0xFFFF));
			mWriter.WriteF32(velregion.Volume);
			mWriter.WriteF32(velregion.Pitch);
		}

		void WriteRandomEffect(RandomInstrumentEffect effect) {
			mWriter.Write8((byte)effect.Target);
			mWriter.WritePadding(4, 0);
			mWriter.WriteF32(effect.RandomBase);
			mWriter.WriteF32(effect.RandomDistance);
			mWriter.WritePadding(16, 0);
		}

		void WriteSenseEffect(SenseInstrumentEffect effect) {
			mWriter.Write8((byte)effect.Target);
			mWriter.Write8((byte)effect.Trigger);
			mWriter.Write8((byte)effect.CenterKey);
			mWriter.WritePadding(4, 0);
			mWriter.WriteF32(effect.RangeLo);
			mWriter.WriteF32(effect.RangeHi);
			mWriter.WritePadding(16, 0);
		}

		int CalculateInstrumentSize(IInstrument instrument) {
			if (instrument is MelodicInstrument) {
				var melodic = (instrument as MelodicInstrument);

				return SMSAudioClass.RoundUp32B(
					SMSAudioClass.RoundUp16B(44 + 4 * melodic.Count) +
					(16 * melodic.EffectCount) +
					melodic.Sum(region => CalculateKeyRegionSize(region))
				);
			} else if (instrument is DrumSet) {
				var drumset = (instrument as DrumSet);

				return SMSAudioClass.RoundUp32B(1056 + drumset.Sum(percussion => percussion != null ? CalculatePercussionSize(percussion) : 0));
			}

			return 0;
		}

		int CalculateKeyRegionSize(MelodicKeyRegion region) {
			return (SMSAudioClass.RoundUp16B(8 + 4 * region.Count) + (16 * region.Count));
		}

		int CalculatePercussionSize(Percussion percussion) {
			return (SMSAudioClass.RoundUp16B(20 + 4 * percussion.Count) + (16 * percussion.EffectCount) + (16 * percussion.Count));
		}

		int CalculateOscillatorSize(InstrumentOscillatorInfo oscillator) {
			return (32 + SMSAudioClass.RoundUp32B(oscillator.StartTableCount * 6) + SMSAudioClass.RoundUp32B(oscillator.ReleaseTableCount * 6));
		}

		int CalculateOscillatorOffset(InstrumentOscillatorInfo oscillator) {
			var offset = cDataStart;

			foreach (var osc in mOscTable) {
				if (oscillator.IsEquivalentTo(osc)) {
					break;
				}
				offset += CalculateOscillatorSize(osc);
			}

			return offset;
		}

		const int cDataStart = 1024; // fixed size of the IBNK/IBNK blocks

	}

	abstract class XmlIBNKTransformer : Transformer<InstrumentIBNK> {

		protected const string cIBNK = "IBNK";
		protected const string cVirtualNumber = "virtual-number";

		protected const string cInstrument = "instrument";
		protected const string cDrumSet = "drum-set";
		protected const string cProgram = "program";
		protected const string cVolume = "volume";
		protected const string cPitch = "pitch";
		protected const string cPan = "pan";
		protected const string cRelease = "release";
		protected const string cKey = "key";

		protected const string cKeyRegion = "key-region";
		protected const string cPercussion = "percussion";
		protected const string cVelRegion = "velocity-region";
		protected const string cVelocity = "velocity";
		protected const string cWaveId = "wave-id";

		protected const string cOscillator = "oscillator";
		protected const string cOscTarget = "target";
		protected const string cOscRate = "rate";
		protected const string cOscWidth = "width";
		protected const string cOscBase = "base";
		protected const string cOscStartTable = "start-table";
		protected const string cOscRelTable = "release-table";
		protected const string cOscLinear = "linear";
		protected const string cOscSquare = "square";
		protected const string cOscSquareRoot = "square-root";
		protected const string cOscSampleCell = "sample-cell";
		protected const string cOscTime = "time";
		protected const string cOscOffset = "offset";
		protected const string cOscLoop = "loop";
		protected const string cOscLoopDest = "dest";
		protected const string cOscHold = "hold";
		protected const string cOscStop = "stop";

		protected const string cEffect = "-effect";
		protected const string cEffectTarget = "target";
		protected const string cEffectRand = "random-effect";
		protected const string cEffectRandBase = "base";
		protected const string cEffectRandDistance = "distance";
		protected const string cEffectSense = "sense-effect";
		protected const string cEffectSenseTrigger = "trigger";
		protected const string cEffectSenseCenterKey = "center-key";
		protected const string cEffectSenseRangeLo = "range-lo";
		protected const string cEffectSenseRangeHi = "range-hi";

	}

	class XmlIBNKDeserializer : XmlIBNKTransformer {

		xElement mRootElement;
		string mName;

		public XmlIBNKDeserializer(xElement element, string name) {
			mRootElement = element;
			mName = name;
		}

		protected override InstrumentIBNK DoTransform(InstrumentIBNK obj) {
			if (obj != null) {
				return obj;
			}

			var xvirtualnumber = mRootElement.Attribute(cVirtualNumber);

			if (xvirtualnumber == null) {
				SMSAudioClass.WriteError("XML: line #{0}: missing virtual number", mRootElement.LineNumber);
			}

			var virtualnumber = (xvirtualnumber | -1);

			if (virtualnumber < 0) {
				SMSAudioClass.WriteError("XML: line #{0}: bad virtual number '{1}'.", xvirtualnumber.LineNumber, xvirtualnumber.Value);
			}

			var IBNK = new InstrumentIBNK(virtualnumber, 256);
			IBNK.Name = mName;
			var warnings = SMSAudioClass.WarningCount;

			foreach (var element in mRootElement.Elements()) {
				switch (element.Name) {
					case cInstrument: LoadMelodic(IBNK, element); break;
					case cDrumSet: LoadDrumSet(IBNK, element); break;
				}
			}

			if (SMSAudioClass.WarningCount != warnings) {
				SMSAudioClass.WriteError("XML: bad input xml");
			}

			return IBNK;
		}

		int LoadProgramNumber(xElement element) {
			var attribute = element.Attribute(cProgram);

			if (attribute == null) {
				SMSAudioClass.WriteWarning("XML: line #{0}: missing program number\n", element.LineNumber);
				return -1;
			}

			var program = (attribute | -1);

			if (program < 0 || program > 255) {
				SMSAudioClass.WriteWarning("XML: line #{0}: bad program number '{1}'\n", attribute.LineNumber, attribute.Value);
				return -1;
			}

			return program;
		}

		void LoadMelodic(InstrumentIBNK IBNK, xElement element) {
			var program = LoadProgramNumber(element);

			if (program < 0) {
				return;
			} else if (IBNK[program] != null) {
				SMSAudioClass.WriteWarning("XML: line #{0}: duplicate program number '{1}'\n", element.LineNumber, program);
				return;
			}

			var instrument = LoadMelodic(element);

			if (instrument != null) {
				SMSAudioClass.WriteMessage(
					"#{0,-3} INST: volume {1:F1} pitch {2:F1} oscillators {3} effects {4} key regions {5}\n",
					program,
					instrument.Volume,
					instrument.Pitch,
					instrument.OscillatorCount,
					instrument.EffectCount,
					instrument.Count
				);

				IBNK.Add(program, instrument);
			}
		}

		MelodicInstrument LoadMelodic(xElement xinstrument) {
			xAttribute attribute;

			var instrument = new MelodicInstrument();

			instrument.Volume = (xinstrument.Attribute(cVolume) | 1.0f);
			instrument.Pitch = (xinstrument.Attribute(cPitch) | 1.0f);

			foreach (var xoscillator in xinstrument.Elements(cOscillator)) {
				instrument.AddOscillator(LoadOscillator(xoscillator));
			}

			foreach (var xeffect in xinstrument.Elements().Where(e => e.Name.EndsWith(cEffect))) {
				var effect = LoadEffect(xeffect);

				if (effect == null) {
					continue;
				}

				instrument.AddEffect(effect);
			}

			foreach (var xkeyregion in xinstrument.Elements(cKeyRegion)) {
				attribute = xkeyregion.Attribute(cKey);
				var keynumber = attribute.AsKeyNumber(127);

				if (keynumber < 0 || keynumber > 127) {
					SMSAudioClass.WriteWarning("XML: line #{0}: bad key number '{1}'\n", attribute.LineNumber, attribute.Value);
					continue;
				}

				var keyregion = instrument.AddRegion(keynumber);

				foreach (var xvelregion in xkeyregion.Elements(cVelRegion)) {
					attribute = xvelregion.Attribute(cVelocity);
					var velocity = attribute.AsInt32(127);

					if (velocity < 0 || velocity > 127) {
						SMSAudioClass.WriteWarning("XML: line #{0}: bad velocity '{1}'\n", attribute.LineNumber, attribute.Value);
						continue;
					}

					attribute = xvelregion.Attribute(cWaveId);

					if (attribute == null) {
						SMSAudioClass.WriteWarning("XML: line #{0}: missing wave id\n", xvelregion.LineNumber);
						continue;
					}

					var waveid = attribute.AsInt32();

					if (waveid < 0) {
						SMSAudioClass.WriteWarning("XML: line #{0}: bad wave id '{1}'\n", attribute.LineNumber, attribute.Value);
						continue;
					}

					var volume = (xvelregion.Attribute(cVolume) | 1.0f);
					var pitch = (xvelregion.Attribute(cPitch) | 1.0f);

					keyregion.AddRegion(velocity, waveid, volume, pitch);
				}
			}

			return instrument;
		}

		void LoadDrumSet(InstrumentIBNK IBNK, xElement element) {
			var program = LoadProgramNumber(element);

			if (program < 0) {
				return;
			} else if (IBNK[program] != null) {
				SMSAudioClass.WriteWarning("XML: line #{0}: duplicate program number '{1}'\n", element.LineNumber, program);
				return;
			}

			var drumset = LoadDrumSet(element);

			if (drumset != null) {
				SMSAudioClass.WriteMessage(
					"#{0,-3} PER2: {1} percussion(s)\n",
					program,
					drumset.Count
				);

				IBNK.Add(program, drumset);
			}
		}

		DrumSet LoadDrumSet(xElement xdrumset) {
			xAttribute attribute;

			var drumset = new DrumSet();

			foreach (var xpercussion in xdrumset.Elements(cPercussion)) {
				attribute = xpercussion.Attribute(cKey);

				if (attribute == null) {
					SMSAudioClass.WriteWarning("XML: line #{0}: missing key number\n", xpercussion.LineNumber);
					continue;
				}

				var keynumber = attribute.AsKeyNumber();

				if (keynumber < 0 || keynumber > 127) {
					SMSAudioClass.WriteWarning("XML: line #{0}: bad key number '{0}'\n", attribute.LineNumber, attribute.Value);
					continue;
				}

				var percussion = drumset.AddPercussion(keynumber);

				percussion.Volume = (xpercussion.Attribute(cVolume) | 1.0f);
				percussion.Pitch = (xpercussion.Attribute(cPitch) | 1.0f);
				percussion.Pan = (xpercussion.Attribute(cPan) | 0.5f);

				foreach (var xeffect in xpercussion.Elements().Where(e => e.Name.EndsWith(cEffect))) {
					var effect = LoadEffect(xeffect);

					if (effect == null) {
						continue;
					}

					percussion.AddEffect(effect);
				}

				foreach (var xvelregion in xpercussion.Elements(cVelRegion)) {
					attribute = xvelregion.Attribute(cVelocity);
					var velocity = attribute.AsInt32(127);

					if (velocity < 0 || velocity > 127) {
						SMSAudioClass.WriteWarning("XML: line #{0}: bad velocity '{1}'\n", attribute.LineNumber, attribute.Value);
						continue;
					}

					attribute = xvelregion.Attribute(cWaveId);

					if (attribute == null) {
						SMSAudioClass.WriteWarning("XML: line #{0}: missing wave id\n", xvelregion.LineNumber);
						continue;
					}

					var waveid = attribute.AsInt32();

					if (waveid < 0) {
						SMSAudioClass.WriteWarning("XML: line #{0}: bad wave id '{1}'\n", attribute.LineNumber, attribute.Value);
						continue;
					}

					var volume = (xvelregion.Attribute(cVolume) | 1.0f);
					var pitch = (xvelregion.Attribute(cPitch) | 1.0f);

					percussion.AddRegion(velocity, waveid, volume, pitch);
				}
			}

			return drumset;
		}
		
		InstrumentOscillatorInfo LoadOscillator(xElement xoscillator) {
			var oscillator = new InstrumentOscillatorInfo();
			var xtarget = xoscillator.Attribute("target");

			if (xtarget == null) {
				SMSAudioClass.WriteError("XML: line #{0}: missing oscillator target.", xoscillator.LineNumber);
			}

			var target = xtarget.AsEnum((InstrumentEffectTarget)(-1));

			if (!target.IsDefined()) {
				SMSAudioClass.WriteError("XML: line #{0}: bad oscillator target '{1}'.", xtarget.LineNumber, xtarget.Value);
			}

			oscillator.Target = target;
			oscillator.Rate = (xoscillator.Attribute(cOscRate) | 1.0f);
			oscillator.Width = (xoscillator.Attribute(cOscWidth) | 1.0f);
			oscillator.Base = (xoscillator.Attribute(cOscBase) | 0.0f);

			var xtable = xoscillator.Element(cOscStartTable);

			if (xtable != null) {
				foreach (var table in LoadOscillatorTable(xtable)) {
					oscillator.AddStartTable(table.mode, table.time, table.amount);
				}
			}

			var xreltable = xoscillator.Element(cOscRelTable);

			if (xreltable != null) {
				foreach (var table in LoadOscillatorTable(xreltable)) {
					oscillator.AddReleaseTable(table.mode, table.time, table.amount);
				}
			}

			return oscillator;
		}

		IEnumerable<InstrumentOscillatorTable> LoadOscillatorTable(xElement xtable) {
			var tables = new List<InstrumentOscillatorTable>();

			foreach (var child in xtable) {
				var table = new InstrumentOscillatorTable();

				switch (child.Name) {
					case cOscLinear: {
						table.mode = InstrumentOscillatorTableMode.Linear;
						table.time = (child.Attribute(cOscTime) | 0);
						table.amount = (child.Attribute(cOscOffset) | 0);
						break;
					}
					case cOscSquare: {
						table.mode = InstrumentOscillatorTableMode.Square;
						table.time = (child.Attribute(cOscTime) | 0);
						table.amount = (child.Attribute(cOscOffset) | 0);
						break;
					}
					case cOscSquareRoot: {
						table.mode = InstrumentOscillatorTableMode.SquareRoot;
						table.time = (child.Attribute(cOscTime) | 0);
						table.amount = (child.Attribute(cOscOffset) | 0);
						break;
					}
					case cOscSampleCell: {
						table.mode = InstrumentOscillatorTableMode.SampleCell;
						table.time = (child.Attribute(cOscTime) | 0);
						table.amount = (child.Attribute(cOscOffset) | 0);
						break;
					}
					case cOscLoop: {
						table.mode = InstrumentOscillatorTableMode.Loop;
						table.time = (child.Attribute(cOscLoopDest) | 0);
						break;
					}
					case cOscHold: table.mode = InstrumentOscillatorTableMode.Hold; break;
					case cOscStop: table.mode = InstrumentOscillatorTableMode.Stop; break;
					default: SMSAudioClass.WriteError("XML: unknown oscillator table mode '{0}'.", child.Name); break;
				}

				tables.Add(table);
			}

			return tables;
		}

		InstrumentEffect LoadEffect(xElement xeffect) {
			switch (xeffect.Name) {
				case cEffectRand: return LoadRandomEffect(xeffect);
				case cEffectSense: return LoadSenseEffect(xeffect);
			}

			return null;
		}

		RandomInstrumentEffect LoadRandomEffect(xElement xeffect) {
			var target = xeffect.Attribute(cEffectTarget).AsEnum(InstrumentEffectTarget.Volume);
			var randomBase = (xeffect.Attribute(cEffectRandBase) | 1.0f);
			var randomDistance = (xeffect.Attribute(cEffectRandDistance) | 0.0f);

			return new RandomInstrumentEffect(target, randomBase, randomDistance);
		}

		SenseInstrumentEffect LoadSenseEffect(xElement xeffect) {
			var target = xeffect.Attribute(cEffectTarget).AsEnum(InstrumentEffectTarget.Volume);
			var trigger = xeffect.Attribute(cEffectSenseTrigger).AsEnum(SenseInstrumentEffectTrigger.Key);
			var centerKey = xeffect.Attribute(cEffectSenseCenterKey).AsKeyNumber(127);
			var rangeLo = (xeffect.Attribute(cEffectSenseRangeLo) | 0.0f);
			var rangeHi = (xeffect.Attribute(cEffectSenseRangeHi) | 1.0f);

			return new SenseInstrumentEffect(target, trigger, centerKey, rangeLo, rangeHi);
		}
		
	}

	class XmlIBNKSerializer : XmlIBNKTransformer {

		XmlWriter mWriter;
		InstrumentIBNK mIBNK;

		public XmlIBNKSerializer(XmlWriter writer) {
			mWriter = writer;
		}

		protected override InstrumentIBNK DoTransform(InstrumentIBNK obj) {
			if (obj == null) {
				return null;
			}

			mIBNK = obj;

			WriteIBNK();
			mWriter.Flush();

			return obj;
		}

		void WriteIBNK() {
			mWriter.WriteStartElement(cIBNK);
			mWriter.WriteAttributeString(cVirtualNumber, mIBNK.VirtualNumber);

			for (var program = 0; program < mIBNK.Capacity; ++program) {
				if (mIBNK[program] == null) {
					continue;
				}

				switch (mIBNK[program].Type) {
					case InstrumentType.Melodic: WriteMelodic((mIBNK[program] as MelodicInstrument), program); break;
					case InstrumentType.DrumSet: WriteDrumSet((mIBNK[program] as DrumSet), program); break;
				}
			}

			mWriter.WriteEndElement();
		}

		void WriteMelodic(MelodicInstrument instrument, int program) {
			mWriter.WriteStartElement(cInstrument);
			mWriter.WriteAttributeString(cProgram, program);

			foreach (var oscillator in instrument.Oscillators) {
				WriteOscillator(oscillator);
			}

			foreach (var effect in instrument.Effects) {
				WriteEffect(effect);
			}

			foreach (var keyregion in instrument) {
				mWriter.WriteStartElement(cKeyRegion);

				if (instrument.Count > 1 || keyregion.Key != 127) {
					mWriter.WriteAttributeString(cKey, SMSAudioClass.ConvertKey(keyregion.Key));
				}

				foreach (var velregion in keyregion) {
					mWriter.WriteStartElement(cVelRegion);

					if (keyregion.Count > 1 || keyregion[0].Velocity < 127) {
						mWriter.WriteAttributeString(cVelocity, velregion.Velocity);
					}

					mWriter.WriteAttributeString(cWaveId, velregion.WaveId);

					if (velregion.Volume != 1.0f) {
						mWriter.WriteAttributeString(cVolume, velregion.Volume);
					}

					if (velregion.Pitch != 1.0f) {
						mWriter.WriteAttributeString(cPitch, velregion.Pitch);
					}

					mWriter.WriteEndElement();
				}

				mWriter.WriteEndElement();
			}

			mWriter.WriteEndElement();
		}

		void WriteDrumSet(DrumSet drumset, int program) {
			mWriter.WriteStartElement(cDrumSet);
			mWriter.WriteAttributeString(cProgram, program);

			foreach (var percussion in drumset.Percussions) {
				if (percussion == null) {
					continue;
				}

				mWriter.WriteStartElement(cPercussion);
				mWriter.WriteAttributeString(cKey, SMSAudioClass.ConvertKey(percussion.Key));

				if (percussion.Volume != 1.0f) {
					mWriter.WriteAttributeString(cVolume, percussion.Volume);
				}

				if (percussion.Pitch != 1.0f) {
					mWriter.WriteAttributeString(cPitch, percussion.Pitch);
				}

				if (percussion.Pan != 0.5f) {
					mWriter.WriteAttributeString(cPan, percussion.Pan);
				}

				foreach (var effect in percussion.Effects) {
					WriteEffect(effect);
				}

				foreach (var velregion in percussion) {
					mWriter.WriteStartElement(cVelRegion);

					if (percussion.Count > 1 || percussion[0].Velocity < 127) {
						mWriter.WriteAttributeString(cVelocity, velregion.Velocity);
					}

					mWriter.WriteAttributeString(cWaveId, velregion.WaveId);

					if (velregion.Volume != 1.0f) {
						mWriter.WriteAttributeString(cVolume, velregion.Volume);
					}

					if (velregion.Pitch != 1.0f) {
						mWriter.WriteAttributeString(cPitch, velregion.Pitch);
					}

					mWriter.WriteEndElement();
				}

				mWriter.WriteEndElement();
			}

			mWriter.WriteEndElement();
		}

		void WriteOscillator(InstrumentOscillatorInfo oscillator) {
			mWriter.WriteStartElement(cOscillator);
			mWriter.WriteAttributeString(cOscTarget, oscillator.Target.ToLowerString());
			mWriter.WriteAttributeString(cOscRate, oscillator.Rate);
			mWriter.WriteAttributeString(cOscWidth, oscillator.Width);
			mWriter.WriteAttributeString(cOscBase, oscillator.Base);

			if (oscillator.StartTableCount > 0) {
				mWriter.WriteStartElement(cOscStartTable);

				for (var i = 0; i < oscillator.StartTableCount; ++i) {
					WriteOscillatorTable(oscillator.GetStartTable(i));
				}

				mWriter.WriteEndElement();
			}

			if (oscillator.ReleaseTableCount > 0) {
				mWriter.WriteStartElement(cOscRelTable);

				for (var i = 0; i < oscillator.ReleaseTableCount; ++i) {
					WriteOscillatorTable(oscillator.GetReleaseTable(i));
				}

				mWriter.WriteEndElement();
			}

			mWriter.WriteEndElement();
		}

		void WriteOscillatorTable(InstrumentOscillatorTable table) {
			string name;

			switch (table.mode) {
				case InstrumentOscillatorTableMode.Linear: name = cOscLinear; break;
				case InstrumentOscillatorTableMode.Square: name = cOscSquare; break;
				case InstrumentOscillatorTableMode.SquareRoot: name = cOscSquareRoot; break;
				case InstrumentOscillatorTableMode.SampleCell: name = cOscSampleCell; break;
				case InstrumentOscillatorTableMode.Loop: name = cOscLoop; break;
				case InstrumentOscillatorTableMode.Hold: name = cOscHold; break;
				case InstrumentOscillatorTableMode.Stop: name = cOscStop; break;
				default: return;
			}

			mWriter.WriteStartElement(name);

			switch (table.mode) {
				case InstrumentOscillatorTableMode.Loop: {
					mWriter.WriteAttributeString(cOscLoopDest, table.time);
					break;
				}
				case InstrumentOscillatorTableMode.Hold: break;
				case InstrumentOscillatorTableMode.Stop: break;
				default: {
					mWriter.WriteAttributeString(cOscTime, table.time);
					mWriter.WriteAttributeString(cOscOffset, table.amount);
					break;
				}
			}

			mWriter.WriteEndElement();
		}

		void WriteEffect(InstrumentEffect effect) {
			if (effect is RandomInstrumentEffect) {
				WriteRandomEffect(effect as RandomInstrumentEffect);
			} else if (effect is SenseInstrumentEffect) {
				WriteSenseEffect(effect as SenseInstrumentEffect);
			}
		}

		void WriteRandomEffect(RandomInstrumentEffect effect) {
			mWriter.WriteStartElement(cEffectRand);
			mWriter.WriteAttributeString(cEffectTarget, effect.Target.ToLowerString());
			mWriter.WriteAttributeString(cEffectRandBase, effect.RandomBase);
			mWriter.WriteAttributeString(cEffectRandDistance, effect.RandomDistance);
			mWriter.WriteEndElement();
		}

		void WriteSenseEffect(SenseInstrumentEffect effect) {
			mWriter.WriteStartElement(cEffectSense);
			mWriter.WriteAttributeString(cEffectTarget, effect.Target.ToLowerString());
			mWriter.WriteAttributeString(cEffectSenseTrigger, effect.Trigger.ToLowerString());

			if (effect.CenterKey < 127) {
				mWriter.WriteAttributeString(cEffectSenseCenterKey, SMSAudioClass.ConvertKey(effect.CenterKey));
			}

			mWriter.WriteAttributeString(cEffectSenseRangeLo, effect.RangeLo);
			mWriter.WriteAttributeString(cEffectSenseRangeHi, effect.RangeHi);
			mWriter.WriteEndElement();
		}
	}

	abstract class SoundFontIBNKTransformer : Transformer<InstrumentIBNK> {
		protected List<Preset> mPresets;
		protected List<Bag> mPresetBags;
		protected List<GenList> mPresetGenerators;
		protected List<SFInst> mInstruments;
		protected List<Bag> mInstrumentBags;
		protected List<GenList> mInstrumentGenerators;
		
		protected class Preset {
			string mName;
			short mPreset;
			short mIBNK;
			short mPresetBagIndex;
			int mLibrary;
			int mGenre;
			int mMorphology;
			
			public Preset() {
			}
			
			public Preset(string name, short preset, short IBNK, short presetBagIndex) {
				mName = name;
				mPreset = preset;
				mIBNK = IBNK;
				mPresetBagIndex = presetBagIndex;
			}
			
			public void Read(aBinaryReader reader) {
				mName = reader.ReadString(20);
				mPreset = reader.ReadS16();
				mIBNK = reader.ReadS16();
				mPresetBagIndex = reader.ReadS16();
				mLibrary = reader.ReadS32();
				mGenre = reader.ReadS32();
				mMorphology = reader.ReadS32();
			}
			
			public void Write(aBinaryWriter writer) {
				writer.WriteString(mName);
				for (var j = mName.Length; j < 20; j++) writer.WriteS8(0);
				writer.WriteS16(mPreset);
				writer.WriteS16(mIBNK);
				writer.WriteS16(mPresetBagIndex);
				writer.WriteS32(mLibrary);
				writer.WriteS32(mGenre);
				writer.WriteS32(mMorphology);
			}
		}
		
		protected class Bag {
			short mGenIndex;
			short mModIndex;
			
			public Bag() {
			}
			
			public Bag(short genIndex, short modIndex) {
				mGenIndex = genIndex;
				mModIndex = modIndex;
			}
			
			public void Read(aBinaryReader reader) {
				mGenIndex = reader.ReadS16();
				mModIndex = reader.ReadS16();
			}
			
			public void Write(aBinaryWriter writer) {
				writer.WriteS16(mGenIndex);
				writer.WriteS16(mModIndex);
			}
		}
		
		protected enum SFGenerator {
			startAddrsOffset = 0,
			endAddrsOffset = 1,
			startloopAddrsOffset = 2,
			endloopAddrsOffset = 3,
			startAddrsCoarseOffset = 4,
			modLfoToPitch = 5,
			vibLfoToPitch = 6,
			modEnvToPitch = 7,
			initialFilterFc = 8,
			initialFilterQ = 9,
			modLfoToFilterFc = 10,
			modEnvToFilterFc = 11,
			endAddrsCoarseOffset = 12,
			modLfoToVolume = 13,
			unused1 = 14,
			chorusEffectsSend = 15,
			reverbEffectsSend = 16,
			pan = 17,
			unused2 = 18,
			unused3 = 19,
			unused4 = 20,
			delayModLFO = 21,
			freqModLFO = 22,
			delayVibLFO = 23,
			freqVibLFO = 24,
			delayModEnv = 25,
			attackModEnv = 26,
			holdModEnv = 27,
			decayModEnv = 28,
			sustainModEnv = 29,
			releaseModEnv = 30,
			keynumToModEnvHold = 31,
			keynumToModEnvDecay = 32,
			delayVolEnv = 33,
			attackVolEnv = 34,
			holdVolEnv = 35,
			decayVolEnv = 36,
			sustainVolEnv = 37,
			releaseVolEnv = 38,
			keynumToVolEnvHold = 39,
			keynumToVolEnvDecay = 40,
			instrument = 41,
			reserved1 = 42,
			keyRange = 43,
			velRange = 44,
			startloopAddrsCoarseOffset = 45,
			keynum = 46,
			velocity = 47,
			initialAttenuation = 48,
			reserved2 = 49,
			endloopAddrsCoarseOffset = 50,
			coarseTune = 51,
			fineTune = 52,
			sampleID = 53,
			sampleModes = 54,
			reserved3 = 55,
			scaleTuning = 56,
			exclusiveClass = 57,
			overridingRootKey = 58,
			unused5 = 59,
			endOper = 60
		}
		
		protected class GenList {
			SFGenerator mGenOper;
			byte mRangeLo;
			byte mRangeHi;
			short mAmount;
			
			public GenList() {
			}
			
			public GenList(SFGenerator genOper, short amount) {
				mGenOper = genOper;
				mAmount = amount;
			}
			
			public GenList(SFGenerator genOper, byte lo, byte hi) {
				mGenOper = genOper;
				mRangeLo = lo;
				mRangeHi = hi;
			}
			
			public void Read(aBinaryReader reader) {
				mGenOper = (SFGenerator)reader.ReadS16();
				if (mGenOper == SFGenerator.keyRange || mGenOper == SFGenerator.velRange) {
					mRangeLo = reader.Read8();
					mRangeHi = reader.Read8();
				}
				else {
					mAmount = reader.ReadS16();
				}
			}
			
			public void Write(aBinaryWriter writer) {
				writer.WriteS16((short)mGenOper);
				if (mGenOper == SFGenerator.keyRange || mGenOper == SFGenerator.velRange) {
					writer.Write8(mRangeLo);
					writer.Write8(mRangeHi);
				}
				else {
					writer.WriteS16(mAmount);
				}
			}
		}
		
		protected class SFInst {
			string mName;
			short mInstBagIndex;
			
			public SFInst() {
			}
			
			public SFInst(string name, short bagIndex) {
				mName = name;
				mInstBagIndex = bagIndex;
			}
			
			public void Read(aBinaryReader reader) {
				mName = reader.ReadString(20);
				mInstBagIndex = reader.ReadS16();
			}
			
			public void Write(aBinaryWriter writer) {
				writer.WriteString(mName);
				for (var j = mName.Length; j < 20; j++) writer.WriteS8(0);
				writer.WriteS16(mInstBagIndex);
			}
		}
	}

	class SoundFontIBNKSerializer : SoundFontIBNKTransformer {
		aBinaryWriter mWriter;
		InstrumentIBNK mIBNK;

		public SoundFontIBNKSerializer(aBinaryWriter writer) {
			mWriter = writer;
		}

		protected override InstrumentIBNK DoTransform(InstrumentIBNK obj) {
			if (obj == null) {
				return null;
			}
			
			mIBNK = obj;
			
			mPresets = new List<Preset>();
			mPresetBags = new List<Bag>();
			mPresetGenerators = new List<GenList>();
			mInstruments = new List<SFInst>();
			mInstrumentBags = new List<Bag>();
			mInstrumentGenerators = new List<GenList>();
			for (var program = 0; program < mIBNK.Capacity; ++program) {
				if (mIBNK[program] == null) {
					continue;
				}
				AddInstrument(program);
			}

			mWriter.PushAnchor();

			mWriter.WriteString("RIFF");
			mWriter.WriteS32(CalculateInfoSize() + 8 + CalculateSmplSize() + 20 + CalculatePdtaSize() + 8 + 4);
			mWriter.WriteString("sfbk");
			
			WriteInfo();
			
			WriteSdta();
			
			WritePdta();

			mWriter.PopAnchor();

			return obj;
		}
		
		void AddInstrument(int program) {
			var name = String.Format("{0:D5}-{1:D5}", mIBNK.VirtualNumber, program);
			short IBNK;
			if (mIBNK[program].Type == InstrumentType.DrumSet) {
				IBNK = (short)128;
			}
			else {
				IBNK = (short)mIBNK.VirtualNumber;
			}
			mPresets.Add(new Preset(name, (short)program, IBNK, (short)mPresetBags.Count));
			mPresetBags.Add(new Bag((short)mPresetGenerators.Count, 0));
			mPresetBags.Add(new Bag((short)mPresetGenerators.Count, 0));
			mPresetGenerators.Add(new GenList(SFGenerator.keyRange, 0, 127));
			mPresetGenerators.Add(new GenList(SFGenerator.instrument, (short)mInstruments.Count));
			
			mInstruments.Add(new SFInst(name, (short)mInstrumentBags.Count));
			switch (mIBNK[program].Type) {
			case InstrumentType.Melodic:
				AddMelodic((MelodicInstrument)mIBNK[program]);
				break;
			case InstrumentType.DrumSet:
				AddDrumset((DrumSet)mIBNK[program]);
				break;
			}
		}
		
		short AmplitudeToCentibel(double vol) {
			double power = vol*vol;
			double decibel = 10.0*System.Math.Log10(power);
			return (short)(decibel*-10.0);
		}
		
		short PitchToCents(double pitch) {
			double cents = 1200.0*System.Math.Log(pitch, 2.0);
			return (short)(cents);
		}
		
		void AddMelodic(MelodicInstrument inst) {
			mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
			mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
			mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
			
			foreach (InstrumentOscillatorInfo osc in inst.Oscillators) {
				if (osc.Target != InstrumentEffectTarget.Volume) {
					continue;
				}
				if (osc.StartTableCount > 0 && osc.GetStartTable(0).time > 0) {
					// can only determine attack time
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
				}
				if (osc.StartTableCount > 1 && osc.GetStartTable(1).time > 0) {
					// can determine decay time
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
				}
				if (osc.StartTableCount > 1 && osc.GetStartTable(1).amount + osc.Base > 0) {
					// sustain volume
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
				}
				if (osc.ReleaseTableCount > 0 && osc.GetReleaseTable(0).time > 0) {
					// release time
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
				}
			}
			
			mInstrumentGenerators.Add(new GenList(SFGenerator.initialAttenuation, AmplitudeToCentibel(inst.Volume)));
			mInstrumentGenerators.Add(new GenList(SFGenerator.fineTune, PitchToCents(inst.Pitch)));
			mInstrumentGenerators.Add(new GenList(SFGenerator.sampleModes, 1));
			
			foreach (InstrumentOscillatorInfo osc in inst.Oscillators) {
				if (osc.Target != InstrumentEffectTarget.Volume) {
					continue;
				}
				if (osc.StartTableCount > 0 && osc.GetStartTable(0).time > 0) {
					// can only determine attack time
					double attackSeconds = (double)(osc.GetStartTable(0).time)*(double)osc.Rate/600.0;
					mInstrumentGenerators.Add(new GenList(SFGenerator.attackVolEnv, PitchToCents(attackSeconds)));
				}
				if (osc.StartTableCount > 1 && osc.GetStartTable(1).time > 0) {
					// can determine decay time
					double decaySeconds = (double)(osc.GetStartTable(1).time)*(double)osc.Rate/600.0;
					mInstrumentGenerators.Add(new GenList(SFGenerator.decayVolEnv, PitchToCents(decaySeconds)));
				}
				if (osc.StartTableCount > 1 && osc.GetStartTable(1).amount + osc.Base > 0) {
					// sustain volume
					double sustain = ((double)(osc.GetStartTable(1).amount)/32767.0*(double)osc.Width) + (double)osc.Base;
					mInstrumentGenerators.Add(new GenList(SFGenerator.sustainVolEnv, AmplitudeToCentibel(sustain)));
				}
				if (osc.ReleaseTableCount > 0 && osc.GetReleaseTable(0).time > 0) {
					// release time
					double releaseSeconds = (double)(osc.GetReleaseTable(0).time)*(double)osc.Rate/600.0;
					mInstrumentGenerators.Add(new GenList(SFGenerator.releaseVolEnv, PitchToCents(releaseSeconds)));
				}
			}
			
			int lastKey = 0;
			foreach (MelodicKeyRegion keyRegion in inst) {
				int lastVel = 0;
				foreach (InstrumentVelocityRegion velRegion in keyRegion) {
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));

					mInstrumentGenerators.Add(new GenList(SFGenerator.keyRange, (byte)lastKey, (byte)keyRegion.Key));
					mInstrumentGenerators.Add(new GenList(SFGenerator.velRange, (byte)lastVel, (byte)velRegion.Velocity));
					mInstrumentGenerators.Add(new GenList(SFGenerator.initialAttenuation, AmplitudeToCentibel(velRegion.Volume)));
					mInstrumentGenerators.Add(new GenList(SFGenerator.fineTune, PitchToCents(velRegion.Pitch)));
					mInstrumentGenerators.Add(new GenList(SFGenerator.sampleID, (short)(velRegion.WaveId)));
					lastVel = velRegion.Velocity+1;
				}
				lastKey = keyRegion.Key+1;
			}
		}
		
		void AddDrumset(DrumSet drums) {
			foreach (Percussion perc in drums) {
				if (perc == null) {
					continue;
				}
				int lastVel = 0;
				foreach (InstrumentVelocityRegion velRegion in perc) {
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));
					mInstrumentBags.Add(new Bag((short)mInstrumentGenerators.Count, 0));

					mInstrumentGenerators.Add(new GenList(SFGenerator.keyRange, (byte)perc.Key, (byte)perc.Key));
					mInstrumentGenerators.Add(new GenList(SFGenerator.velRange, (byte)lastVel, (byte)velRegion.Velocity));
					mInstrumentGenerators.Add(new GenList(SFGenerator.initialAttenuation, AmplitudeToCentibel(velRegion.Volume*perc.Volume)));
					mInstrumentGenerators.Add(new GenList(SFGenerator.fineTune, PitchToCents(velRegion.Pitch*perc.Pitch)));
					mInstrumentGenerators.Add(new GenList(SFGenerator.pan, (short)((perc.Pan-0.5f)*1000.0f)));
					mInstrumentGenerators.Add(new GenList(SFGenerator.releaseVolEnv, PitchToCents(perc.Release/32767.0)));
					mInstrumentGenerators.Add(new GenList(SFGenerator.overridingRootKey, (short)perc.Key));
					mInstrumentGenerators.Add(new GenList(SFGenerator.sampleID, (short)(velRegion.WaveId)));
					lastVel = velRegion.Velocity+1;
				}
			}
		}
		
		void WriteInfo() {
			mWriter.WriteString("LIST");
			mWriter.WriteS32(CalculateInfoSize());
			
			mWriter.WriteString("INFO");
			
			mWriter.WriteString("ifil");
			mWriter.WriteS32(4);
			mWriter.WriteS16(2);
			mWriter.WriteS16(1);
			
			mWriter.WriteString("isng");
			mWriter.WriteS32(8);
			mWriter.WriteString("EMU8000");
			mWriter.WriteS8(0);
			
			mWriter.WriteString("INAM");
			mWriter.WriteS32(((mIBNK.Name.Length + 2) / 2) * 2);
			mWriter.WriteString(mIBNK.Name);
			mWriter.WriteS8(0);
			mWriter.WritePadding(2, 0);
		}
		
		int CalculateInfoSize() {
			return 4 + 12 + 16 + 8 + (((mIBNK.Name.Length + 2) / 2) * 2);
		}
		
		void WriteSdta() {
			mWriter.WriteString("LIST");
			mWriter.WriteS32(CalculateSmplSize() + 12);
			
			mWriter.WriteString("sdta");
			
			mWriter.WriteString("smpl");
			mWriter.WriteS32(CalculateSmplSize());
		}
		
		int CalculateSmplSize() {
			return 0;
		}
		
		void WritePdta() {
			mWriter.WriteString("LIST");
			mWriter.WriteS32(CalculatePdtaSize());
			
			mWriter.WriteString("pdta");
			
			mWriter.WriteString("phdr");
			mWriter.WriteS32(38 + 38*mPresets.Count);
			foreach (Preset preset in mPresets) preset.Write(mWriter);
			mWriter.WriteString("EOP");
			for (var i = 0; i < 21; i++) mWriter.WriteS8(0);
			mWriter.WriteS16((short)mPresetBags.Count);
			for (var i = 0; i < 12; i++) mWriter.WriteS8(0);
			
			mWriter.WriteString("pbag");
			mWriter.WriteS32(4 + 4*mPresetBags.Count);
			foreach (Bag bag in mPresetBags) bag.Write(mWriter);
			mWriter.WriteS16((short)mPresetGenerators.Count);
			mWriter.WriteS16((short)0);
			
			mWriter.WriteString("pmod");
			mWriter.WriteS32(10);
			for (var i = 0; i < 10; i++) mWriter.WriteS8(0);
			
			mWriter.WriteString("pgen");
			mWriter.WriteS32(4 + 4*mPresetGenerators.Count);
			foreach (GenList gen in mPresetGenerators) gen.Write(mWriter);
			mWriter.WriteS32(0);
			
			mWriter.WriteString("inst");
			mWriter.WriteS32(22 + 22*mInstruments.Count);
			foreach (SFInst inst in mInstruments) inst.Write(mWriter);
			mWriter.WriteString("EOI");
			for (var i = 0; i < 19; i++) mWriter.WriteS8(0);
			
			mWriter.WriteString("ibag");
			mWriter.WriteS32(4 + 4*mInstrumentBags.Count);
			foreach (Bag bag in mInstrumentBags) bag.Write(mWriter);
			mWriter.WriteS16(0);
			mWriter.WriteS16(0);
			
			mWriter.WriteString("imod");
			mWriter.WriteS32(10);
			for (var i = 0; i < 10; i++) mWriter.WriteS8(0);
			
			mWriter.WriteString("igen");
			mWriter.WriteS32(4 + 4*mInstrumentGenerators.Count);
			foreach (GenList gen in mInstrumentGenerators) gen.Write(mWriter);
			mWriter.WriteS32(0);
			
			mWriter.WriteString("shdr");
			mWriter.WriteS32(46);
			mWriter.WriteString("EOS");
			for (var i = 0; i < 43; i++) mWriter.WriteS8(0);
		}
		
		int CalculatePdtaSize() {
			return 4 +
				46 + 38*mPresets.Count + // phdr
				12 + 4*mPresetBags.Count + // pbag
				18 + // pmod
				12 + 4*mPresetGenerators.Count + // pgen
				30 + 22*mInstruments.Count + // inst
				12 + 4*mInstrumentBags.Count + // ibag
				18 + // imod
				12 + 4*mInstrumentGenerators.Count + // igen
				54; // shdr
		}
	}
}
