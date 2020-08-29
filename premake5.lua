
workspace "flaaffy"
configurations { "Debug", "Release" }
targetdir "bin/%{cfg.buildcfg}"
startproject "SMSAudioClass"

filter "configurations:Debug"
defines { "DEBUG" }
symbols "on"

filter "configurations:Release"
defines { "RELEASE" }
optimize "On"

project "SMSAudioTool"
kind "ConsoleApp"
language "C#"
namespace "arookas"
location "SMSAudioTool"
entrypoint "arookas.SMSAudioTool"
targetname "SMSAudioTool"
framework "4.6.1"

links {
	"arookas",
	"System",
	"System.Core",
	"System.Xml",
	"System.Xml.Linq",
}

files {
	"SMSAudioClass/**.cs",
}

excludes {
	"SMSAudioClass/bin/**",
	"SMSAudioClass/obj/**",
}
