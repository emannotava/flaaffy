
using System;

namespace arookas {

	static partial class SMSAudioClass {

		static Version sVersion = new Version(0, 6, 1);

		static void Main(string[] arguments) {
			Console.Title = String.Format("SMSAudioClass v{0} arookas", sVersion);
			SMSAudioClass.WriteMessage("SMSAudioClass v{0} arookas\n", sVersion);
			SMSAudioClass.WriteSeparator('=');

			if (arguments.Length == 0) {
				ShowUsage();
			}

			bool help = false;
			string name = null;
			int i;

			for (i = 0; (name == null && i < arguments.Length); ++i) {
				switch (arguments[i]) {
					case "-help": {
						help = true;
						break;
					}
					case "-errand": {
						if ((i + 1) >= arguments.Length) {
							ShowUsage();
						}

						name = arguments[++i];
						break;
					}
				}
			}

			if (name == null) {
				ShowUsage();
			}

			var errand = SMSAudioClass.ReadErrand(name);
			var instance = SMSAudioClass.InitErrand(errand);

			if (help) {
				instance.ShowUsage();
			} else {
				string[] args = new string[arguments.Length - i];
				Array.Copy(arguments, i, args, 0, args.Length);
				instance.LoadParams(args);
				instance.Perform();

				SMSAudioClass.WriteLine();
				SMSAudioClass.WriteSeparator('-');

				if (sWarningCount > 0) {
					SMSAudioClass.WriteMessage("Completed with {0} warning(s).\n", sWarningCount);
				} else {
					SMSAudioClass.WriteMessage("Completed successfully!\n");
				}
			}
		}

		static void ShowUsage() {
			SMSAudioClass.WriteMessage("USAGE: SMSAudioClass [-help] -errand <errand> [...]\n");
			SMSAudioClass.WriteMessage("\n");
			SMSAudioClass.WriteMessage("OPTIONS:\n");
			SMSAudioClass.WriteMessage("  -help    display help on program or errand\n");
			SMSAudioClass.WriteMessage("\n");
			SMSAudioClass.WriteMessage("ERRANDS:\n");
			SMSAudioClass.WriteMessage("  IBNK    convert banks 'IBNK'\n");
			SMSAudioClass.WriteMessage("  WSYS     convert wave banks 'WSYS'\n");
			SMSAudioClass.WriteMessage("  WAVE     convert audio files\n");
			SMSAudioClass.WriteMessage("  BMS   assemble sequence files\n");
			SMSAudioClass.WriteMessage("  MIDI     convert midi to 	BMS assembly\n");
			SMSAudioClass.WriteMessage("  DataSEQ   extract and import aaf\n");
			SMSAudioClass.Exit(0);
		}

	}

}
