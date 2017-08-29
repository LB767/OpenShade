# OpenShade
OpenShade is a C#/WPF tool to modify HLSL shader files in Prepar3D v4.

## Getting started
To use OpenShade, simply download the latest release [here](https://github.com/LB767/OpenShade/releases).

The software is still in active development, so expect bugs and feel free to report them here.

## FAQ
- Is this PTA for v4?

No. OpenShade comes with the same base tweaks as the old free version of PTA and uses the same preset file format, simply to retain cross compatibility as long as possible.
Any changes that were made from PTA v3 to v4 are not present in OpenShade and with time both softwares will evolve each in their own direction.

The official PTA for v4 app can be found over at [SimTweaks](https://simtweaks.com/)

- Can I use the latest PTA v4 preset files with this?

Yes. The file format is the same, for now. But bear in mind that not all tweaks may work correctly since OpenShade is based on an older version of PTA.

As an example, using the [THOPAT 2.1 preset](https://www.sass-projects.info/thopat/) the following tweaks are not supported:    
  → Cloud shadow depth  
  → HDR off with post-processing on (not used by the preset)  
  → FXAA tweaks (not used by the preset, FXAA is one of the worst AA method available. SMAA might be added in the future)  
  → Older DPX post-process (not used by the preset)  
  → Any .cfg tweak

- The program does not launch

Make sure you have Microsoft .Net Framework 4.5.2 or later installed on your machine.

## Acknowledgments
Massive thanks to Yuri (KNOSSOS) for creating the original program, only someone with great graphics rendering knowledge could come up with such a tool, and these people are rare!

Matt Davies and the SimTweaks crew for investing in PTA and porting in over to P3D v4 from the get-go.
