using Microsoft.Win32;
using OpenShade.Controls;
using OpenShade.Classes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Diagnostics;

namespace OpenShade
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public enum ErrorType { None, Warning, Error };


    public partial class MainWindow : Window
    {
        // @VERY_IMPORTANT TODO: Find a way to make sure the backup shader files are actually the default ones!!!
        // Otherwise the program WILL NOT work. Period.

        Dictionary<string, Tweak> tweaks;
        Dictionary<string, CustomTweak> customTweaks;
        Dictionary<string, PostProcess> postProcesses;   

        const string P3DRegistryPath = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Lockheed Martin\\Prepar3D v4";
        string cacheDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Lockheed Martin\\Prepar3D v4\\Shaders\\";
        string currentDirectory = Directory.GetCurrentDirectory();

        FileIO fileData;
        string shaderDirectory;
        string backupDirectory;

        public string presetPath;
        public string presetName;

        // TODO: put this in a struct somewhere
        public static string cloudText, generalText, terrainText, funclibText, terrainFXHText, shadowText, HDRText;

        public MainWindow()
        {
            InitializeComponent();

            Log_RichTextBox.Document.Blocks.Clear();

            tweaks = new Dictionary<string, Tweak>() { };
            customTweaks = new Dictionary<string, CustomTweak>() { };
            postProcesses = new Dictionary<string, PostProcess>() { };

            Tweak.GenerateTweaksData(tweaks);
            PostProcess.GeneratePostProcessData(postProcesses);

            Tweak_List.ItemsSource = tweaks.Values;
            CustomTweak_List.ItemsSource = customTweaks.Values;
            PostProcess_List.ItemsSource = postProcesses.Values;

            CollectionView tweaksView = (CollectionView)CollectionViewSource.GetDefaultView(Tweak_List.ItemsSource);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("category");
            tweaksView.GroupDescriptions.Add(groupDescription);           

            // Shaders files
            string P3DDirectory = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Lockheed Martin\Prepar3D v4", "AppPath", null);
            if (P3DDirectory == null)
            {
                Log(ErrorType.Error, "Prepar3D v4 path not found");
                return;
            }

            int index = P3DDirectory.IndexOf('\0');
            if (index >= 0) { P3DDirectory = P3DDirectory.Substring(0, index); }

            P3DMain_TextBox.Text = P3DDirectory;
            P3DMain_TextBox.IsEnabled = false;

            shaderDirectory = P3DDirectory + "ShadersHLSL\\";

            if (!Directory.Exists(shaderDirectory))
            {
                Log(ErrorType.Error, "P3D shader directory not found");
                return;
            }
            P3DShaders_TextBox.Text = shaderDirectory;
            P3DShaders_TextBox.IsEnabled = false;

            if (!Directory.Exists(cacheDirectory))
            {
                Log(ErrorType.Error, "Shader cache directory not found");
                return;
            }
            ShaderCache_TextBox.Text = cacheDirectory;
            ShaderCache_TextBox.IsEnabled = false;

            fileData = new FileIO(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(currentDirectory + "\\" + FileIO.settingsFile))
            {
                fileData.LoadSettings(currentDirectory + "\\" + FileIO.settingsFile);
            }

            Theme_ComboBox.ItemsSource = Enum.GetValues(typeof(Themes)).Cast<Themes>();
            Theme_ComboBox.SelectedItem = ((App)Application.Current).CurrentTheme;

            backupDirectory = currentDirectory + "\\Backup Shaders\\";

            if (!Directory.Exists(backupDirectory))
            {
                if (Directory.Exists(shaderDirectory))
                {
                    MessageBoxResult result = MessageBox.Show("OpenShade will backup your Prepar3D shaders now.\r\n Make sure the files are the original ones!", "Backup", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation, MessageBoxResult.OK);
                    if (result == MessageBoxResult.OK)
                    {
                        Directory.CreateDirectory("Backup Shaders");
                        if (fileData.CopyShaderFiles(shaderDirectory, backupDirectory))
                        {
                            Log(ErrorType.None, "Shaders backed up");
                        }
                    }
                    else {
                        Log(ErrorType.Warning, "Shaders were not backed up. OpenShade can not run.");
                        NewPreset_btn.IsEnabled = false;
                        OpenPreset_btn.IsEnabled = false;
                        SavePreset_btn.IsEnabled = false;
                        SavePresetAs_btn.IsEnabled = false;
                        ApplyPreset_btn.IsEnabled = false;
                        ResetShaderFiles_btn.IsEnabled = false;
                        ClearShaders_btn.IsEnabled = false;

                        return;
                    }
                }
            }
            ShaderBackup_TextBox.Text = backupDirectory;
            ShaderBackup_TextBox.IsEnabled = false;

            fileData.LoadShaderFiles(backupDirectory);

            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.cloudFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.generalFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.terrainFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.funclibFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.terrainFXHFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.shadowFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.HDRFile);

            if (presetPath != null)
            {
                if (File.Exists(presetPath))
                {
                    LoadPreset();
                }
                else {
                    Log(ErrorType.Error, "Active Preset file [" + presetName + "] not found");
                }
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            fileData.SaveSettings(currentDirectory + "\\" + FileIO.settingsFile);
        }


        #region MainTweaks
        private void TweakList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Tweak_List.SelectedItem != null)
            {
                ParameterStack.Children.Clear();
                TweakClearStack.Children.Clear();

                Tweak selectedTweak = (Tweak)Tweak_List.SelectedItem;
                TweakTitleTextblock.Content = selectedTweak.name;
                TweakDescriptionTextblock.Text = selectedTweak.description;

                if (selectedTweak.parameters != null)
                {
                    ParameterStack.Rows = selectedTweak.parameters.Count();

                    Button resetButton = new Button();
                    resetButton.Content = "Reset";
                    resetButton.ToolTip = "Reset parameters to their default value";
                    resetButton.Width = 50;
                    resetButton.Height = 25;
                    resetButton.VerticalAlignment = VerticalAlignment.Top;
                    resetButton.HorizontalAlignment = HorizontalAlignment.Right;
                    resetButton.Click += new RoutedEventHandler(ResetParameters_Click);

                    Grid.SetColumn(resetButton, 1);
                    TweakClearStack.Children.Add(resetButton);                    

                    foreach (Parameter param in selectedTweak.parameters)
                    {

                        TextBlock txtBlock = new TextBlock();
                        txtBlock.Text = param.name;
                        txtBlock.TextWrapping = TextWrapping.Wrap;
                        txtBlock.Width = 170;
                        txtBlock.Height = 30;
                        txtBlock.Margin = new Thickness(0, 0, 10, 0);

                        ParameterStack.Children.Add(txtBlock);

                        if (param.control == UIType.Checkbox)
                        {
                            CheckBox checkbox = new CheckBox();
                            checkbox.IsChecked = ((param.value == "1") ? true : false);
                            checkbox.Uid = param.id;
                            checkbox.VerticalAlignment = VerticalAlignment.Center;
                            checkbox.Click += new RoutedEventHandler(Checkbox_Click);
                            ParameterStack.Children.Add(checkbox);
                        }
                        else if (param.control == UIType.RGB)
                        {
                            var group = new GroupBox();
                            group.Header = "RGB";

                            var container = new StackPanel();
                            container.Orientation = Orientation.Horizontal;

                            var Rtext = new TextBox();
                            Rtext.Uid = param.id + "_R";
                            Rtext.Height = 25;
                            Rtext.Width = 50;
                            Rtext.Text = param.value.Split(',')[0];
                            Rtext.LostFocus += new RoutedEventHandler(RGB_LostFocus);

                            var Gtext = new TextBox();
                            Gtext.Uid = param.id + "_G";
                            Gtext.Height = 25;
                            Gtext.Width = 50;
                            Gtext.Text = param.value.Split(',')[1];
                            Gtext.LostFocus += new RoutedEventHandler(RGB_LostFocus);

                            var Btext = new TextBox();
                            Btext.Uid = param.id + "_B";
                            Btext.Height = 25;
                            Btext.Width = 50;
                            Btext.Text = param.value.Split(',')[2];
                            Btext.LostFocus += new RoutedEventHandler(RGB_LostFocus);

                            container.Children.Add(Rtext);
                            container.Children.Add(Gtext);
                            container.Children.Add(Btext);

                            group.Content = container;

                            ParameterStack.Children.Add(group);
                        }

                        else if (param.control == UIType.Text)
                        {
                            //var spinner = new NumericSpinner();
                            var spinner = new TextBox();
                            spinner.Uid = param.id;
                            spinner.Height = 25;
                            spinner.Text = param.value;
                            //spinner.Decimals = 10;
                            //spinner.MinValue = Convert.ToDecimal(param.min);
                            //spinner.MaxValue = Convert.ToDecimal(param.max);
                            //spinner.Value = Convert.ToDecimal(param.value);
                            //spinner.Step = Convert.ToDecimal((param.max - param.min) / 10); // TODO: Make something better

                            spinner.LostFocus += new RoutedEventHandler(ParameterText_LostFocus);

                            ParameterStack.Children.Add(spinner);
                        }
                    }
                }
                else
                {
                    Label label = new Label();
                    label.Content = "No additional parameters";
                    ParameterStack.Children.Add(label);
                }
            }
        }
        #endregion

        #region CustomTweaks
        private void CustomTweakList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CustomTweak_List.SelectedItem != null)
            {
                CustomTweaks_Grid.Visibility = Visibility.Visible;

                CustomTweak selectedTweak = (CustomTweak)CustomTweak_List.SelectedItem;
                CustomTweakName_TextBox.Text = selectedTweak.name;
                CustomTweakShaderFile_ComboBox.SelectedValue = selectedTweak.shaderFile;

                CustomTweakOldCode_RichTextBox.Document.Blocks.Clear();
                CustomTweakOldCode_RichTextBox.Document.Blocks.Add(new Paragraph(new Run(selectedTweak.oldCode)));

                CustomTweakNewCode_RichTextBox.Document.Blocks.Clear();
                CustomTweakNewCode_RichTextBox.Document.Blocks.Add(new Paragraph(new Run(selectedTweak.newCode)));
            }
            else {
                CustomTweaks_Grid.Visibility = Visibility.Collapsed;
            }
        }

        private void AddCustomTweak(object sender, RoutedEventArgs e)
        {
            if (AddCustomTweak_TextBox.Text != "")
            {
                customTweaks.Add("CUSTOM_TWEAK" + (customTweaks.Count).ToString(), new CustomTweak(AddCustomTweak_TextBox.Text, FileIO.cloudFile, customTweaks.Count, "", "", false));
                CustomTweak_List.SelectedIndex = customTweaks.Count - 1;
                CustomTweak_List.ScrollIntoView(CustomTweak_List.SelectedItem);
                AddCustomTweak_TextBox.Text = "";

                CustomTweak_List.Items.Refresh();
            }
        }

        private void DeleteCustomTweak(object sender, RoutedEventArgs e)
        {
            if (CustomTweak_List.SelectedItem != null)
            {                
                CustomTweak selectedTweak = (CustomTweak)CustomTweak_List.SelectedItem;
                var item = customTweaks.First(p => p.Value == selectedTweak); // Not the best
                customTweaks.Remove(item.Key);
                CustomTweak_List.Items.Refresh();
            }
        }
        
        private void ShaderFile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug.Assert(CustomTweak_List.SelectedItem != null);
            Debug.Assert(CustomTweakShaderFile_ComboBox.SelectedItem != null);

            CustomTweak selectedTweak = (CustomTweak)CustomTweak_List.SelectedItem;
            selectedTweak.shaderFile = (string)CustomTweakShaderFile_ComboBox.SelectedItem;
        }
        #endregion

        #region PostProcesses
        // TODO: This should probably be merged somewhat with TweakList_SelectionChanged() since it's basically the same thing
        private void PostProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PostProcess_List.SelectedItem != null)
            {
                PostProcessStack.Children.Clear();
                PostClearStack.Children.Clear();

                PostProcess selectedPost = (PostProcess)PostProcess_List.SelectedItem;
                PostTitleTextblock.Content = selectedPost.name;
                PostDescriptionTextblock.Text = selectedPost.description;

                if (selectedPost.parameters != null)
                {
                    PostProcessStack.Rows = selectedPost.parameters.Count();

                    Button resetButton = new Button();
                    resetButton.Content = "Reset";
                    resetButton.ToolTip = "Reset parameters to their default value";
                    resetButton.Width = 50;
                    resetButton.Height = 25;
                    resetButton.VerticalAlignment = VerticalAlignment.Top;
                    resetButton.HorizontalAlignment = HorizontalAlignment.Right;
                    resetButton.Click += new RoutedEventHandler(ResetParameters_Click);

                    Grid.SetColumn(resetButton, 1);
                    PostClearStack.Children.Add(resetButton);                    

                    foreach (Parameter param in selectedPost.parameters)
                    {
                        TextBlock txtBlock = new TextBlock();
                        txtBlock.Text = param.name;
                        txtBlock.TextWrapping = TextWrapping.Wrap;
                        txtBlock.Width = 170;
                        txtBlock.Height = 30;
                        txtBlock.Margin = new Thickness(0, 0, 10, 0);

                        PostProcessStack.Children.Add(txtBlock);

                        if (param.control == UIType.Checkbox)
                        {
                            CheckBox checkbox = new CheckBox();
                            checkbox.IsChecked = ((param.value == "1") ? true : false);
                            checkbox.Uid = param.id;
                            checkbox.Click += new RoutedEventHandler(Checkbox_Click);
                            PostProcessStack.Children.Add(checkbox);
                        }
                        else if (param.control == UIType.RGB)
                        {
                            var group = new GroupBox();
                            group.Header = "RGB";

                            var container = new StackPanel();
                            container.Orientation = Orientation.Horizontal;

                            var Rtext = new TextBox();
                            Rtext.Uid = param.id + "_R";
                            Rtext.Height = 25;
                            Rtext.Width = 50;
                            Rtext.Text = param.value.Split(',')[0];
                            Rtext.LostFocus += new RoutedEventHandler(RGB_LostFocus);

                            var Gtext = new TextBox();
                            Gtext.Uid = param.id + "_G";
                            Gtext.Height = 25;
                            Gtext.Width = 50;
                            Gtext.Text = param.value.Split(',')[1];
                            Gtext.LostFocus += new RoutedEventHandler(RGB_LostFocus);

                            var Btext = new TextBox();
                            Btext.Uid = param.id + "_B";
                            Btext.Height = 25;
                            Btext.Width = 50;
                            Btext.Text = param.value.Split(',')[2];
                            Btext.LostFocus += new RoutedEventHandler(RGB_LostFocus);

                            container.Children.Add(Rtext);
                            container.Children.Add(Gtext);
                            container.Children.Add(Btext);

                            group.Content = container;

                            PostProcessStack.Children.Add(group);
                        }

                        else if (param.control == UIType.Text)
                        {
                            //var spinner = new NumericSpinner();
                            var spinner = new TextBox();
                            spinner.Uid = param.id;
                            spinner.Height = 25;
                            spinner.Text = param.value;
                            //spinner.Decimals = 10;
                            //spinner.MinValue = Convert.ToDecimal(param.min);
                            //spinner.MaxValue = Convert.ToDecimal(param.max);
                            //spinner.Value = Convert.ToDecimal(param.value);
                            //spinner.Step = Convert.ToDecimal((param.max - param.min) / 10); // TODO: Make something better

                            spinner.LostFocus += new RoutedEventHandler(ParameterText_LostFocus);

                            PostProcessStack.Children.Add(spinner);
                        }
                    }
                }
                else
                {
                    Label label = new Label();
                    label.Content = "No additional parameters";
                    ParameterStack.Children.Add(label);
                }
            }
        }
        #endregion

        #region ParametersUpdates
        private void Checkbox_Click(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            TabItem currentTab = HelperFunctions.FindAncestorOrSelf<TabItem>(checkbox);
            Parameter param = null;

            switch (currentTab.Header.ToString())
            {
                case "Tweaks":
                    param = ((Tweak)(Tweak_List.SelectedItem)).parameters.First(p => p.id == checkbox.Uid);
                    break;

                case "Post Process":
                    param = ((PostProcess)(PostProcess_List.SelectedItem)).parameters.First(p => p.id == checkbox.Uid);
                    break;
            }

            if (checkbox.IsChecked == true)
            {
                param.value = "1";
            }
            else
            {
                param.value = "0";
            }
        }

        private void ParameterText_LostFocus(object sender, EventArgs e)
        {
            TextBox spinner = (TextBox)sender;
            TabItem currentTab = HelperFunctions.FindAncestorOrSelf<TabItem>(spinner);
            ListView currentList = null;
            Parameter param = null;

            switch (currentTab.Header.ToString())
            {
                case "Tweaks":
                    param = ((Tweak)Tweak_List.SelectedItem).parameters.First(p => p.id == spinner.Uid);
                    currentList = Tweak_List;
                    break;

                case "Post Process":
                    param = ((PostProcess)PostProcess_List.SelectedItem).parameters.First(p => p.id == spinner.Uid);
                    currentList = PostProcess_List;
                    break;

                case "Custom": // NOTE: Maybe unify this to behave like tweaks and post-processes                    
                    ((CustomTweak)CustomTweak_List.SelectedItem).name = spinner.Text;
                    currentList = CustomTweak_List;
                    break; 
            }

            if (currentTab.Header.ToString() != "Custom") { param.value = spinner.Text; }
        }

        private void RGB_LostFocus(object sender, EventArgs e)
        {
            TextBox senderTextBox = (TextBox)sender;

            string uid = senderTextBox.Uid.Split('_')[0];
            string channel = senderTextBox.Uid.Split('_')[1];

            TabItem currentTab = HelperFunctions.FindAncestorOrSelf<TabItem>(senderTextBox);
            Parameter param = null;

            switch (currentTab.Header.ToString())
            {
                case "Tweaks":
                    param = ((Tweak)(Tweak_List.SelectedItem)).parameters.First(p => p.id == uid);
                    break;

                case "Post Process":
                    param = ((PostProcess)(PostProcess_List.SelectedItem)).parameters.First(p => p.id == uid);
                    break;
            }

            string oldR = param.value.Split(',')[0];
            string oldG = param.value.Split(',')[1];
            string oldB = param.value.Split(',')[2];

            switch (channel)
            {
                case "R":
                    param.value = senderTextBox.Text + "," + oldG + "," + oldB;
                    break;
                case "G":
                    param.value = oldR + "," + senderTextBox.Text + "," + oldB;
                    break;
                case "B":
                    param.value = oldR + "," + oldG + "," + senderTextBox.Text;
                    break;
            }
        }

        private void RichTextBox_LostFocus(object sender, EventArgs e)
        {
            RichTextBox rich = (RichTextBox)sender;            

            switch (rich.Name)
            {
                case "CustomTweakOldCode_RichTextBox":
                    ((CustomTweak)CustomTweak_List.SelectedItem).oldCode = new TextRange(CustomTweakOldCode_RichTextBox.Document.ContentStart, CustomTweakOldCode_RichTextBox.Document.ContentEnd).Text;
                    break;

                case "CustomTweakNewCode_RichTextBox":
                    ((CustomTweak)CustomTweak_List.SelectedItem).newCode = new TextRange(CustomTweakNewCode_RichTextBox.Document.ContentStart, CustomTweakNewCode_RichTextBox.Document.ContentEnd).Text;
                    break;
            }                   
        }

        private void ResetParameters_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            TabItem currentTab = HelperFunctions.FindAncestorOrSelf<TabItem>(btn);

            switch (currentTab.Header.ToString())
            {
                case "Tweaks":
                    Tweak selectedTweak = (Tweak)Tweak_List.SelectedItem;
                    foreach (var param in selectedTweak.parameters)
                    {
                        param.value = param.defaultValue;
                    }
                    TweakList_SelectionChanged(null, null); // Trick... would not need this if we had standard bindings
                    break;

                case "Post Process":
                    PostProcess selectedPost = (PostProcess)PostProcess_List.SelectedItem;
                    foreach (var param in selectedPost.parameters)
                    {
                        param.value = param.defaultValue;
                    }
                    PostProcessList_SelectionChanged(null, null);
                    break;             
            }
        }
        #endregion

        #region Settings
        private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                ((App)Application.Current).ChangeTheme((Themes)Theme_ComboBox.SelectedItem);
            }
        }
        #endregion


        private void NewPreset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tweak in tweaks)
            {
                tweak.Value.isEnabled = false;
                if (tweak.Value.parameters != null)
                {
                    foreach (var param in tweak.Value.parameters)
                    {
                        param.value = param.defaultValue;
                    }
                }
            }

            foreach (var post in postProcesses)
            {
                post.Value.isEnabled = false;
                if (post.Value.parameters != null) { 
                    foreach (var param in post.Value.parameters)
                    {
                        param.value = param.defaultValue;
                    }
                }
            }

            PresetComments_TextBox.Text = "";
            customTweaks.Clear();
            CustomTweak_List.Items.Refresh();

            presetName = "custom_preset";
            presetPath = currentDirectory + "\\custom_preset.ini";

            Log(ErrorType.None, "New Preset [" + presetName + "] created");
        }

        private void OpenPreset_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".ini";
            dlg.Filter = "ini files|*.ini";
            dlg.Multiselect = false;
            dlg.InitialDirectory = Directory.GetCurrentDirectory();
            dlg.Title = "Browse ini file";

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            if (result.HasValue && result.Value)
            {
                // Load preset
                presetPath = dlg.FileName;
                presetName = System.IO.Path.GetFileNameWithoutExtension(presetPath);

                LoadPreset();
            }
        }

        private void LoadPreset()
        {

            try
            {
                IniFile pref = new IniFile(presetPath);

                fileData.LoadTweaks(tweaks, pref);
                fileData.LoadCustomTweaks(customTweaks, pref);
                fileData.LoadPostProcesses(postProcesses, pref);
                PresetComments_TextBox.Text = fileData.LoadComments(pref);

                Tweak_List.Items.Refresh();
                PostProcess_List.Items.Refresh();

                Log(ErrorType.None, "Preset [" + presetName + "] loaded");

            }
            catch (Exception ex)
            {
                Log(ErrorType.Error, "Failed to load preset file [" + presetName + "]. " + ex.Message);
            }
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            IniFile pref = new IniFile(presetPath);
            try
            {
                string comment = PresetComments_TextBox.Text;
                fileData.SavePreset(presetPath, tweaks, customTweaks, postProcesses, comment, pref);
                Log(ErrorType.None, "Preset [" + presetName + "] saved in " + presetPath);
            }
            catch (Exception ex)
            {
                Log(ErrorType.Error, "Failed to save preset file [" + presetName + "]. " + ex.Message);
            }
        }

        private void SavePresetAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.Filter = "ini files|*.ini";
            dlg.Title = "Save Preset";
            dlg.FileName = "custom_preset.ini";

            Nullable<bool> result = dlg.ShowDialog();

            if (result.HasValue && result.Value && dlg.FileName != "")
            {
                presetPath = dlg.FileName;
                presetName = System.IO.Path.GetFileNameWithoutExtension(presetPath);
                IniFile pref = new IniFile(presetPath);
                try
                {
                    string comment = PresetComments_TextBox.Text;
                    fileData.SavePreset(presetPath, tweaks, customTweaks, postProcesses, comment, pref);
                    Log(ErrorType.None, "Preset [" + presetName + "] saved in " + presetPath);
                }
                catch (Exception ex)
                {
                    Log(ErrorType.Error, "Failed to save preset file [" + presetName + "]. " + ex.Message);
                }
            }
        }


        private void ApplyPreset(object sender, RoutedEventArgs e)
        {

            fileData.LoadShaderFiles(backupDirectory); // Always load the unmodified files;

            // NOTE: Not sure what is the best way to implement this... for now just handle each tweak on a case by case basis, which is a lot of code but fine for now
            // NOTE: This code is getting more awful by the minute           

            foreach (var tweak in tweaks.Values)
            {
                if (tweak.isEnabled)
                {
                    bool supported = true;
                    bool success = false;
                    string currentFile = "";

                    switch (tweak.name)
                    {

                        #region Clouds       
                        case "'No popcorn' clouds":
                            currentFile = FileIO.cloudFile; // I really don't like this
                            cloudText = cloudText.AddAfter(ref success, "void GetPointDiffuse( out float4 diffuse, in float3 corner, in float3 groupCenter", ", in float cloudDistance");
                            cloudText = cloudText.AddAfter(ref success, "float  fIntensity = -1.0f * max(dot(lightDirection, cloudGroupNormal), dot(lightDirection, facingDirection));", "\r\nconst float fExp = saturate(exp(-cloudDistance * cloudDistance * " + tweak.parameters[0].value.ToString() + "));\r\nfIntensity = lerp(0.35f, fIntensity, fExp);");
                            cloudText = cloudText.AddAfter(ref success, "diffuse = saturate(float4(.85f * colorIntensity.rgb + (0.33f * saturate(colorIntensity.rgb - 1)), colorIntensity.a));", "\r\nif (diffuse.a > " + tweak.parameters[1].value.ToString() + ") { diffuse.a = lerp(" + tweak.parameters[1].value.ToString() + ", diffuse.a, fExp); }");
                            cloudText = cloudText.AddAfter(ref success, "GetPointDiffuse(Out.diffuse[i], position, spriteCenter.xyz", ", length(positionVector)");
                            cloudText = cloudText.AddAfter(ref success, "GetPointDiffuse( Out.diffuse[i], position, spriteCenter.xyz", ", length(positionVector)");
                            break;

                        case "Alternate lighting for cloud groups":
                            currentFile = FileIO.cloudFile;
                            cloudText = cloudText.ReplaceAll(ref success, "GetPointDiffuse(Out.diffuse[i], position, spriteCenter.xyz", "GetPointDiffuse(Out.diffuse[i], position, groupCenter.xyz");
                            cloudText = cloudText.ReplaceAll(ref success, "GetPointDiffuse( Out.diffuse[i], position, spriteCenter.xyz", "GetPointDiffuse(Out.diffuse[i], position, groupCenter.xyz");
                            break;

                        case "Cirrus lighting":
                            currentFile = FileIO.generalFile;
                            generalText = generalText.AddBefore(ref success, "// Apply IR if active", "if (cb_mObjectType == (uint)3)\r\n    {\r\n        cColor.rgb = " + tweak.parameters[0].value.ToString() + " * saturate(lerp(dot(cColor.rgb, float3(0.299f, 0.587f, 0.114f)), cColor.rgb, " + tweak.parameters[1].value.ToString() + "));\r\n   }\r\n");
                            break;

                        case "Cloud light scattering":
                            currentFile = FileIO.cloudFile;
                            cloudText.CommentOut(ref success, "if (fIntensity < -cb_mMedianLine)", "    fIntensity = clamp(fIntensity, 0, 1);", false);
                            cloudText.AddBefore(ref success, "/*if (fIntensity < -cb_mMedianLine)", "fIntensity =  saturate(" + tweak.parameters[0].value.ToString() + " * fIntensity + " + tweak.parameters[1].value.ToString() + ");\r\n");

                            if (tweak.parameters[2].value == "1")
                            {
                                cloudText = cloudText.CommentOut(ref success, "float height = corner.y;", "float4 color = lerp(baseColor, topColor, s);", true);
                                cloudText = cloudText.ReplaceAll(ref success, "float4 colorIntensity = float4(fRed, fGreen, fBlue, saturate(alpha)) * color;", "float4 colorIntensity = float4(fRed, fGreen, fBlue, saturate(alpha));");
                            }
                            break;

                        case "Cloud lighting tuning":
                            currentFile = FileIO.cloudFile;
                            cloudText = cloudText.ReplaceAll(ref success, "diffuse = saturate(float4(.85f * colorIntensity.rgb + (0.33f * saturate(colorIntensity.rgb - 1)), colorIntensity.a));", "diffuse = saturate( float4( " + tweak.parameters[0].value.ToString() + " * colorIntensity.rgb + ( " + tweak.parameters[1].value.ToString() + " * saturate(colorIntensity.rgb - 1)), colorIntensity.a));");
                            break;

                        case "Cloud saturation":
                            currentFile = FileIO.cloudFile;
                            cloudText = cloudText.AddAfter(ref success, "* saturate(colorIntensity.rgb - 1)), colorIntensity.a));", "\r\ndiffuse.rgb = saturate(lerp(dot(diffuse.rgb, float3(0.299f, 0.587f, 0.114f)), diffuse.rgb, " + tweak.parameters[0].value.ToString() + "));");
                            break;

                        // NOTE: not sure if needed anymore, see translucentIntensity = lerp(1.0f, translucentShadows.r, blendValue); in v4 file
                        case "Cloud shadow depth":
                            currentFile = FileIO.cloudFile;
                            //shadowText = shadowText.ReplaceAll("cloudShadowIntensity = lerp(0.15f,1.0f,cloudShadows.r);", "cloudShadowIntensity = lerp(x, 1.0, cloudShadows.r);");
                            supported = false;
                            break;

                        // NOTE: Only the 1st entry needs to be replaced
                        case "Cloud shadow extended size":
                            currentFile = FileIO.cloudFile;
                            cloudText = cloudText.ReplaceFirst(ref success, "Out.position[i] = mul(float4(position, 1.0), matWorld);", "Out.position[i] = mul(float4(position, 0.8), matWorld);");
                            break;

                        case "Reduce cloud brightness at dawn/dusk/night":
                            currentFile = FileIO.cloudFile;
                            cloudText = cloudText.AddAfter(ref success, "float3 fColor = fIntensity * cb_mCloudDirectional.rgb + cb_mCloudAmbient.rgb;", "\r\n    float kk = 1 + saturate(fColor.g/(cb_mFogColor.g + 0.00001) - 2);\r\n    fColor /= kk;\r\n");
                            break;

                        case "Reduce top layer cloud brightness at dawn/dusk/night":
                            currentFile = FileIO.generalFile;
                            generalText = generalText.AddAfter(ref success, "#endif //SHD_ALPHA_TEST", "\r\nif (cb_mObjectType == (uint)3)\r\n {\r\n      float kk = 1 + saturate(cColor.g / (cb_mFogColor.g + 0.00001) - 2);\r\n     cColor.rgb /= kk;\r\n  }\r\n", 2);
                            break;

                        case "Cloud puffs width and height scaling":
                            currentFile = FileIO.cloudFile;
                            cloudText = cloudText.ReplaceAll(ref success, "GetScreenQuadPositions(quad, width*0.5, height*0.5);", "GetScreenQuadPositions(quad, width*" + tweak.parameters[0].value.ToString() + ", height*" + tweak.parameters[1].value.ToString() + ");");
                            break;

                        #endregion

                        #region Lighting
                        case "Aircraft lighting and saturation":
                            currentFile = FileIO.generalFile;

                            if (tweak.parameters[5].value == "1")
                            {
                                generalText = generalText.ReplaceAll(ref success, @"cDiffuse = cBase * (float4( saturate( 
                (cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + 
                (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);", "#if !defined(PS_NEEDS_TANSPACE)\r\n   if (cb_mObjectType == 19)\r\n cDiffuse = cBase * (float4(saturate((cb_mSun.mAmbient.xyz * " + tweak.parameters[0].value.ToString() + " + (shadowContrib * (sunDiffuse * " + tweak.parameters[1].value.ToString() + " * fDotSun))) +\r\n     (cb_mMoon.mAmbient.xyz * " + tweak.parameters[2].value.ToString() + " + (shadowContrib * (moonDiffuse * " + tweak.parameters[3].value.ToString() + " * fDotMoon)))), 1) + cDiffuse);\r\n  #else\r\n  if (cb_mObjectType == 19)\r\n   cDiffuse = cBase * (float4(saturate((cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);\r\n #endif\r\n  else\r\n  cDiffuse = cBase * (float4(saturate((cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);");
                            }
                            else
                            {
                                generalText = generalText.ReplaceAll(ref success, @"cDiffuse = cBase * (float4( saturate( 
                (cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + 
                (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);", "if (cb_mObjectType == 19)\r\n cDiffuse = cBase * (float4( saturate((cb_mSun.mAmbient.xyz * " + tweak.parameters[0].value.ToString() + " + (shadowContrib * (sunDiffuse * " + tweak.parameters[1].value.ToString() + " * fDotSun))) + (cb_mMoon.mAmbient.xyz * " + tweak.parameters[2].value.ToString() + " + (shadowContrib * (moonDiffuse * " + tweak.parameters[3].value.ToString() + " * fDotMoon)))), 1) +cDiffuse);\r\n   else\r\n     cDiffuse = cBase * (float4(saturate((cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse); ");
                            }

                            generalText = generalText.AddBefore(ref success, "// Apply IR if active", "if ((cb_mObjectType == (uint)0)  ||  (cb_mObjectType == (uint)19))\r\n    {\r\n   cColor.rgb = saturate(lerp(dot(cColor.rgb, float3(0.299f, 0.587f, 0.114f)), cColor.rgb, " + tweak.parameters[4].value.ToString() + "));\r\n    }\r\n");

                            break;

                        case "Autogen emissive lighting":
                            currentFile = FileIO.generalFile;
                            generalText = generalText.AddBefore(ref success, "#if ( VIEW_TYPE == SHD_VIEW_TYPE_REFLECTION )", "if (cb_mObjectType != 19) fEmissiveScale *= " + tweak.parameters[1].value.ToString() + ";\r\n");
                            generalText = generalText.ReplaceAll(ref success, "cColor = lerp(fEmissiveScale * cEmissive, cColor, 1 - cb_mDayNightInterpolant);", "cColor = saturate(lerp(fEmissiveScale * cEmissive, cColor, 1 - cb_mDayNightInterpolant));");
                            generalText = generalText.ReplaceSecond(ref success, "fEmissiveScale = cb_mHDREmissiveScale * cEmissive.a;", "fEmissiveScale = " + tweak.parameters[0].value.ToString() + " * cb_mHDREmissiveScale * cEmissive.a;");

                            if (tweak.parameters[2].value == "1")
                            {
                                generalText = generalText.ReplaceSecond(ref success, "cColor += float4(fEmissiveScale * cEmissive.rgb, 0);", "if ((cb_mObjectType == 10) || (cb_mObjectType == 28)) cColor = lerp(fEmissiveScale * cEmissive, cColor, 1 - cb_mDayNightInterpolant); else cColor += float4(fEmissiveScale * cEmissive.rgb, 0);");
                            }
                            break;

                        case "Objects lighting":
                            currentFile = FileIO.generalFile;
                            {
                                int index = generalText.IndexOf(@"cDiffuse = cBase * (float4( saturate( 
                (cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + 
                (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);");

                                if (index >= 0)
                                {
                                    generalText = generalText.ReplaceAll(ref success, @"cDiffuse = cBase * (float4( saturate( 
                (cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + 
                (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);", "if (cb_mObjectType != 19)\r\n cDiffuse = cBase * (float4(saturate((cb_mSun.mAmbient.xyz * " + tweak.parameters[0].value.ToString() + " + (shadowContrib * (sunDiffuse * " + tweak.parameters[1].value.ToString() + " * fDotSun))) + (cb_mMoon.mAmbient.xyz * " + tweak.parameters[2].value.ToString() + " + (shadowContrib * (moonDiffuse * " + tweak.parameters[3].value.ToString() + " * fDotMoon)))), 1) + cDiffuse);\r\n   else\r\n cDiffuse = cBase * (float4(saturate((cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse); ");
                                }
                                else // Aircraft lighting and saturation is applied
                                {
                                    generalText = generalText.AddBefore(ref success, "#if !defined(SHD_BASE) && defined(SHD_RECEIVE_SHADOWS)", "if (cb_mObjectType != 19)\r\n cDiffuse = cBase * (float4(saturate((cb_mSun.mAmbient.xyz * " + tweak.parameters[0].value.ToString() + " + (shadowContrib * (sunDiffuse * " + tweak.parameters[1].value.ToString() + " * fDotSun))) + (cb_mMoon.mAmbient.xyz * " + tweak.parameters[2].value.ToString() + " + (shadowContrib * (moonDiffuse * " + tweak.parameters[3].value.ToString() + " * fDotMoon)))), 1) + cDiffuse);\r\n   else\r\n cDiffuse = cBase * (float4(saturate((cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);\r\n");
                                }
                            }
                            break;

                        case "Specular lighting":
                            currentFile = FileIO.funclibFile;
                            funclibText = funclibText.ReplaceAll(ref success, "return specularIntensity * SpecularColor * DiffuseColor;", "return " + tweak.parameters[0].value.ToString() + " * specularIntensity * SpecularColor * DiffuseColor;");
                            break;

                        case "Terrain lighting":
                            currentFile = FileIO.terrainFile;
                            terrainText = terrainText.ReplaceAll(ref success, "const float3 finalSunColor = (sunAmbient + (sunDiffuse * (sunContrib * shadowContrib)));", "const float3 finalSunColor = (sunAmbient * " + tweak.parameters[0].value.ToString() + " + (sunDiffuse * " + tweak.parameters[1].value.ToString() + " * (sunContrib * shadowContrib)));");
                            terrainText = terrainText.ReplaceAll(ref success, "const float3 finalMoonColor = (moonAmbient + (moonDiffuse * (moonContrib * shadowContrib)));", "const float3 finalMoonColor = (moonAmbient * " + tweak.parameters[2].value.ToString() + " + (moonDiffuse * " + tweak.parameters[3].value.ToString() + " * (moonContrib * shadowContrib)));");
                            break;

                        case "Terrain saturation":
                            currentFile = FileIO.terrainFile;
                            terrainText = terrainText.AddAfter(ref success, "FinalColor = float4(FinalLighting, fAlpha);", "\r\nFinalColor.rgb = saturate(lerp(dot(FinalColor.rgb, float3(0.299f, 0.587f, 0.114f)), FinalColor.rgb, " + tweak.parameters[0].value.ToString() + "));");
                            break;

                        case "Urban areas lighting at night":
                            currentFile = FileIO.terrainFile;
                            terrainText = terrainText.ReplaceSecond(ref success, "EmissiveColor = (EmissiveColor*EmissiveColor);", "EmissiveColor = pow(saturate(EmissiveColor), " + tweak.parameters[1].value.ToString() + ");");
                            terrainText = terrainText.AddAfter(ref success, @"EmissiveColor *= 0.35f;
        #endif", "\r\nEmissiveColor *= " + tweak.parameters[0].value.ToString() + ";");
                            break;

                        #endregion

                        #region Atmosphere
                        case "Clouds Fog tuning":
                            currentFile = FileIO.cloudFile;
                            cloudText = cloudText.ReplaceAll(ref success, "cColor = VolumetricFogPS( In.mAlt, cColor, In.fFogDistance / 2.0, cb_mFogDensity, cb_mFogColor.xyz);", "cColor = VolumetricFogPS( In.mAlt, cColor, In.fFogDistance * " + tweak.parameters[0].value.ToString() + ", cb_mFogDensity, cb_mFogColor.xyz);");
                            cloudText = cloudText.ReplaceAll(ref success, "cColor = float4( FogPS( cColor.xyz, In.fFogDistance / 2.0, cb_mFogDensity, cb_mFogColor.xyz ), cColor.a );", "cColor = float4( FogPS( cColor.xyz, In.fFogDistance * " + tweak.parameters[0].value.ToString() + ", cb_mFogDensity, cb_mFogColor.xyz ), cColor.a );");
                            break;


                        case "Haze effect":
                            currentFile = FileIO.funclibFile;

                            string rayleighString = "";
                            Tweak rayleigh = tweaks.Values.First(p => p.name == "Rayleigh scattering effect");
                            if (rayleigh.isEnabled)
                            {
                                rayleighString = "&&& ADD THE RAYLEIGH TWEAK HERE &&&";
                            }

                            funclibText = funclibText.AddBefore(ref success, "return lerp(cFog, cColor, saturate(exp(-fDistance*fDistance*fFogDensitySquared)));", "#if !defined(SHD_VOLUMETRIC_FOG)\r\n float3 FinalColor = cColor;\r\n  if ((cb_mObjectType != (uint)1) && (cb_mObjectType != (uint)3) && (cb_mObjectType != (uint)21) && (cb_mObjectType != (uint)19))\r\n   {\r\n   FinalColor.rgb = lerp(pow(saturate(cb_mFogColor.rgb * float3(" + tweak.parameters[3].value.ToString() + ")), (1 + saturate(cb_mSun.mDiffuse.g - 0.35f)) * " + tweak.parameters[0].value.ToString() + "), FinalColor.rgb, saturate(exp(-fDistance * fDistance * " + tweak.parameters[1].value.ToString() + ")));\r\n  }\r\n" + rayleighString + "  return lerp(cFog, FinalColor, saturate(exp(-fDistance * fDistance * fFogDensitySquared)));\r\n #endif\r\n");
                            funclibText = funclibText.AddAfter(ref success, "float horizonFogDensity = fFogDensity;", "\r\n#if !defined(SHD_ADDITIVE) && !defined(SHD_MULTIPLICATIVE)\r\n if ((cb_mObjectType != (uint)1) && (cb_mObjectType != (uint)3) && (cb_mObjectType != (uint)21) && (cb_mObjectType != (uint)19))\r\n  {\r\n  FinalColor.rgb = lerp(pow(saturate(cb_mFogColor.rgb * float3(" + tweak.parameters[3].value.ToString() + ")), (1 + saturate(cb_mSun.mDiffuse.g - 0.35f)) * " + tweak.parameters[0].value.ToString() + "), FinalColor.rgb, saturate(exp(-distQuared * " + tweak.parameters[1].value.ToString() + ")));\r\n }\r\n #endif");

                            if (tweak.parameters[2].value == "1")
                            {
                                // NOTE: Careful with this, the search string has a variable in it...
                                funclibText = funclibText.ReplaceAll(ref success, "FinalColor.rgb, saturate(exp(-distQuared * " + tweak.parameters[1].value.ToString() + "))", "FinalColor.rgb, saturate(exp(-distQuared * " + tweak.parameters[1].value.ToString() + " * saturate(1.0f - cb_Altitude/15000)))");
                            }

                            break;

                        case "Rayleigh scattering effect":
                            currentFile = FileIO.funclibFile;
                            {
                                int index = funclibText.IndexOf("&&& ADD THE RAYLEIGH TWEAK HERE &&&");

                                if (index >= 0)
                                {
                                    funclibText = funclibText.ReplaceAll(ref success, "&&& ADD THE RAYLEIGH TWEAK HERE &&&", "if ((cb_mObjectType != (uint)1) && (cb_mObjectType != (uint)21) && (cb_mObjectType != (uint)19))\r\n  {\r\n  const float DensFactor = " + tweak.parameters[1].value.ToString() + ";\r\n  const float DistK = " + tweak.parameters[0].value.ToString() + " * (1 - saturate(exp(-fDistance * fDistance * DensFactor))) * saturate(cb_mSun.mDiffuse.g - 0.15);\r\n  FinalColor.rgb = FinalColor.rgb * (1 - float3(0.00, 0.055, 0.111) * DistK) + float3(0.00, 0.055, 0.111) * DistK;\r\n  }\r\n");
                                }
                                else
                                {
                                    funclibText = funclibText.AddBefore(ref success, "return lerp(cFog, cColor, saturate(exp(-fDistance*fDistance*fFogDensitySquared)));", "#if !defined(SHD_VOLUMETRIC_FOG)\r\n     float3 FinalColor = cColor;\r\n  if ((cb_mObjectType != (uint)1) && (cb_mObjectType != (uint)21) && (cb_mObjectType != (uint)19))\r\n   {\r\n  const float DensFactor = " + tweak.parameters[1].value.ToString() + ";\r\n  const float DistK = " + tweak.parameters[0].value.ToString() + " * (1 - saturate(exp(-fDistance * fDistance * DensFactor))) * saturate(cb_mSun.mDiffuse.g - 0.15);\r\n    FinalColor.rgb = FinalColor.rgb * (1 - float3(0.00, 0.055, 0.111) * DistK) + float3(0.00, 0.055, 0.111) * DistK;\r\n  }\r\n  return lerp(cFog, FinalColor, saturate(exp(-fDistance * fDistance * fFogDensitySquared)));\r\n #endif\r\n");
                                }

                                funclibText = funclibText.AddAfter(ref success, "float3 layerEnableFade = float3(1, 1, 1);", "\r\n#if !defined(SHD_ADDITIVE) && !defined(SHD_MULTIPLICATIVE)\r\n  if ((cb_mObjectType != (uint)1) && (cb_mObjectType != (uint)21) && (cb_mObjectType != (uint)19))\r\n  {\r\n    const float DensFactor = " + tweak.parameters[1].value.ToString() + ";\r\n    const float DistK = " + tweak.parameters[0].value.ToString() + " * (1 - saturate(exp(-distQuared * DensFactor))) * saturate(cb_mSun.mDiffuse.g - 0.15);\r\n   FinalColor.rgb = FinalColor.rgb * (1 - float3(0.00, " + tweak.parameters[4].value.ToString() + ", " + tweak.parameters[5].value.ToString() + ") * DistK) + float3(0.00, " + tweak.parameters[4].value.ToString() + ", " + tweak.parameters[5].value.ToString() + ") * DistK;\r\n  }\r\n#endif");

                                if (tweak.parameters[2].value == "1")
                                {
                                    // NOTE: Careful with this, the search string has a variable in it...
                                    funclibText = funclibText.ReplaceAll(ref success, "const float DensFactor = " + tweak.parameters[1].value.ToString() + ";", "const float DensFactor = " + tweak.parameters[1].value.ToString() + " * saturate(1.0f - cb_Altitude/15000);");
                                    funclibText = funclibText.ReplaceAll(ref success, "if ((cb_mObjectType != (uint)1) && (cb_mObjectType != (uint)21) && (cb_mObjectType != (uint)19))", "if ((cb_mObjectType != (uint)1) && (cb_mObjectType != (uint)3) && (cb_mObjectType != (uint)21) && (cb_mObjectType != (uint)19))");
                                }

                            }
                            break;

                        case "Sky Fog tuning":
                            currentFile = FileIO.funclibFile;
                            funclibText = funclibText.AddAfter(ref success, @"float3 FogPS(const float3 cColor,
             const float  fDistance,
             const float  fFogDensitySquared,
             const float3 cFog)
{", "\r\nfloat fDens = fFogDensitySquared;\r\n    if (cb_mObjectType == (uint)1) fDens *= " + tweak.parameters[0].value.ToString() + "; ");
                            funclibText = funclibText.ReplaceAll(ref success, "return lerp(cFog, cColor, saturate(exp(-fDistance*fDistance*fFogDensitySquared)));", "return lerp(cFog, cColor, saturate(exp(-fDistance*fDistance*fDens)));");
                            break;

                        case "Sky saturation":
                            currentFile = FileIO.generalFile;
                            generalText = generalText.AddBefore(ref success, "// Apply IR if active", "if (cb_mObjectType == (uint)1)\r\n    {\r\n    cColor.rgb = saturate(lerp(dot(cColor.rgb, float3(0.299f, 0.587f, 0.114f)), cColor.rgb, " + tweak.parameters[0].value.ToString() + "));\r\n   }\r\n");
                            break;

                        #endregion

                        #region Water
                        case "FSX-style reflections":
                            currentFile = FileIO.terrainFile;
                            terrainText = terrainText.ReplaceAll(ref success, "float3 vEyeDirWS = (vEyeVectWS) / eyeDist;", "float3 vEyeDirWS = (vEyeVectWS) * 0.99/ eyeDist;");
                            terrainText = terrainText.ReplaceAll(ref success, "saturate((pow(abs(specularBoost * saturate(float2(dot(vreflect,vEyeDirWS.xyz), dot(runningNormal, vHN2))))", "saturate((pow(abs(specularBoost * saturate(float2(dot(runningNormal, vHN), dot(runningNormal, vHN2))))");
                            terrainText = terrainText.ReplaceAll(ref success, "(pow(abs(specularBoost * saturate(float2(dot(vreflect,vEyeDirWS.xyz), dot(Bump.xyz, vHN2))))", "(pow(abs(specularBoost * saturate(float2(dot(Bump.xyz, vHN), dot(Bump.xyz, vHN2))))");
                            break;

                        case "Water saturation":
                            currentFile = FileIO.terrainFile;
                            terrainText = terrainText.AddAfter(ref success, "FinalColor = float4(FinalLighting, fAlpha);", "\r\n#if defined(SHD_HAS_WATER)\r\n  if (Input.IsWater) FinalColor.rgb = saturate(lerp(dot(FinalColor.rgb, float3(0.299f, 0.587f, 0.114f)), FinalColor.rgb, " + tweak.parameters[0].value + "));\r\n #endif");
                            break;

                        case "Water surface tuning":
                            currentFile = FileIO.terrainFile;
                            terrainText = terrainText.ReplaceAll(ref success, "const float bias = 1 + 3 * saturate( 1.0f - dot( vEyeDirWS,float3(  0, 1, 0 )));", "const float bias = 1 + " + tweak.parameters[2].value + " * saturate( 1.0f - dot( vEyeDirWS,float3(  0, 1, 0 )));");
                            terrainText = terrainText.ReplaceAll(ref success, "specularFactor = (specularBlend *", "specularFactor = (specularBlend * " + tweak.parameters[3].value + " *");
                            terrainText = terrainText.ReplaceAll(ref success, "reflectionFresnel = clamp( .001f + 0.99f * pow( saturate(1 - dot( vEyeDirWS.xyz, fresnelNormal)), 4 ), 0, 1 );", "reflectionFresnel = clamp( .001f + 0.99f * pow( saturate(1 - dot( vEyeDirWS.xyz, fresnelNormal)), " + tweak.parameters[4].value + " ), 0, 1 );");
                            terrainText = terrainText.ReplaceAll(ref success, "EnvironmentColor.rgb = .40f * reflectionRefractionColor.rgb * ( 1 - fAlpha );", "EnvironmentColor.rgb = " + tweak.parameters[0].value + " * reflectionRefractionColor.rgb * ( 1 - fAlpha );");
                            break;

                        case "Wave size":
                            currentFile = FileIO.terrainFile;
                            terrainText = terrainText.ReplaceAll(ref success, "const float fLogEyeDist = min(log2(eyeDist) - 7, 7);", "const float fLogEyeDist = min(log2(eyeDist/(1 + saturate(cb_Altitude/10000) * " + tweak.parameters[0].value + ")) - 7, 7);");
                            terrainText = terrainText.AddAfter(ref success, "Bump.xyz = lerp(Bump.xyz, level[1], saturate((eyeDist - 24000) / 24000));", "\r\nBump.xz *= (1 - saturate(cb_Altitude/10000 * " + tweak.parameters[1].value + "));");
                            break;

                        case "Wave speed":
                            currentFile = FileIO.terrainFXHFile;
                            terrainFXHText = terrainFXHText.ReplaceAll(ref success, "const float2 scrollOffset = windScaler * cb_mSimTime * float2(sc.x, sc.y);", "const float2 scrollOffset = windScaler * " + tweak.parameters[0].value + " * cb_mSimTime * float2(sc.x, sc.y);");
                            break;

                        #endregion

                        #region HDR
                        case "Alternate tonemap adjustment":
                            currentFile = FileIO.HDRFile;
                            HDRText = HDRText.ReplaceAll(ref success, "return saturate(pow(color, 2.2f));", "return saturate(pow(color, 2.5f) * 1.2f);");
                            break;

                        case "Contrast tuning":
                            currentFile = FileIO.HDRFile;
                            double tweakValue = double.Parse(tweak.parameters[0].value); // TODO: Robustness, error checking etc
                            double val1 = 1 + (0 - 1) * tweakValue;
                            double val2 = 2.2 + (1.2 - 2.2) * tweakValue;

                            HDRText = HDRText.ReplaceAll(ref success, "color = (color * (6.2f * color + 0.5f)) / (color * (6.2f * color + 1.7f) + 0.06f);", $"color = (color * (6.2f * color + {val1.ToString()})) / (color * (6.2f * color + {val2.ToString()}) + 0.06);");
                            break;

                        case "Scene tone adjustment":
                            currentFile = FileIO.HDRFile;
                            HDRText = HDRText.AddBefore(ref success, "return float4(finalColor, 1.0f);", $"finalColor.rgb = saturate(finalColor.rgb * float3({tweak.parameters[0].value}))\r\n;");
                            break;

                        case "Turn off HDR luminance adaptation effect":
                            currentFile = FileIO.HDRFile;
                            HDRText = HDRText.ReplaceAll(ref success, "return max(exp(lumTex.Sample(samClamp, texCoord).x), 0.1f);", "return max((1-cb_mDayNightInterpolant) * 0.35, 0.1);");
                            break;

                            #endregion

                    }


                    if (!success && supported)
                    {
                        Log(ErrorType.Error, "Failed to apply tweak [" + tweak.name + "] in " + currentFile + " file.");
                    }
                    else if (!success && !supported) {
                        Log(ErrorType.Warning, "Did not apply tweak [" + tweak.name + "] in " + currentFile + " file. Tweak is not upported.");
                    }
                    else
                    {
                        Log(ErrorType.None, "Tweak [" + tweak.name + "] applied.");
                    }
                }
            }

            foreach (CustomTweak custom in customTweaks.Values)
            {
                if (custom.isEnabled)
                {
                    bool success = false;

                    switch (custom.shaderFile)
                    {

                        case FileIO.cloudFile:
                            cloudText = cloudText.ReplaceAll(ref success, custom.oldCode, custom.newCode);
                            break;

                        case FileIO.generalFile:
                            generalText = generalText.ReplaceAll(ref success, custom.oldCode, custom.newCode);
                            break;

                        case FileIO.terrainFile:
                            terrainText = terrainText.ReplaceAll(ref success, custom.oldCode, custom.newCode);
                            break;

                        case FileIO.funclibFile:
                            funclibText = funclibText.ReplaceAll(ref success, custom.oldCode, custom.newCode);
                            break;

                        case FileIO.shadowFile:
                            shadowText = shadowText.ReplaceAll(ref success, custom.oldCode, custom.newCode);
                            break;

                        case FileIO.terrainFXHFile:
                            terrainFXHText = terrainFXHText.ReplaceAll(ref success, custom.oldCode, custom.newCode);
                            break;

                        case FileIO.HDRFile:
                            HDRText = HDRText.ReplaceAll(ref success, custom.oldCode, custom.newCode);
                            break;
                    }

                    if (!success)
                    {
                        Log(ErrorType.Error, "Failed to apply custom tweak [" + custom.name + "] in " + custom.shaderFile + " file.");
                    }
                    else
                    {
                        Log(ErrorType.None, "Custom tweak [" + custom.name + "] applied.");
                    }
                }
            }


            if (postProcesses.Any(p => p.Value.isEnabled == true))
            { // if at least one effect is applied
                bool success = false;
                HDRText = HDRText.ReplaceAll(ref success, "return float4(finalColor, 1.0f);", "float4 EndColor = float4(finalColor.rgb, 1);\r\nreturn EndColor;");

                if (!success)
                {
                    Log(ErrorType.Error, "Failed to apply post process function block in " + FileIO.HDRFile + " file.");
                }
            }

            foreach (PostProcess post in postProcesses.Values)
            {
                if (post.isEnabled)
                {
                    bool success = false;

                    switch (post.name)
                    {
                        // NOTE: C# 6 String stuff, pretty neat
                        case "Sepia":
                            HDRText = HDRText.AddBefore(ref success, "// Applies exposure and tone mapping to the input, and combines it with the",
$@"float4 SepiaMain(PsQuad vert, float4 color) : SV_Target
{{
const float3 Sepia_ColorTone = float3({post.parameters[0].value});
const float Sepia_GreyPower = {post.parameters[1].value};
const float Sepia_SepiaPower = {post.parameters[2].value};
    uint2 uTDim, uDDim;
    srcTex.GetDimensions(uTDim.x,uTDim.y);
    int3 iTexCoord = int3(uTDim.x * vert.texcoord.x, uTDim.y * vert.texcoord.y, 0);
	float3 sepia = color.rgb;
	float grey = dot(sepia, float3(0.2126, 0.7152, 0.0722));
	sepia *= Sepia_ColorTone;
	float3 blend2 = (grey * Sepia_GreyPower) + (color.rgb / (Sepia_GreyPower + 1));
	color.rgb = lerp(blend2, sepia, Sepia_SepiaPower);
	return color;
}}"
);
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", "EndColor = SepiaMain(vert, EndColor);\r\n");
                            break;

                        case "Curves":
                            HDRText = HDRText.AddBefore(ref success, "// Applies exposure and tone mapping to the input, and combines it with the",
$@"#define Curves_mode {post.parameters[0].value}
#define Curves_formula {post.parameters[2].value}
float4 CurvesMain(PsQuad vert, float4 color) : SV_Target
{{
  const float Curves_contrast = {post.parameters[1].value};
  uint2 uTDim, uDDim;
  srcTex.GetDimensions(uTDim.x,uTDim.y);
  int3 iTexCoord = int3(uTDim.x * vert.texcoord.x, uTDim.y * vert.texcoord.y, 0);
  float4 colorInput = saturate(color);
  float3 lumCoeff = float3(0.2126, 0.7152, 0.0722);
  float Curves_contrast_blend = Curves_contrast;
#if Curves_mode != 2
   float luma = dot(lumCoeff, colorInput.rgb);
    float3 chroma = colorInput.rgb - luma;
#endif
#if Curves_mode == 2
	float3 x = colorInput.rgb;
#elif Curves_mode == 1
	float3 x = chroma;
	x = x * 0.5 + 0.5;
#else
	float x = luma;
#endif
#if Curves_formula == 1
   x = sin(3.1415927 * 0.5 * x);
   x *= x;
#endif
#if Curves_formula == 2
  x = x - 0.5;
  x = ( x / (0.5 + abs(x)) ) + 0.5;
#endif
#if Curves_formula == 3
	x = x*x*(3.0-2.0*x);
#endif
#if Curves_formula == 4
   x = (1.0524 * exp(6.0 * x) - 1.05248) / (exp(6.0 * x) + 20.0855);
#endif
#if Curves_formula == 5
  x = x * (x * (1.5-x) + 0.5);
  Curves_contrast_blend = Curves_contrast * 2.0;
#endif
#if Curves_formula == 6
  x = x*x*x*(x*(x*6.0 - 15.0) + 10.0);
#endif
#if Curves_formula == 7
	x = x - 0.5;
	x = x / ((abs(x)*1.25) + 0.375 ) + 0.5;
#endif
#if Curves_formula == 8
  x = (x * (x * (x * (x * (x * (x * (1.6 * x - 7.2) + 10.8) - 4.2) - 3.6) + 2.7) - 1.8) + 2.7) * x * x;
#endif
#if Curves_formula == 9
  x =  -0.5 * (x*2.0-1.0) * (abs(x*2.0-1.0)-2.0) + 0.5;
#endif
#if Curves_formula == 10
    #if Curves_mode == 0
			float xstep = step(x,0.5);
			float xstep_shift = (xstep - 0.5);
			float shifted_x = x + xstep_shift;
    #else
			float3 xstep = step(x,0.5);
			float3 xstep_shift = (xstep - 0.5);
			float3 shifted_x = x + xstep_shift;
    #endif
	x = abs(xstep - sqrt(-shifted_x * shifted_x + shifted_x) ) - xstep_shift;
	Curves_contrast_blend = Curves_contrast * 0.5;
#endif
#if Curves_formula == 11
  	#if Curves_mode == 0
			float a = 0.0;
			float b = 0.0;
		#else
			float3 a = float3(0.0,0.0,0.0);
			float3 b = float3(0.0,0.0,0.0);
		#endif
    a = x * x * 2.0;
    b = (2.0 * -x + 4.0) * x - 1.0;
    x = (x < 0.5) ? a : b;
#endif
#if Curves_formula == 21
    float a = 1.00; float b = 0.00; float c = 1.00; float d = 0.20;
    x = 0.5 * ((-a + 3*b -3*c + d)*x*x*x + (2*a -5*b + 4*c - d)*x*x + (-a+c)*x + 2*b);
#endif
#if Curves_formula == 22
    float a = 0.00; float b = 0.00; float c = 1.00; float d = 1.00;
	float r  = (1-x); float r2 = r*r; float r3 = r2 * r; float x2 = x*x; float x3 = x2*x;
	x = a*(1-x)*(1-x)*(1-x) + 3*b*(1-x)*(1-x)*x + 3*c*(1-x)*x*x + d*x*x*x;
#endif
#if Curves_formula == 23
    float3 a = float3(0.00,0.00,0.00); float3 b = float3(0.25,0.15,0.85);  float3 c = float3(0.75,0.85,0.15); float3 d = float3(1.00,1.00,1.00);
    float3 ab = lerp(a,b,x); float3 bc = lerp(b,c,x); float3 cd = lerp(c,d,x); float3 abbc = lerp(ab,bc,x); float3 bccd = lerp(bc,cd,x);
    float3 dest = lerp(abbc,bccd,x);
    x = dest;
#endif
#if Curves_formula == 24
   x = 1.0 / (1.0 + exp(-(x * 10.0 - 5.0)));
#endif
#if Curves_mode == 2
	float3 color = x;
	colorInput.rgb = lerp(colorInput.rgb, color, Curves_contrast_blend);
  #elif Curves_mode == 1
	x = x * 2.0 - 1.0;
	float3 color = luma + x;
	colorInput.rgb = lerp(colorInput.rgb, color, Curves_contrast_blend);
  #else
    x = lerp(luma, x, Curves_contrast_blend);
    colorInput.rgb = x + chroma;
#endif
  return colorInput;
}}"
);
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", "EndColor = EndColor = CurvesMain(vert, EndColor);\r\n");
                            break;

                        case "Levels":
                            HDRText = HDRText.AddBefore(ref success, "// Applies exposure and tone mapping to the input, and combines it with the",
$@"float4 LevelsMain(PsQuad vert, float4 color) : SV_Target
{{
const float Levels_black_point = {post.parameters[0].value};
const float Levels_white_point = {post.parameters[1].value};
const float black_point_float = ( Levels_black_point / 255.0 );
float white_point_float;
if (Levels_white_point == Levels_black_point)
  white_point_float = ( 255.0 / 0.00025);
else
  white_point_float = ( 255.0 / (Levels_white_point - Levels_black_point));
    uint2 uTDim, uDDim;
    srcTex.GetDimensions(uTDim.x,uTDim.y);
    int3 iTexCoord = int3(uTDim.x * vert.texcoord.x, uTDim.y * vert.texcoord.y, 0);
	float4 colorInput = color;
	colorInput.rgb = colorInput.rgb * white_point_float - (black_point_float *  white_point_float);
	return colorInput;
}}"
);
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", "EndColor = LevelsMain(vert, EndColor);\r\n");
                            break;

                        case "Lift Gamma Gain":
                            HDRText = HDRText.AddBefore(ref success, "// Applies exposure and tone mapping to the input, and combines it with the",
$@"float4 LiftGammaGainMain(PsQuad vert, float4 Inp_color) : SV_Target
{{
const float3 RGB_Lift = float3({post.parameters[0].value});
const float3 RGB_Gamma = float3({post.parameters[1].value});
const float3 RGB_Gain = float3({post.parameters[2].value});
    uint2 uTDim, uDDim;
    srcTex.GetDimensions(uTDim.x,uTDim.y);
    int3 iTexCoord = int3(uTDim.x * vert.texcoord.x, uTDim.y * vert.texcoord.y, 0);
	float4 colorInput = Inp_color;
	float3 color = colorInput.rgb;
	color = color * (1.5-0.5 * RGB_Lift) + 0.5 * RGB_Lift - 0.5;
	color = saturate(color);
	color *= RGB_Gain;
	colorInput.rgb = pow(color, 1.0 / RGB_Gamma);
	return saturate(colorInput);
}}"
);
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", "EndColor = LiftGammaGainMain(vert, EndColor);\r\n");
                            break;

                        case "Technicolor":
                            HDRText = HDRText.AddBefore(ref success, "// Applies exposure and tone mapping to the input, and combines it with the",
$@"#define cyanfilter float3(0.0, 1.30, 1.0)
#define magentafilter float3(1.0, 0.0, 1.05)
#define yellowfilter float3(1.6, 1.6, 0.05)
#define redorangefilter float2(1.05, 0.620)
#define greenfilter float2(0.30, 1.0)
#define magentafilter2 magentafilter.rb
float4 TechnicolorMain(PsQuad vert, float4 color) : SV_Target
{{
    const float TechniAmount = {post.parameters[0].value};
    const float TechniPower = {post.parameters[1].value};
    const float redNegativeAmount = {post.parameters[2].value};
    const float greenNegativeAmount = {post.parameters[3].value};
    const float blueNegativeAmount = {post.parameters[4].value};
    uint2 uTDim, uDDim;
    srcTex.GetDimensions(uTDim.x, uTDim.y);
    int3 iTexCoord = int3(uTDim.x * vert.texcoord.x, uTDim.y * vert.texcoord.y, 0);
    float4 colorInput = color;
    float3 tcol = colorInput.rgb;
    float2 rednegative_mul = tcol.rg * (1.0 / (redNegativeAmount * TechniPower));
    float2 greennegative_mul = tcol.rg * (1.0 / (greenNegativeAmount * TechniPower));
    float2 bluenegative_mul = tcol.rb * (1.0 / (blueNegativeAmount * TechniPower));
    float rednegative = dot(redorangefilter, rednegative_mul);
    float greennegative = dot(greenfilter, greennegative_mul);
    float bluenegative = dot(magentafilter2, bluenegative_mul);
    float3 redoutput = rednegative.rrr + cyanfilter;
    float3 greenoutput = greennegative.rrr + magentafilter;
    float3 blueoutput = bluenegative.rrr + yellowfilter;
    float3 result = redoutput * greenoutput * blueoutput;
    colorInput.rgb = lerp(tcol, result, TechniAmount);
    return colorInput;
}}
");
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", "EndColor = TechnicolorMain(vert, EndColor);\r\n");
                            break;

                        case "Vibrance":
                            HDRText = HDRText.AddBefore(ref success, "// Applies exposure and tone mapping to the input, and combines it with the",
$@"float4 VibranceMain(PsQuad vert, float4 Inp_color) : SV_Target
{{
const float Vibrance = {post.parameters[0].value};
const float3 Vibrance_RGB_balance = float3({post.parameters[1].value});
    uint2 uTDim, uDDim;
    srcTex.GetDimensions(uTDim.x,uTDim.y);
    int3 iTexCoord = int3(uTDim.x * vert.texcoord.x, uTDim.y * vert.texcoord.y, 0);
	float4 colorInput = Inp_color;
 float3 Vibrance_coeff = float3(Vibrance_RGB_balance * Vibrance);
	float4 color = colorInput;
	float3 lumCoeff = float3(0.212656, 0.715158, 0.072186);
	float luma = dot(lumCoeff, color.rgb);
	float max_color = max(colorInput.r, max(colorInput.g,colorInput.b));
	float min_color = min(colorInput.r, min(colorInput.g,colorInput.b));
	float color_saturation = max_color - min_color;
	color.rgb = lerp(luma, color.rgb, (1.0 + (Vibrance_coeff * (1.0 - (sign(Vibrance_coeff) * color_saturation)))));
	return color;
}}"
);
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", "EndColor = VibranceMain(vert, EndColor);\r\n");
                            break;

                        case "Cineon DPX":
                            HDRText = HDRText.AddBefore(ref success, "// Applies exposure and tone mapping to the input, and combines it with the",
$@"float4 DPXMain(PsQuad vert, float4 Inp_color) : SV_Target
{{
const float3x3 RGB = float3x3
(2.67147117265996,-1.26723605786241,-0.410995602172227,
-1.02510702934664,1.98409116241089,0.0439502493584124,
0.0610009456429445,-0.223670750812863,1.15902104167061);
const float3x3 XYZ = float3x3
(0.500303383543316,0.338097573222739,0.164589779545857,
0.257968894274758,0.676195259144706,0.0658358459823868,
0.0234517888692628,0.1126992737203,0.866839673124201);

const float DPX_ColorGamma = {post.parameters[1].value};
const float DPXSaturation = {post.parameters[2].value};

const float DPX_Blend = {post.parameters[4].value};
	uint2 uTDim, uDDim;
	srcTex.GetDimensions(uTDim.x,uTDim.y);
	int3 iTexCoord = int3(uTDim.x * vert.texcoord.x, uTDim.y * vert.texcoord.y, 0);
	float4 InputColor = Inp_color;
	float DPXContrast = 0.1;
	float DPXGamma = 1.0;
	float3 RGB_Curve = float3({post.parameters[0].value});
	float3 RGB_C = float3({post.parameters[3].value});
	float3 B = InputColor.rgb;
	B = pow(abs(B), 1.0/DPXGamma);
	B = B * (1.0 - DPXContrast) + (0.5 * DPXContrast);
    float3 Btemp = (1.0 / (1.0 + exp(RGB_Curve / 2.0)));
	B = ((1.0 / (1.0 + exp(-RGB_Curve * (B - RGB_C)))) / (-2.0 * Btemp + 1.0)) + (-Btemp / (-2.0 * Btemp + 1.0));
	float value = max(max(B.r, B.g), B.b);
	float3 color = B / value;
	color = pow(abs(color), 1.0/DPX_ColorGamma);
	float3 c0 = color * value;
	c0 = mul(XYZ, c0);
	float luma = dot(c0, float3(0.30, 0.59, 0.11));
    c0 = (1.0 - DPXSaturation) * luma + DPXSaturation * c0;
	c0 = mul(RGB, c0);
	InputColor.rgb = lerp(InputColor.rgb, c0, DPX_Blend);
	return InputColor;
}}"
);
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", "EndColor = DPXMain(vert, EndColor);\r\n");
                            break;

                        case "Tonemap":
                            HDRText = HDRText.AddBefore(ref success, "// Applies exposure and tone mapping to the input, and combines it with the",
$@"float4 TonemapMain(PsQuad vert, float4 Inp_color) : SV_Target
{{
const float Tonemap_Gamma = {post.parameters[0].value};
const float Tonemap_Exposure = {post.parameters[1].value};
const float Tonemap_Saturation = {post.parameters[2].value};
const float Tonemap_Bleach = {post.parameters[3].value};
const float Tonemap_Defog = {post.parameters[4].value};
const float3 Tonemap_FogColor = float3({post.parameters[5].value});
    uint2 uTDim, uDDim;
    srcTex.GetDimensions(uTDim.x,uTDim.y);
    int3 iTexCoord = int3(uTDim.x * vert.texcoord.x, uTDim.y * vert.texcoord.y, 0);
    float4 colorInput = Inp_color;
    float3 color = colorInput.rgb;
    color = saturate(color - Tonemap_Defog * Tonemap_FogColor);
    color *= pow(2.0f, Tonemap_Exposure);
    color = pow(color, Tonemap_Gamma);
    float3 lumCoeff = float3(0.2126, 0.7152, 0.0722);
    float lum = dot(lumCoeff, color.rgb);
    float3 blend = lum.rrr;
    float L = saturate( 10.0 * (lum - 0.45) );
    float3 result1 = 2.0f * color.rgb * blend;
    float3 result2 = 1.0f - 2.0f * (1.0f - blend) * (1.0f - color.rgb);
    float3 newColor = lerp(result1, result2, L);
    float3 A2 = Tonemap_Bleach * color.rgb;
    float3 mixRGB = A2 * newColor;
    color.rgb += ((1.0f - A2) * mixRGB);
    float3 middlegray = dot(color,(1.0/3.0));
    float3 diffcolor = color - middlegray;
    colorInput.rgb = (color + diffcolor * Tonemap_Saturation)/(1+(diffcolor * Tonemap_Saturation));
    return colorInput;
}}
");
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", "EndColor = TonemapMain(vert, EndColor);\r\n");
                            break;

                        case "Luma Sharpen":
                            HDRText = HDRText.AddBefore(ref success, "// Applies exposure and tone mapping to the input, and combines it with the",
$@"float4 LumaSharpenMain(PsQuad vert, float4 color) : SV_Target
{{
const float3 Luma_CoefLuma = float3(0.2126, 0.7152, 0.0722);
const float Luma_sharp_strength = {post.parameters[0].value};
const float Luma_sharp_clamp = {post.parameters[1].value};
const float Luma_pattern = {post.parameters[2].value};
const float Luma_offset_bias = {post.parameters[3].value};
const float Luma_show_sharpen = 0;
float3 blur_ori;
    uint2 uTDim, uDDim;
    srcTex.GetDimensions(uTDim.x,uTDim.y);
    int3 iTexCoord = int3(uTDim.x * vert.texcoord.x, uTDim.y * vert.texcoord.y, 0);
	float px = 1;
	float py = 1;
  float3 ori = color.rgb;
  float3 sharp_strength_luma = (Luma_CoefLuma * Luma_sharp_strength);
  if (Luma_pattern == 1) {{
    iTexCoord = int3(uTDim.x * vert.texcoord.x + px * 0.5 * Luma_offset_bias, uTDim.y * vert.texcoord.y - py * 0.5 * Luma_offset_bias, 0);
	   blur_ori = srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x - px * 0.5 * Luma_offset_bias, uTDim.y * vert.texcoord.y - py * 0.5 * Luma_offset_bias, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x + px * 0.5 * Luma_offset_bias, uTDim.y * vert.texcoord.y + py * 0.5 * Luma_offset_bias, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x - px * 0.5 * Luma_offset_bias, uTDim.y * vert.texcoord.y + py * 0.5 * Luma_offset_bias, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
	blur_ori *= 0.25;
 }}
 else if (Luma_pattern == 2) {{
    iTexCoord = int3(uTDim.x * vert.texcoord.x + px * 0.5 * Luma_offset_bias, uTDim.y * vert.texcoord.y - py * 0.5 * Luma_offset_bias, 0);
	   blur_ori = srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x - px * 0.5 * Luma_offset_bias, uTDim.y * vert.texcoord.y - py * 0.5 * Luma_offset_bias, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x + px * 0.5 * Luma_offset_bias, uTDim.y * vert.texcoord.y + py * 0.5 * Luma_offset_bias, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x - px * 0.5 * Luma_offset_bias, uTDim.y * vert.texcoord.y + py * 0.5 * Luma_offset_bias, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
	blur_ori *= 0.25;
 }}
 else if (Luma_pattern == 3) {{
	iTexCoord = int3(uTDim.x * vert.texcoord.x + px * 0.4 * Luma_offset_bias, uTDim.y * vert.texcoord.y - py * 1.2 * Luma_offset_bias, 0);
	blur_ori = srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x - px * 1.2 * Luma_offset_bias, uTDim.y * vert.texcoord.y - py * 0.4 * Luma_offset_bias, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x + px * 1.2 * Luma_offset_bias, uTDim.y * vert.texcoord.y + py * 0.4 * Luma_offset_bias, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x - px * 0.4 * Luma_offset_bias, uTDim.y * vert.texcoord.y + py * 1.2 * Luma_offset_bias, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
	blur_ori *= 0.25;
	sharp_strength_luma *= 0.51;
 }}
 else if (Luma_pattern == 4) {{
	iTexCoord = int3(uTDim.x * vert.texcoord.x + px * 0.5, uTDim.y * vert.texcoord.y - py * Luma_offset_bias, 0);
	blur_ori = srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x - px * 0.5 * Luma_offset_bias, uTDim.y * vert.texcoord.y - py * 0.5, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x + px * Luma_offset_bias, uTDim.y * vert.texcoord.y + py * 0.5, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
    iTexCoord = int3(uTDim.x * vert.texcoord.x - px * 0.5, uTDim.y * vert.texcoord.y + py * Luma_offset_bias, 0);
	blur_ori += srcTex.Load(iTexCoord).rgb;
	blur_ori /= 4.0;
	sharp_strength_luma *= 0.666;
 }}
	float3 sharp = ori - blur_ori;
	float4 sharp_strength_luma_clamp = float4(sharp_strength_luma * (0.5 / Luma_sharp_clamp),0.5);
	float sharp_luma = saturate(dot(float4(sharp,1.0), sharp_strength_luma_clamp));
	sharp_luma = (Luma_sharp_clamp * 2.0) * sharp_luma - Luma_sharp_clamp;
	color.rgb = ori + sharp_luma;
	return color;
}}"
);
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", "EndColor = LumaSharpenMain(vert, EndColor);\r\n");
                            break;

                    }

                    if (!success)
                    {
                        Log(ErrorType.Error, "Failed to apply post process [" + post.name + "] in " + FileIO.HDRFile + " file.");
                        return;
                    }
                    else
                    {
                        Log(ErrorType.None, "Post process [" + post.name + "] applied.");
                    }
                }
            }

            try
            {
                File.WriteAllText(shaderDirectory + FileIO.cloudFile, cloudText);
                File.WriteAllText(shaderDirectory + FileIO.generalFile, generalText);
                File.WriteAllText(shaderDirectory + FileIO.shadowFile, shadowText);
                File.WriteAllText(shaderDirectory + FileIO.funclibFile, funclibText);
                File.WriteAllText(shaderDirectory + FileIO.terrainFile, terrainText);
                File.WriteAllText(shaderDirectory + FileIO.terrainFXHFile, terrainFXHText);
                File.WriteAllText(shaderDirectory + "PostProcess\\" + FileIO.HDRFile, HDRText);
            }
            catch
            {
                Log(ErrorType.Error, "Could not write tweaks to shader files.");
                return;
            }

            try
            {
                fileData.ClearDirectory(cacheDirectory);
                Log(ErrorType.None, "Shader cache cleared");
            }
            catch
            {
                Log(ErrorType.Error, "Could not clear shader cache.");
                return;
            }

            Log(ErrorType.None, "Preset [" + presetName + "] applied");

        }

        private void ResetShaderFiles(object sender, RoutedEventArgs e)
        {
            if (fileData.CopyShaderFiles(backupDirectory, shaderDirectory)) { 
                Log(ErrorType.None, "Shader files restored");
            }            
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tweak in tweaks) {
                if (tweak.Value.parameters != null)
                {
                    foreach (var param in tweak.Value.parameters)
                    {
                        param.value = param.defaultValue;
                    }
                }
            }

            foreach (var post in postProcesses)
            {
                if (post.Value.parameters != null)
                {
                    foreach (var param in post.Value.parameters)
                    {
                        param.value = param.defaultValue;
                    }
                }
            }
        }

        private void ClearShaders_Click(object sender, RoutedEventArgs e) {
            try
            {
                fileData.ClearDirectory(cacheDirectory);
                Log(ErrorType.None, "Shader cache cleared");
            }
            catch
            {
                Log(ErrorType.Error, "Could not clear shader cache.");
                return;
            }
        }

        public void Log(ErrorType type, string message)
        {

            string typeString = "";
            SolidColorBrush color = Brushes.Black;

            switch (type)
            {
                case ErrorType.None:
                    typeString = "Success";
                    color = Brushes.Green;
                    break;
                case ErrorType.Warning:
                    typeString = "Warning";
                    color = Brushes.Orange;
                    break;
                case ErrorType.Error:
                    typeString = "Error";
                    color = Brushes.Red;
                    break;
            }


            Paragraph para = new Paragraph();
            para.Inlines.Add(new Bold(new Run(DateTime.Now.ToLongTimeString() + " - ")));
            para.Inlines.Add(new Bold(new Run(typeString)) { Foreground = color });
            para.Inlines.Add(new Run(": " + message));

            Log_RichTextBox.Document.Blocks.Add(para);
            Log_RichTextBox.ScrollToEnd();
        }
    }


}
