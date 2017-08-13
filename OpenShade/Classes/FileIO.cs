using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OpenShade.Classes
{
    class FileIO
    {
        MainWindow mainWindowHandle;

        public const string settingsFile = "config.txt";

        public const string cloudFile = "Cloud.fx";
        public const string generalFile = "General.fx";
        public const string terrainFile = "GPUTerrain.fx";
        public const string funclibFile = "FuncLibrary.fxh";
        public const string terrainFXHFile = "GPUTerrain.fxh";
        public const string shadowFile = "Shadow.fxh";
        public const string HDRFile = "HDR.hlsl";

        public FileIO(MainWindow handle)
        {
            mainWindowHandle = handle;
        }

        public bool LoadShaderFiles(string dir)
        {
            try
            {
                MainWindow.cloudText = File.ReadAllText(dir + cloudFile);
                MainWindow.generalText = File.ReadAllText(dir + generalFile);
                MainWindow.terrainText = File.ReadAllText(dir + terrainFile);
                MainWindow.funclibText = File.ReadAllText(dir + funclibFile);
                MainWindow.terrainFXHText = File.ReadAllText(dir + terrainFXHFile);
                MainWindow.shadowText = File.ReadAllText(dir + shadowFile);
                MainWindow.HDRText = File.ReadAllText(dir + HDRFile);

                return true;

            }
            catch (Exception ex)
            {
                mainWindowHandle.Log(ErrorType.Error, ex.Message);
                return false;
            }
        }


        public bool CopyShaderFiles(string origin, string destination)
        {
            try
            {
                File.Copy(origin + cloudFile, destination + cloudFile, true);
                File.Copy(origin + generalFile, destination + generalFile, true);
                File.Copy(origin + terrainFile, destination + terrainFile, true);
                File.Copy(origin + funclibFile, destination + funclibFile, true);
                File.Copy(origin + terrainFXHFile, destination + terrainFXHFile, true);
                File.Copy(origin + shadowFile, destination + shadowFile, true);

                if (origin.Contains("ShadersHLSL"))
                {
                    origin += "PostProcess\\";
                }
                else
                {
                    destination += "PostProcess\\";
                }
                File.Copy(origin + HDRFile, destination + HDRFile, true);

                return true;
            }
            catch (Exception ex)
            {
                mainWindowHandle.Log(ErrorType.Error, ex.Message);
                return false;
            }
        }

        public void ClearDirectory(string dir)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dir);

            foreach (FileInfo file in dirInfo.GetFiles())
            {
                file.Delete();
            }
        }


        public void LoadTweaks(Dictionary<string, Tweak> tweaks, IniFile pref)
        {
            foreach (var tweak in tweaks) {
                tweak.Value.isEnabled = pref.Read("IsActive", tweak.Key) == "1" ? true : false;

                if (tweak.Value.parameters != null) // NOTE: Do this for now, maybe it'd be best to NOT have null lists but only empty ones, would need to change initialization!
                {  
                    foreach (var param in tweak.Value.parameters)
                    {
                        if (param.control == UIType.RGB)
                        {
                            string dataR = param.dataName.Split(',')[0];
                            string dataG = param.dataName.Split(',')[1];
                            string dataB = param.dataName.Split(',')[2];

                            param.value = pref.Read(dataR, tweak.Key) + "," + pref.Read(dataG, tweak.Key) + "," + pref.Read(dataB, tweak.Key);
                        }
                        else
                        {
                            param.value = pref.Read(param.dataName, tweak.Key);
                        }
                    }
                }
            }           
        }

        public void LoadCustomTweaks(Dictionary<string, CustomTweak> customTweaks, IniFile pref)
        {
            customTweaks.Clear();
            int count = 0;
            string section = "CUSTOM_TWEAK" + count.ToString();
            bool customExists = pref.KeyExists("isActive", section);

            while (customExists)
            {
                var newTweak = new CustomTweak(pref.Read("Name", section),
                    pref.Read("Shader", section),
                    int.Parse(pref.Read("Index", section)),
                    pref.Read("OldPattern", section).FromHexString(),
                    pref.Read("NewPattern", section).FromHexString(),
                    pref.Read("IsActive", section) == "1" ? true : false);

                customTweaks.Add(section, newTweak);

                count++;
                section = "CUSTOM_TWEAK" + count.ToString();
                customExists = pref.KeyExists("isActive", section);
            }

        }

        public void LoadPostProcesses(Dictionary<string, PostProcess> postProcesses, IniFile pref)
        {
            foreach (var post in postProcesses) {
                post.Value.isEnabled = pref.Read("IsActive", post.Key) == "1" ? true : false;

                string rawParams = pref.Read("Params", post.Key).FromHexString();
                string[] lines = rawParams.Split(new string[] { "\r\n" }, StringSplitOptions.None);

                if (post.Value.parameters != null)
                {
                    foreach (var param in post.Value.parameters)
                    {
                        if (param.control == UIType.RGB)
                        {
                            string dataR = param.dataName.Split(',')[0];
                            string dataG = param.dataName.Split(',')[1];
                            string dataB = param.dataName.Split(',')[2];

                            string identifiedLineR = lines.First(p => p.StartsWith(dataR));
                            string identifiedLineG = lines.First(p => p.StartsWith(dataG));
                            string identifiedLineB = lines.First(p => p.StartsWith(dataB));

                            param.value = identifiedLineR.Split('=')[1] + "," + identifiedLineG.Split('=')[1] + "," + identifiedLineB.Split('=')[1];
                        }
                        else
                        {
                            string identifiedLine = lines.First(p => p.StartsWith(param.dataName));
                            param.value = identifiedLine.Split('=')[1];
                        }
                    }
                }
            }           
        }

        public string LoadComments(IniFile pref) {
            string rawComment = pref.Read("Comment", "PRESET COMMENTS");
            string result = rawComment.Replace("~^#", "\r\n");
            return result;
        }

        public void SavePreset(string filepath, Dictionary<string, Tweak> tweaks, Dictionary<string, CustomTweak> customTweaks, Dictionary<string, PostProcess> postProcesses, string comment, IniFile pref)
        {
            // Standard tweaks    

            foreach (var tweak in tweaks) {
                pref.Write("IsActive", tweak.Value.isEnabled ? "1" : "0", tweak.Key);

                if (tweak.Value.parameters != null)
                {
                    foreach (var param in tweak.Value.parameters)
                    {
                        if (param.control == UIType.RGB)
                        {
                            string dataR = param.dataName.Split(',')[0];
                            string dataG = param.dataName.Split(',')[1];
                            string dataB = param.dataName.Split(',')[2];

                            string valueR = param.value.Split(',')[0];
                            string valueG = param.value.Split(',')[1];
                            string valueB = param.value.Split(',')[2];

                            pref.Write(dataR, valueR, tweak.Key);
                            pref.Write(dataG, valueG, tweak.Key);
                            pref.Write(dataB, valueB, tweak.Key);
                        }
                        else
                        {
                            pref.Write(param.dataName, param.value, tweak.Key);
                        }
                    }
                }
            }       

            // Custom tweaks

            foreach (var custom in customTweaks)
            {
                pref.Write("IsActive", custom.Value.isEnabled ? "1" : "0", custom.Key);
                pref.Write("Name", custom.Value.name, custom.Key);
                pref.Write("Shader", custom.Value.shaderFile, custom.Key);
                pref.Write("Index", custom.Value.index.ToString(), custom.Key);
                pref.Write("OldPattern", custom.Value.oldCode.ToHexString(), custom.Key);
                pref.Write("NewPattern", custom.Value.newCode.ToHexString(), custom.Key);                
            }

            // Post-Process
            
            foreach (var post in postProcesses) {
                pref.Write("IsActive", post.Value.isEnabled ? "1" : "0", post.Key);

                string finalString = "";

                if (post.Value.parameters != null)
                {
                    foreach (var param in post.Value.parameters)
                    {
                        if (param.control == UIType.RGB)
                        {
                            string dataR = param.dataName.Split(',')[0];
                            string dataG = param.dataName.Split(',')[1];
                            string dataB = param.dataName.Split(',')[2];

                            string valueR = param.value.Split(',')[0];
                            string valueG = param.value.Split(',')[1];
                            string valueB = param.value.Split(',')[2];

                            finalString += dataR + "=" + valueR + "\r\n";
                            finalString += dataG + "=" + valueG + "\r\n";
                            finalString += dataB + "=" + valueB + "\r\n";
                        }
                        else
                        {
                            finalString += param.dataName + "=" + param.value + "\r\n";
                        }

                        pref.Write("Params", finalString.ToHexString(), post.Key);
                    }
                }
            }

            // Comment

            pref.Write("Comment", comment.Replace("\r\n", "~^#"), "PRESET COMMENTS");
        }

        public void LoadSettings(string filepath)
        {

            // TODO: Ignore comments and blank lines
            // TODO: Error checking

            var lines = File.ReadAllLines(filepath);

            for (int i = 0; i < lines.Count(); i++)
            {
                List<string> parts = lines[i].Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                if (parts.Count() == 1)
                {
                    mainWindowHandle.Log(ErrorType.Warning, $"Missing data in config.txt. Check line {(i + 1).ToString()} contains {{key}}, {{value}}");
                }
                else if (parts.Count() == 2)
                {
                    switch (parts[0])
                    {
                        case "Preset":
                            mainWindowHandle.presetPath = parts[1].Trim();
                            mainWindowHandle.presetName = Path.GetFileNameWithoutExtension(parts[1].Trim());
                            break;

                        case "Theme":
                            Themes current;
                            if (Enum.TryParse(parts[1], out current))
                            {
                                ((App)Application.Current).ChangeTheme(current);
                            }
                            break;

                        case "Main_Width":
                            mainWindowHandle.Width = double.Parse(parts[1].Trim());
                            break;

                        case "Main_Height":
                            mainWindowHandle.Height = double.Parse(parts[1].Trim());
                            break;
                    }
                }
                else
                {
                    mainWindowHandle.Log(ErrorType.Warning, $"Too much data in config.txt. Check line {(i + 1).ToString()} contains only {{key}}, {{value}}");
                }
            }
        }

        public void SaveSettings(string filepath)
        {
            List<string> lines = new List<string>();

            if (mainWindowHandle.presetPath != null) lines.Add("Preset, " + mainWindowHandle.presetPath);
            lines.Add("Theme, " + ((App)Application.Current).CurrentTheme.ToString());
            lines.Add("Main_Width, " + mainWindowHandle.Width.ToString());
            lines.Add("Main_Height, " + mainWindowHandle.Height.ToString());

            try
            {
                File.WriteAllLines(filepath, lines);
                mainWindowHandle.Log(ErrorType.None, "Data saved in settings.txt");
            }
            catch
            {
                mainWindowHandle.Log(ErrorType.Error, "Could not save data in settings.txt");
            }

        }
    }
}
