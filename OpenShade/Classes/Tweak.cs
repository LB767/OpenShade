using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenShade.Classes
{
    public enum Category
    {
        Clouds,
        Lighting,
        Atmosphere,
        Water,
        HDR
    };

    public enum ChangeType
    {
        Replace,
        AddAfter,
        AddBefore,
        CommentOut
    }

    public enum UIType {
        Text,
        Checkbox,
        RGB
    }

    public class RGB {
        public double R;
        public double G;
        public double B;

        public RGB(double red, double green, double blue) {
            R = red;
            G = green;
            B = blue;
        }

        public string GetString() {
            string result = R.ToString() + "," + G.ToString() + "," + B.ToString();
            return result;
        }
    }

    public class Parameter {
        public string id;
        public string dataName; // name of the parameter in the .ini file
        public string name; // name of the parameter in the UI
        public string description;
        public string value;
        public string defaultValue;
        public double min;
        public double max;
        public UIType control;

        public Parameter(string DataName, string Name, string Val, string Default, double Min, double Max, UIType Control, string Descr = null) {
            id = Guid.NewGuid().ToString();
            dataName = DataName;
            name = Name;
            description = Descr;
            value = Val;
            defaultValue = Default;
            min = Min;
            max = Max;
            control = Control;
        }

        public Parameter(string DataName, string Name, double Val, double Default, double Min, double Max, UIType Control, string Descr = null)
        {
            id = Guid.NewGuid().ToString();
            dataName = DataName;
            name = Name;
            description = Descr;
            value = Convert.ToDecimal(Val).ToString();
            defaultValue = Convert.ToDecimal(Default).ToString();
            min = Min;
            max = Max;
            control = Control;
        }

        public Parameter(string DataName, string Name, RGB Val, RGB Default, double Min, double Max, UIType Control, string Descr = null)
        {
            id = Guid.NewGuid().ToString();
            dataName = DataName;
            name = Name;
            description = Descr;
            value = Val.GetString();
            defaultValue = Default.GetString();
            min = Min;
            max = Max;
            control = Control;
        }
    }

    public class Tweak : INotifyPropertyChanged
    {
        public Category category { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        private bool _isEnabled;
        public bool isEnabled {
            get { return _isEnabled; }
            set
            {
                _isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("isEnabled"));
            }
        }
        public List<Parameter> parameters { get; set; }

        //public ChangeType tweakType;
        //public string referenceCode;
        //public string newCode;

        public Tweak(Category Cat, string Name, string Descr, bool IsOn, List<Parameter> Params)
        {
            category = Cat;
            name = Name;
            description = Descr;
            isEnabled = IsOn;
            parameters = Params;
        }

        public static void GenerateTweaksData(Dictionary<string, Tweak> tweaks)
        {
            var newTweak = new Tweak(Category.Clouds, "'No popcorn' clouds", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("CloudDistanceFactor", "Distance factor", 0.0000000005, 0.0000000005, 0.0000000001, 0.0000000010, UIType.Text));
            newTweak.parameters.Add(new Parameter("CloudOpacity", "Opacity at far range", 1, 1, 0.1, 1, UIType.Text));
            tweaks.Add("CLOUDS_POPCORN_MODIFICATOR", newTweak);

            newTweak = new Tweak(Category.Clouds, "Alternate lighting for cloud groups", "", false, null);
            tweaks.Add("CLOUDS_CLOUD_ALTERNATE_LIGHTING", newTweak);

            newTweak = new Tweak(Category.Clouds, "Cirrus lighting", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("LightingRatio", "Lighting", 1, 1, 0, 2, UIType.Text));
            newTweak.parameters.Add(new Parameter("SaturateRatio", "Saturation", 1, 1, 0, 2, UIType.Text));
            tweaks.Add("CLOUDS_CIRRUS_LIGHTING", newTweak);

            newTweak = new Tweak(Category.Clouds, "Cloud light scattering", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("ScatteringFactor", "Scattering factor", 0.5, 0.5, 0.1, 3, UIType.Text));
            newTweak.parameters.Add(new Parameter("LightingFactor", "Lighting factor", 0.5, 0.5, 0.01, 2, UIType.Text));
            newTweak.parameters.Add(new Parameter("NoPattern", "Don't use cloud lighting patterns", 0, 0, 0, 1, UIType.Checkbox));
            tweaks.Add("CLOUDS_CLOUD_VOLUME", newTweak);

            newTweak = new Tweak(Category.Clouds, "Cloud lighting tuning", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("CloudLightFactor", "Lighting factor", 0.85, 0.85, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("CloudSaturateFactor", "Saturation factor", 0.33, 0.33, 0.1, 5, UIType.Text));
            tweaks.Add("CLOUDS_CLOUDS_LIGHTING_TUNING", newTweak);

            newTweak = new Tweak(Category.Clouds, "Cloud saturation", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("ShadeFactor", "Saturation", 1, 1, 0, 3, UIType.Text));
            tweaks.Add("CLOUDS_CLOUD_SATURATION", newTweak);

            newTweak = new Tweak(Category.Clouds, "Cloud shadow depth", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("FDepthFactor", "Shadow depth", 0.15, 0.15, 0.01, 0.25, UIType.Text));
            tweaks.Add("CLOUDS_CLOUD_SHADOWS_DEPTH_NEW", newTweak);

            newTweak = new Tweak(Category.Clouds, "Cloud shadow extended size", "", false, null);
            tweaks.Add("CLOUDS_CLOUD_SHADOWS_SIZE", newTweak);

            newTweak = new Tweak(Category.Clouds, "Reduce cloud brightness at dawn/dusk/night", "", false, null);
            tweaks.Add("CLOUDS_CLOUD_BRIGHTNESS_TWILIGHT", newTweak);

            newTweak = new Tweak(Category.Clouds, "Reduce top layer cloud brightness at dawn/dusk/night", "", false, null);
            tweaks.Add("CLOUDS_CIRRUS_BRIGHTNESS_TWILIGHT", newTweak);

            newTweak = new Tweak(Category.Clouds, "Cloud puffs width and height scaling", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("CloudSizeHCoeff", "Horizontal", 0.5, 0.5, 0.3, 1, UIType.Text));
            newTweak.parameters.Add(new Parameter("CloudSizeVCoeff", "Vertical", 0.5, 0.5, 0.3, 1, UIType.Text));
            tweaks.Add("CLOUDS_CLOUD_SIZE", newTweak);

            // -----------------------
            //
            // -----------------------


            newTweak = new Tweak(Category.Lighting, "Aircraft lighting and saturation", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("SunAmbientCoeff", "Ambient sunlight ratio", 1, 1, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("SunDiffuseCoeff", "Diffuse sunlight ratio", 1, 1, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("MoonAmbientCoeff", "Ambient moonlight ratio", 1, 1, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("MoonDiffuseCoeff", "Diffuse moonlight ratio", 1, 1, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("SaturateRatio", "Saturation", 1, 1, 0, 2, UIType.Text));
            newTweak.parameters.Add(new Parameter("VCOnly", "Adjust only internal/virtual cockpit view", 0, 0, 0, 1, UIType.Checkbox));
            tweaks.Add("LIGHTING_AIRCRAFT_LIGHTING", newTweak);

            newTweak = new Tweak(Category.Lighting, "Autogen emissive lighting", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("LightsRatio", "Lights ratio", 1, 1, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("AutogenRatio", "Autogen ratio", 1, 1, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("SmoothTransition", "Smooth day-night transition for lights", 0, 0, 0, 1, UIType.Checkbox));
            tweaks.Add("LIGHTING_AUTOGEN_EMISSIVE", newTweak);

            newTweak = new Tweak(Category.Lighting, "Objects lighting", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("SunAmbientCoeff", "Ambient sunlight ratio", 0.65, 0.65, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("SunDiffuseCoeff", "Diffuse sunlight ratio", 1, 1, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("MoonAmbientCoeff", "Ambient moonlight ratio", 1, 1, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("MoonDiffuseCoeff", "Diffuse moonlight ratio", 1, 1, 0.1, 5, UIType.Text));
            tweaks.Add("LIGHTING_AUTOGEN_LIGHTING", newTweak);

            newTweak = new Tweak(Category.Lighting, "Specular lighting", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("SpecularRatio", "Ratio", 1, 1, 0.1, 4, UIType.Text));
            tweaks.Add("LIGHTING_SPECULAR_LIGHTING", newTweak);

            newTweak = new Tweak(Category.Lighting, "Terrain lighting", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("SunAmbientCoeff", "Ambient sunlight ratio", 0.65, 0.65, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("SunDiffuseCoeff", "Diffuse sunlight ratio", 1, 1, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("MoonAmbientCoeff", "Ambient moonlight ratio", 1, 1, 0.1, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("MoonDiffuseCoeff", "Diffuse moonlight ratio", 1, 1, 0.1, 5, UIType.Text));
            tweaks.Add("LIGHTING_TERRAIN_LIGHTING", newTweak);

            newTweak = new Tweak(Category.Lighting, "Terrain saturation", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("SaturateRatio", "Saturation", 1, 1, 0, 2, UIType.Text));
            tweaks.Add("LIGHTING_TERRAIN_SATURATION", newTweak);

            newTweak = new Tweak(Category.Lighting, "Urban areas lighting at night", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("BoostRatio", "Brightness", 1, 1, 0.1, 15, UIType.Text));
            newTweak.parameters.Add(new Parameter("SaturateRatio", "Saturation", 2, 2, 0.1, 3, UIType.Text));
            tweaks.Add("LIGHTING_BOOST_EMISSIVELANDCLASS", newTweak);

            // -----------------------
            //
            // -----------------------

            newTweak = new Tweak(Category.Atmosphere, "Clouds Fog tuning", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("FogFactor", "Fog factor", 0.5, 0.5, 0.1, 3, UIType.Text));
            tweaks.Add("ATMOSPHERE & FOG_CLOUDS_FOG_TUNING", newTweak);

            newTweak = new Tweak(Category.Atmosphere, "Haze effect", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("Power", "Effect power", 2, 2, 1.01, 7, UIType.Text));
            newTweak.parameters.Add(new Parameter("Distance", "Density factor", 0.00000000035, 0.00000000035, 0.00000000001, 0.00000000200, UIType.Text));
            newTweak.parameters.Add(new Parameter("DensityCorrection", "Density depends on altitude", 1, 1, 0, 1, UIType.Checkbox));
            newTweak.parameters.Add(new Parameter("Red,Green,Blue", "RGB", new RGB(1, 1, 1), new RGB(1, 1, 1), 0.5, 1.5, UIType.RGB));
            tweaks.Add("ATMOSPHERE & FOG_ATMO_HAZE", newTweak);

            newTweak = new Tweak(Category.Atmosphere, "Rayleigh scattering effect", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("Power", "Effect power", 2, 2, 1.01, 7, UIType.Text));
            newTweak.parameters.Add(new Parameter("Density", "Density factor", 0.00000000020, 0.00000000020, 0.00000000001, 0.00000000200, UIType.Text));
            newTweak.parameters.Add(new Parameter("DensityCorrection", "Density depends on altitude", 1, 1, 0, 1, UIType.Checkbox));
            newTweak.parameters.Add(new Parameter("ExcludeClouds", "Exclude clouds", 0, 0, 0, 1, UIType.Checkbox));
            newTweak.parameters.Add(new Parameter("Green", "Green", 0.055, 0.055, 0, 0.5, UIType.Text));
            newTweak.parameters.Add(new Parameter("Blue", "Blue", 0.15, 0.15, 0, 0.5, UIType.Text));
            tweaks.Add("ATMOSPHERE & FOG_RAYLEIGH_SCATTERING", newTweak);

            newTweak = new Tweak(Category.Atmosphere, "Sky Fog tuning", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("FogFactor", "Fog factor", 1, 1, 0.1, 3, UIType.Text));
            tweaks.Add("ATMOSPHERE & FOG_SKY_FOG_TUNING", newTweak);

            newTweak = new Tweak(Category.Atmosphere, "Sky saturation", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("SaturateRatio", "Saturation", 1, 1, 0, 2, UIType.Text));
            tweaks.Add("ATMOSPHERE & FOG_SKY_SATURATION", newTweak);

            // -----------------------
            //
            // -----------------------

            newTweak = new Tweak(Category.Water, "FSX-style reflections", "", false, null);
            tweaks.Add("WATER_FSXREFLECTION", newTweak);

            newTweak = new Tweak(Category.Water, "Water saturation", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("SaturateRatio", "Saturation", 1, 1, 0, 2, UIType.Text));
            tweaks.Add("WATER_WATER_SATURATION", newTweak);

            newTweak = new Tweak(Category.Water, "Water surface tuning", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("ReflectionCoeff", "Reflection coefficient", 0.4, 0.4, 0, 1, UIType.Text));
            newTweak.parameters.Add(new Parameter("RefractionCoeff", "Refraction coefficient (limpidity)", 0.35, 0.35, 0, 1, UIType.Text));
            newTweak.parameters.Add(new Parameter("GranularityCoeff", "Granularity", 3, 3, 0, 5, UIType.Text));
            newTweak.parameters.Add(new Parameter("SpecularBlend", "Specular blend", 1, 1, 0, 3, UIType.Text));
            newTweak.parameters.Add(new Parameter("FresnelAngle", "Water view angle/darkness factor", 4, 4, 0.1, 6, UIType.Text));
            tweaks.Add("WATER_WATERSURFACE", newTweak);

            newTweak = new Tweak(Category.Water, "Wave size", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("SizeRatio", "Scale ratio", 0, 0, 0, 10, UIType.Text));
            newTweak.parameters.Add(new Parameter("SmoothRatio", "Waves smoothing", 0, 0, 0, 10, UIType.Text));
            tweaks.Add("WATER_WAVESIZE", newTweak);

            newTweak = new Tweak(Category.Water, "Wave speed", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("SpeedRatio", "Waves speed factor", 1, 1, 0, 2, UIType.Text));
            tweaks.Add("WATER_WAVESPEED", newTweak);

            // -----------------------
            //
            // -----------------------

            newTweak = new Tweak(Category.HDR, "Alternate tonemap adjustment", "", false, null);
            tweaks.Add("HDR & POST-PROCESSING_HDRTONEMAP", newTweak);

            newTweak = new Tweak(Category.HDR, "Contrast tuning", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("Coeff", "Contrast", 0.5, 0.5, 0, 1, UIType.Text));
            tweaks.Add("HDR & POST-PROCESSING_HDRCONTRAST", newTweak);

            newTweak = new Tweak(Category.HDR, "Scene tone adjustment", "", false, new List<Parameter>() { });
            newTweak.parameters.Add(new Parameter("Red,Green,Blue","RGB", new RGB(1, 1, 1), new RGB(1, 1, 1), 0.5, 1.5, UIType.RGB));
            tweaks.Add("HDR & POST-PROCESSING_HDRTONE", newTweak);

            newTweak = new Tweak(Category.HDR, "Turn off HDR luminance adaptation effect", "", false, null);
            tweaks.Add("HDR & POST-PROCESSING_HDRADAPTATION", newTweak);

        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class CustomTweak : INotifyPropertyChanged
    {
        private string _name;
        public string name // This is fucking awful boilerplate code, I hate it.. getters and setters everywhere, ugh. Just imagine having this on every property...
        {
            get { return _name; }
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("name"));
            }
        }
        public string shaderFile { get; set; }        
        public int index { get; set; }
        public string oldCode { get; set; }
        public string newCode { get; set; }
        public bool isEnabled { get; set; }

        public CustomTweak(string Name, string Shader, int idx, string OldCode, string NewCode, bool IsOn)
        {
            name = Name;
            shaderFile = Shader;
            index = idx;
            oldCode = OldCode;
            newCode = NewCode;
            isEnabled = IsOn;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class PostProcess {
        public string name { get; set; }
        public string description { get; set; }
        public bool isEnabled { get; set; }
        public List<Parameter> parameters { get; set; }

        public PostProcess(string Name, bool IsOn, List<Parameter> Params, string Descr)
        {            
            name = Name;
            description = Descr;       
            isEnabled = IsOn;
            parameters = Params;
        }

        public static void GeneratePostProcessData(Dictionary<string, PostProcess> postProcesses)
        {
            // NOTE: I removed DayNightUse, because I don't know what it actually does
            var newPost = new PostProcess("Sepia", false, new List<Parameter>() { }, "Sepia desaturates and colorizes the image with a specified color. The most obvious application is the traditional old-time photograph effect where the image colors are faded and then overlayed with a yellowish color, but other effects are possible for those seeking a quick two-in-one desaturation/toning effect on the image.");
            newPost.parameters.Add(new Parameter("ColorToneX,ColorToneY,ColorToneZ", "Color Tone", new RGB(1.4, 1.1, 0.9), new RGB(1.4, 1.1, 0.9), 0, 2.55, UIType.RGB, "ColorTone values 0.00 to 2.55 can be thought of as equivalents to 0 to 255.To find a sepia color, open the GIMP color chooser and note the RGB values of the selected color.\r\n\r\nSuppose we want to add a dark yellow. RGB = 173, 171, 59. Add a decimal point two places to the left in each value to produce 1.73, 1.71, and 0.59. Use those values for ColorTone."));
            newPost.parameters.Add(new Parameter("GreyPower", "Grey Power", 0.11, 0.11, 0, 1, UIType.Text, "Desaturates the image this much before tinting.\r\n\r\nIf GreyPower is over 1.00, then the image will appear over - whitened."));
            newPost.parameters.Add(new Parameter("SepiaPower", "Sepia Power", 0.58, 0.58, 0, 1, UIType.Text, "How strong the tint color should be.\r\n\r\nLower values are best used with SepiaPower.Increasing SepiaPower too much causes the image to appear colorized.Values 0.13 to 0.25 are best.Not too much, not too little."));
            postProcesses.Add("POSTPROCESS_SHADER Sepia", newPost);

            newPost = new PostProcess("Curves", false, new List<Parameter>() { }, "");
            newPost.parameters.Add(new Parameter("Curves_mode", "Curves Mode", 0, 0, 0, 0, UIType.Text));
            newPost.parameters.Add(new Parameter("Curves_contrast", "Curves Contrast", 0.65, 0.65, 0, 0, UIType.Text));
            newPost.parameters.Add(new Parameter("Curves_formula", "Curves Formula", 5, 5, 0, 0, UIType.Text));
            postProcesses.Add("POSTPROCESS_SHADER Curves", newPost);

            newPost = new PostProcess("Levels", false, new List<Parameter>() { }, "Used sparingly, Levels will trim off excess whiteness, and it will darken shadows and other dark areas that appear too “washed out” when they should be darker.\r\n\r\nOn the other hand, visual detail is lost when used excessively, and drastic scene changes can be produced.This is either good or bad depending upon the desired effect.In short, Levels is an effect best used for minor touchups to the resulting image.");
            newPost.parameters.Add(new Parameter("Levels_black_point", "Black point", 16, 16, 0, 255, UIType.Text, "Anything below this value to 0 becomes solid black."));
            newPost.parameters.Add(new Parameter("Levels_white_point", "White point", 235, 235, 0, 255, UIType.Text, "Anything above this value to 255 becomes solid white."));
            postProcesses.Add("POSTPROCESS_SHADER Levels", newPost);

            newPost = new PostProcess("Lift Gamma Gain", false, new List<Parameter>() { }, "Lift Gamma Gain effect provides a fine amount of control over how gamma is applied to an image. While the Tonemap effect provides a basic gamma control for basic gamma application, Lift Gamma Gain allows for more precise gamma control over the brightness of shadow areas, midrange areas, and bright areas, and it can do so at the color level with RGB values.");
            newPost.parameters.Add(new Parameter("RGB_LiftX,RGB_LiftY,RGB_LiftZ", "RGB Lift", new RGB(1, 1, 1), new RGB(1, 1, 1), 0, 2, UIType.RGB, "Lowering RGB Lift makes dark areas darker. Raising RGB Lift makes dark areas lighter."));
            newPost.parameters.Add(new Parameter("RGB_GammaX,RGB_GammaY,RGB_GammaZ", "RGB Gamma", new RGB(1, 1, 1), new RGB(1, 1, 1), 0, 2, UIType.RGB, ""));
            newPost.parameters.Add(new Parameter("RGB_GainX,RGB_GainY,RGB_GainZ", "RGB Gain", new RGB(1, 1, 1), new RGB(1, 1, 1), 0, 2, UIType.RGB, "Raising RGB Gain makes light areas lighter. Lowering RGB Gain makes light areas darker."));
            postProcesses.Add("POSTPROCESS_SHADER LiftGammaGain", newPost);

            newPost = new PostProcess("Technicolor", false, new List<Parameter>() { }, "Technicolor attempts to recreate a pseudo-Technicolor effect by modifying the colors of the image enough to emulate the three-strip film process used by movie studios to produce color movies during the 1930s through 1950s.");
            newPost.parameters.Add(new Parameter("TechniAmount", "Techni Amount", 0.4, 0.4, 0, 1, UIType.Text, "Higher = more desaturated, color lessens, image colors appear faded\r\nLower = more color, color increases"));
            newPost.parameters.Add(new Parameter("TechniPower", "Techni Power", 4, 4, 0, 8, UIType.Text, "Higher = Closer to original white levels. 8 = Original whites.\r\nLower = More whites, brighter image"));
            newPost.parameters.Add(new Parameter("redNegativeAmount", "redNegativeAmount", 0.88, 0.88, 0, 1, UIType.Text, "Reducing this value adds more of Red"));
            newPost.parameters.Add(new Parameter("greenNegativeAmount", "Green Negative Amount", 0.88, 0.88, 0, 1, UIType.Text, "Reducing this value adds more of Green"));
            newPost.parameters.Add(new Parameter("blueNegativeAmount", "Blue Negative Amount", 0.88, 0.88, 0, 1, UIType.Text, "Reducing this value adds more of Blue"));
            postProcesses.Add("POSTPROCESS_SHADER Technicolor", newPost);

            newPost = new PostProcess("Vibrance", false, new List<Parameter>() { }, "Vibrance saturates or desaturates the image by a specified color. Vibrance offers more flexibility over which color influences the image.\r\n\r\nVibrance does not remove all colors the way the monochrome effect produces a black and white image, but vibrance will make colors more colorful or less colorful depending upon the values used to adjust the effect.Faded colors can give the image a washed out film effect that other effects can refine.");
            newPost.parameters.Add(new Parameter("Vibrance", "Vibrance", 0.2, 0.2, -1, 1, UIType.Text, "Specifies how much to saturate (+) or desturate (-) the image."));
            newPost.parameters.Add(new Parameter("Vibrance_RGB_balanceX,Vibrance_RGB_balanceY,Vibrance_RGB_balanceZ", "Vibrance RGB Balance", new RGB(1, 1, 1), new RGB(1, 1, 1), -10, 10, UIType.RGB, "Gives priority to a given RGB color. The range for each color channel is -10.00 to 10.00. Each RGB channel value is multiplied by the value of Vibrance to produce the final adjustment that specifies how much to saturate or desaturate that color."));
            postProcesses.Add("POSTPROCESS_SHADER Vibrance", newPost);

            newPost = new PostProcess("Cineon DPX", false, new List<Parameter>() { }, "Cineon DPX setting allows limited post-production image effects that (somewhat) resemble the results obtained with the Cineon System released by Kodak sometime around 1992-1993.");
            newPost.parameters.Add(new Parameter("Red,Green,Blue", "RGB", new RGB(8, 8, 8), new RGB(8, 8, 8), 1, 15, UIType.RGB, ""));
            newPost.parameters.Add(new Parameter("ColorGamma", "Color Gamma", 2.5, 2.5, 0.1, 2.5, UIType.Text, "Adjusts how vibrant the colors should be."));
            newPost.parameters.Add(new Parameter("DPXSaturation", "DPX Saturation", 3, 3, 0, 8, UIType.Text, "Saturates colors."));
            newPost.parameters.Add(new Parameter("RedC,GreenC,BlueC", "RGB C", new RGB(0.36, 0.36, 0.34), new RGB(0.36, 0.36, 0.34), 0.2, 0.6, UIType.RGB, ""));
            newPost.parameters.Add(new Parameter("Blend", "Blend", 0.2, 0.2, 0, 1, UIType.Text, "Blend is how strong the effect should be applied."));
            postProcesses.Add("POSTPROCESS_SHADER DPX", newPost);

            newPost = new PostProcess("Tonemap", false, new List<Parameter>() { }, "Tonemap is an effect that adjusts a variety of related image enhancements that include gamma, saturation, bleach, exposure, and color removal.\r\n\r\nTonemap is a useful “many-in-one” effect. Other effects might offer a greater degree of control over the image, but if only minor modifications are needed by using one effect, then Tonemap has its place.");
            newPost.parameters.Add(new Parameter("Gamma", "Gamma", 1, 1, 0, 2, UIType.Text, "Adjusts gamma. However, this gamma control is limited. If only minor gamma adjustment is needed, then this should be enough, but for more accurate gamma control over the image, set the Tonemap Gamma to 1.000 (neutral) and use the Lift Gamma Gain effect instead."));
            newPost.parameters.Add(new Parameter("Exposure", "Exposure", 0, 0, -1, 1, UIType.Text, "Makes the image brighter. Brightens the darks so more detail is visible in dark areas."));
            newPost.parameters.Add(new Parameter("Saturation", "Saturation", 0, 0, -1, 1, UIType.Text, "Saturates or desaturates colors to make them more or less intense.Use negative values to desaturate colors, and use positive values to saturate colors.Saturation = 0 is the neutral setting and has no effect."));
            newPost.parameters.Add(new Parameter("Bleach", "Bleach", 0, 0, 0, 1, UIType.Text, "Avoid setting bleach above 1. The image becomes darker after that, not lighter. Whites become blacks while blacks remain dark. Small values, such as 0.020, are best."));
            newPost.parameters.Add(new Parameter("Defog", "Defog", 0, 0, 0, 1, UIType.Text, "Defog and FogColor work together. FogColor specifies a color to remove (in decimal RGB format), and Defog specifies how much of that color to remove."));
            newPost.parameters.Add(new Parameter("FogColorX,FogColorY,FogColorZ", "FogColor RGB", new RGB(0, 0, 2.55), new RGB(0, 0, 2.55), 0, 2.55, UIType.RGB, "This operates differently than might be expected because lower values preserve more color. Higher values indicate more of that color to be removed."));
            postProcesses.Add("POSTPROCESS_SHADER Tonemap", newPost);

            newPost = new PostProcess("Luma Sharpen", false, new List<Parameter>() { }, "Lumasharpen sharpens the image to enhance details. The end result is similar to what would be seen after an image has been enhanced using the Unsharp Mask filter in GIMP or Photoshop.");
            newPost.parameters.Add(new Parameter("Sharp_strength", "Sharp Strength", 0.65, 0.65, 0.1, 3, UIType.Text, "Strength of the sharpening"));
            newPost.parameters.Add(new Parameter("Sharp_clamp", "Sharp Clamp", 0.035, 0.035, 0, 0, UIType.Text, "Limits maximum amount of sharpening a pixel recieves"));
            newPost.parameters.Add(new Parameter("Pattern", "Pattern", 2, 2, 1, 4, UIType.Text, "Choose a sample pattern. 1 = Fast, 2 = Normal, 3 = Wider, 4 = Pyramid shaped."));
            newPost.parameters.Add(new Parameter("Offset_bias", "Offset Bias", 1, 1, 0, 6, UIType.Text, "Offset bias adjusts the radius of the sampling pattern."));
            postProcesses.Add("POSTPROCESS_SHADER LumaSharpen", newPost);

        }

    }


}
