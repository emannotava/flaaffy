
using System;
using System.Linq;

namespace arookas {

	static partial class SMSAudioClass {

		public static aCommandLineParameter GetLastCmdParam(aCommandLine cmdline, string name) {
			return cmdline.LastOrDefault(param => param.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
		}

	}

}
