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


        public void LoadTweaks(List<Tweak> tweaks, IniFile pref, bool monitorChanges)
        {
            foreach (var tweak in tweaks) {
                bool wasEnabled = tweak.isEnabled;
                if (!pref.KeyExists("IsActive", tweak.key))
                {
                    mainWindowHandle.Log(ErrorType.Warning, "Missing entry 'IsActive' for tweak [" + tweak.key + "]");
                    break;
                }
                tweak.isEnabled = pref.Read("IsActive", tweak.key) == "1" ? true : false;                

                if (wasEnabled != tweak.isEnabled && monitorChanges)
                {
                    tweak.stateChanged = true;
                }
                else {
                    tweak.stateChanged = false;
                }
                                
                foreach (var param in tweak.parameters)
                {
                    param.oldValue = param.value;

                    if (param.control == UIType.RGB)
                    {
                        string dataR = param.dataName.Split(',')[0];
                        string dataG = param.dataName.Split(',')[1];
                        string dataB = param.dataName.Split(',')[2];

                        if (!pref.KeyExists(dataR, tweak.key)) { mainWindowHandle.Log(ErrorType.Warning, "Missing entry '" + dataR + "' for tweak [" + tweak.key + "]"); break; }
                        if (!pref.KeyExists(dataG, tweak.key)) { mainWindowHandle.Log(ErrorType.Warning, "Missing entry '" + dataG + "' for tweak [" + tweak.key + "]"); break; }
                        if (!pref.KeyExists(dataB, tweak.key)) { mainWindowHandle.Log(ErrorType.Warning, "Missing entry '" + dataB + "' for tweak [" + tweak.key + "]"); break; }

                        param.value = pref.Read(dataR, tweak.key) + "," + pref.Read(dataG, tweak.key) + "," + pref.Read(dataB, tweak.key);                                             
                    }
                    else
                    {
                        if (!pref.KeyExists(param.dataName, tweak.key))
                        {
                            mainWindowHandle.Log(ErrorType.Warning, "Missing entry '" + param.dataName + "' for tweak [" + tweak.key + "]");
                            break;
                        }
                        param.value = pref.Read(param.dataName, tweak.key);
                    }

                    if (param.value != param.oldValue && monitorChanges) // TODO: In some cases this evaluates to false because of stuff like "1.0" != "1.00" ... not sure what's the best thing to do
                    { 
                        param.hasChanged = true;
                    }
                    else {
                        param.oldValue = param.value;
                        param.hasChanged = false;
                    }
                }                
            }           
        }

        public void LoadCustomTweaks(ObservableCollection<CustomTweak> customTweaks, IniFile pref, bool monitorChanges)
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

        public void LoadPostProcesses(List<PostProcess> postProcesses, IniFile pref, bool monitorChanges)
        {
            foreach (var post in postProcesses)
            {
                bool wasEnabled = post.isEnabled;
                if (!pref.KeyExists("IsActive", post.key))
                {
                    mainWindowHandle.Log(ErrorType.Warning, "Missing entry 'IsActive' for post-process [" + post.key + "]");
                    break;
                }
                post.isEnabled = pref.Read("IsActive", post.key) == "1" ? true : false;

                if (wasEnabled != post.isEnabled && monitorChanges)
                {
                    post.stateChanged = true;
                }
                else {
                    post.stateChanged = false;
                }

                post.index = int.Parse(pref.Read("Index", post.key));

                string rawParams = pref.Read("Params", post.key).FromHexString();
                string[] lines = rawParams.Split(new string[] { "\r\n" }, StringSplitOptions.None);

                foreach (var param in post.parameters)
                {
                    param.oldValue = param.value;

                    if (param.control == UIType.RGB)
                    {
                        string dataR = param.dataName.Split(',')[0];
                        string dataG = param.dataName.Split(',')[1];
                        string dataB = param.dataName.Split(',')[2];

                        string identifiedLineR = lines.FirstOrDefault(p => p.StartsWith(dataR));
                        string identifiedLineG = lines.FirstOrDefault(p => p.StartsWith(dataG));
                        string identifiedLineB = lines.FirstOrDefault(p => p.StartsWith(dataB));

                        if (identifiedLineR == null) { mainWindowHandle.Log(ErrorType.Warning, "Missing entry '" + dataR + "' for post-process [" + post.key + "]"); break; }
                        if (identifiedLineG == null) { mainWindowHandle.Log(ErrorType.Warning, "Missing entry '" + dataG + "' for post-process [" + post.key + "]"); break; }
                        if (identifiedLineB == null) { mainWindowHandle.Log(ErrorType.Warning, "Missing entry '" + dataB + "' for post-process [" + post.key + "]"); break; }

                        param.value = identifiedLineR.Split('=')[1] + "," + identifiedLineG.Split('=')[1] + "," + identifiedLineB.Split('=')[1];
                    }
                    else
                    {
                        string identifiedLine = lines.FirstOrDefault(p => p.StartsWith(param.dataName));
                        if (identifiedLine == null) { mainWindowHandle.Log(ErrorType.Warning, "Missing entry '" + param.dataName + "' for post-process [" + post.key + "]"); break; }
                        param.value = identifiedLine.Split('=')[1];
                    }

                    if (param.value != param.oldValue && monitorChanges)
                    {
                        param.hasChanged = true;
                    }
                    else {
                        param.oldValue = param.value; // put the current value as old value, for instance when we load the first preset
                        param.hasChanged = false;
                    }
                }                
            }

            // Reorder the list based on process index
            postProcesses.Sort((x, y) => x.index.CompareTo(y.index));
        }

        public string LoadComments(IniFile pref) {
            if (pref.KeyExists("Comment", "PRESET COMMENTS")) {
                string rawComment = pref.Read("Comment", "PRESET COMMENTS");
                string result = rawComment.Replace("~^#", "\r\n");
                return result;
            }
            return "";            
        }

        public void SavePreset(List<Tweak> tweaks, ObservableCollection<CustomTweak> customTweaks, List<PostProcess> postProcesses, string comment, IniFile preset)
        {
            // Standard tweaks    

            foreach (var tweak in tweaks) {
                preset.Write("IsActive", tweak.isEnabled ? "1" : "0", tweak.key);

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

                        preset.Write(dataR, valueR, tweak.key);
                        preset.Write(dataG, valueG, tweak.key);
                        preset.Write(dataB, valueB, tweak.key);
                    }
                    else
                    {
                        preset.Write(param.dataName, param.value, tweak.key);
                    }
                }                
            }       

            // Custom tweaks

            foreach (var custom in customTweaks)
            {
                preset.Write("IsActive", custom.isEnabled ? "1" : "0", custom.key);
                preset.Write("Name", custom.name, custom.key);
                preset.Write("Shader", custom.shaderFile, custom.key);
                preset.Write("Index", custom.index.ToString(), custom.key);
                preset.Write("OldPattern", custom.oldCode.ToHexString(), custom.key);
                preset.Write("NewPattern", custom.newCode.ToHexString(), custom.key);                
            }

            // Post-Process
            
            foreach (var post in postProcesses)
            {
                preset.Write("IsActive", post.isEnabled ? "1" : "0", post.key);
                preset.Write("Index", post.index.ToString(), post.key);

                string finalString = "";

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

                    preset.Write("Params", finalString.ToHexString(), post.key);
                }                
            }

            // Comment
            preset.Write("Comment", comment.Replace("\r\n", "~^#"), "PRESET COMMENTS");
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
                            mainWindowHandle.activePresetPath = parts[1].Trim();
                            mainWindowHandle.loadedPresetPath = mainWindowHandle.activePresetPath;
                            mainWindowHandle.LoadedPreset_Label.Content = Path.GetFileNameWithoutExtension(mainWindowHandle.loadedPresetPath); // TODO: Replace with proper binding
                            mainWindowHandle.ActivePreset_Label.Content = Path.GetFileNameWithoutExtension(mainWindowHandle.activePresetPath);
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

            if (mainWindowHandle.activePresetPath != null)
            {
                lines.Add("Preset, " + mainWindowHandle.activePresetPath);
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
