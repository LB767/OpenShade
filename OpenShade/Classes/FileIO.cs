using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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

        public bool CheckShaderBackup(string dir) {
            if (File.Exists(dir + cloudFile) == false) { return false; }
            if (File.Exists(dir + generalFile) == false) { return false; }
            if (File.Exists(dir + terrainFile) == false) { return false; }
            if (File.Exists(dir + funclibFile) == false) { return false; }
            if (File.Exists(dir + terrainFXHFile) == false) { return false; }
            if (File.Exists(dir + shadowFile) == false) { return false; }
            if (File.Exists(dir + HDRFile) == false) { return false; }

            return true;
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

        public bool ClearDirectory(string dir)
        {
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(dir);

                foreach (FileInfo file in dirInfo.GetFiles())
                {
                    file.Delete();
                }
                return true;
            }
            catch (Exception ex) {
                mainWindowHandle.Log(ErrorType.Error, ex.Message);
                return false;
            }
        }


        public void LoadTweaks(List<Tweak> tweaks, IniFile pref)
        {
            foreach (var tweak in tweaks) {
                tweak.isEnabled = pref.Read("IsActive", tweak.key) == "1" ? true : false;

                if (tweak.parameters != null) // NOTE: Do this for now, maybe it'd be best to NOT have null lists but only empty ones, would need to change initialization!
                {  
                    foreach (var param in tweak.parameters)
                    {
                        string oldValue = param.value;

                        if (param.control == UIType.RGB)
                        {
                            string dataR = param.dataName.Split(',')[0];
                            string dataG = param.dataName.Split(',')[1];
                            string dataB = param.dataName.Split(',')[2];

                            param.value = pref.Read(dataR, tweak.key) + "," + pref.Read(dataG, tweak.key) + "," + pref.Read(dataB, tweak.key);
                        }
                        else
                        {
                            param.value = pref.Read(param.dataName, tweak.key);
                        }

                        if (param.value != oldValue) {
                            param.hasChanged = true;
                        }
                    }
                }
            }           
        }

        public void LoadCustomTweaks(ObservableCollection<CustomTweak> customTweaks, IniFile pref)
        {
            // TODO: Find a decent way to check if there are changes between the new loaded custom tweaks and the old ones

            customTweaks.Clear();
            int count = 0;
            string section = "CUSTOM_TWEAK" + count.ToString();
            bool customExists = pref.KeyExists("isActive", section);

            while (customExists)
            {
                var newTweak = new CustomTweak(section, 
                    pref.Read("Name", section),
                    Path.GetFileName(pref.Read("Shader", section)), // to remove Post-Process// directory for HDR file
                    int.Parse(pref.Read("Index", section)),
                    pref.Read("OldPattern", section).FromHexString(),
                    pref.Read("NewPattern", section).FromHexString(),
                    pref.Read("IsActive", section) == "1" ? true : false);

                customTweaks.Add(newTweak);

                count++;
                section = "CUSTOM_TWEAK" + count.ToString();
                customExists = pref.KeyExists("isActive", section);
            }

        }

        public void LoadPostProcesses(List<PostProcess> postProcesses, IniFile pref)
        {
            foreach (var post in postProcesses) {
                post.isEnabled = pref.Read("IsActive", post.key) == "1" ? true : false;

                post.index = int.Parse(pref.Read("Index", post.key));

                string rawParams = pref.Read("Params", post.key).FromHexString();
                string[] lines = rawParams.Split(new string[] { "\r\n" }, StringSplitOptions.None);

                if (post.parameters != null)
                {
                    foreach (var param in post.parameters)
                    {
                        string oldValue = param.value;

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

                        if (param.value != oldValue) {
                            param.hasChanged = true;
                        }
                    }
                }
            }

            // Reorder the list based on process index
            postProcesses.Sort((x, y) => x.index.CompareTo(y.index));
        }

        public string LoadComments(IniFile pref) {
            string rawComment = pref.Read("Comment", "PRESET COMMENTS");
            string result = rawComment.Replace("~^#", "\r\n");
            return result;
        }

        public void SavePreset(string filepath, List<Tweak> tweaks, ObservableCollection<CustomTweak> customTweaks, List<PostProcess> postProcesses, string comment, IniFile pref)
        {
            // Standard tweaks    

            foreach (var tweak in tweaks) {
                pref.Write("IsActive", tweak.isEnabled ? "1" : "0", tweak.key);

                if (tweak.parameters != null)
                {
                    foreach (var param in tweak.parameters)
                    {
                        if (param.control == UIType.RGB)
                        {
                            string dataR = param.dataName.Split(',')[0];
                            string dataG = param.dataName.Split(',')[1];
                            string dataB = param.dataName.Split(',')[2];

                            string valueR = param.value.Split(',')[0];
                            string valueG = param.value.Split(',')[1];
                            string valueB = param.value.Split(',')[2];

                            pref.Write(dataR, valueR, tweak.key);
                            pref.Write(dataG, valueG, tweak.key);
                            pref.Write(dataB, valueB, tweak.key);
                        }
                        else
                        {
                            pref.Write(param.dataName, param.value, tweak.key);
                        }
                    }
                }
            }       

            // Custom tweaks

            foreach (var custom in customTweaks)
            {
                pref.Write("IsActive", custom.isEnabled ? "1" : "0", custom.key);
                pref.Write("Name", custom.name, custom.key);
                pref.Write("Shader", custom.shaderFile, custom.key);
                pref.Write("Index", custom.index.ToString(), custom.key);
                pref.Write("OldPattern", custom.oldCode.ToHexString(), custom.key);
                pref.Write("NewPattern", custom.newCode.ToHexString(), custom.key);                
            }

            // Post-Process
            
            foreach (var post in postProcesses)
            {
                pref.Write("IsActive", post.isEnabled ? "1" : "0", post.key);
                pref.Write("Index", post.index.ToString(), post.key);

                string finalString = "";

                if (post.parameters != null)
                {
                    foreach (var param in post.parameters)
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

                        pref.Write("Params", finalString.ToHexString(), post.key);
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

                        case "Backup_Directory":
                            mainWindowHandle.backupDirectory = parts[1].Trim();
                            break;

                        case "Main_Width":
                            mainWindowHandle.Width = double.Parse(parts[1].Trim());
                            break;

                        case "Main_Height":
                            mainWindowHandle.Height = double.Parse(parts[1].Trim());
                            break;

                        case "Col1_Width":
                            mainWindowHandle.Tweaks_Grid.ColumnDefinitions[0].Width = new GridLength(double.Parse(parts[1].Trim()));
                            break;
                        case "Col2_Width":
                            mainWindowHandle.Post_Grid.ColumnDefinitions[0].Width = new GridLength(double.Parse(parts[1].Trim()));
                            break;
                        case "Col3_Width":
                            mainWindowHandle.Custom_Grid.ColumnDefinitions[0].Width = new GridLength(double.Parse(parts[1].Trim()));
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

            if (mainWindowHandle.presetPath != null)
            {
                lines.Add("Preset, " + mainWindowHandle.presetPath);
                lines.Add("Theme, " + ((App)Application.Current).CurrentTheme.ToString());
                lines.Add("Backup_Directory, " + mainWindowHandle.backupDirectory);
                lines.Add("Main_Width, " + mainWindowHandle.Width.ToString());
                lines.Add("Main_Height, " + mainWindowHandle.Height.ToString());
                lines.Add("Col1_Width, " + mainWindowHandle.Tweaks_Grid.ColumnDefinitions[0].Width.ToString());
                lines.Add("Col2_Width, " + mainWindowHandle.Post_Grid.ColumnDefinitions[0].Width.ToString());
                lines.Add("Col3_Width, " + mainWindowHandle.Custom_Grid.ColumnDefinitions[0].Width.ToString());
            }

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
