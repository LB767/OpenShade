using Microsoft.Win32;
using OpenShade.Controls;
using OpenShade.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace OpenShade
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public enum ErrorType { None, Warning, Error };


    public partial class MainWindow : Window
    {
        string tweaksHash;
        string customTweaksHash;
        string postProcessesHash;
        string commentHash; // that's just the original comments

        /*
         * If a list contains the same items for their whole lifetime, but the individual objects within that list change, 
         * then it's enough for just the objects to raise change notifications (typically through INotifyPropertyChanged) and List<T> is sufficient. 
         * But if the list contains different objects from time to time, or if the order changes, then you should use ObservableCollection<T>.
         */
        List<Tweak> tweaks;
        ObservableCollection<CustomTweak> customTweaks;
        List<PostProcess> postProcesses;
        string comment;

        const string P3DRegistryPath = "HKEY_LOCAL_MACHINE\\SOFTWARE\\Lockheed Martin\\Prepar3D v4";
        string cacheDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Lockheed Martin\\Prepar3D v4\\Shaders\\";
        string currentDirectory = Directory.GetCurrentDirectory();

        FileIO fileData;
        string shaderDirectory;
        public string backupDirectory;

        public string activePresetPath;
        IniFile activePreset;
        public string loadedPresetPath;
        IniFile loadedPreset;

        // TODO: put this in a struct somewhere
        public static string cloudText, generalText, terrainText, funclibText, terrainFXHText, shadowText, HDRText;

        public MainWindow()
        {
            InitializeComponent();

            Log_RichTextBox.Document.Blocks.Clear();

            // Init
            tweaks = new List<Tweak>() { };
            customTweaks = new ObservableCollection<CustomTweak>() { };
            postProcesses = new List<PostProcess>() { };
            comment = "";

            Tweak.GenerateTweaksData(tweaks);
            PostProcess.GeneratePostProcessData(postProcesses);

            Tweak_List.ItemsSource = tweaks;
            CustomTweak_List.ItemsSource = customTweaks;
            PostProcess_List.ItemsSource = postProcesses;

            CollectionView tweaksView = (CollectionView)CollectionViewSource.GetDefaultView(Tweak_List.ItemsSource);
            PropertyGroupDescription groupDescription = new PropertyGroupDescription("category");
            tweaksView.GroupDescriptions.Add(groupDescription);

            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.cloudFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.generalFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.terrainFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.funclibFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.terrainFXHFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.shadowFile);
            CustomTweakShaderFile_ComboBox.Items.Add(FileIO.HDRFile);

            fileData = new FileIO(this);

            tweaksHash = HelperFunctions.GetDictHashCode(tweaks);
            customTweaksHash = HelperFunctions.GetDictHashCode(customTweaks);
            postProcessesHash = HelperFunctions.GetDictHashCode(postProcesses);
            commentHash = comment;


            // Shaders files
            string P3DDirectory = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Lockheed Martin\Prepar3D v4", "AppPath", null);
            if (P3DDirectory == null)
            {
                Log(ErrorType.Error, "Prepar3D v4 path not found");
                ChangeMenuBarState(false);
                return;
            }

            int index = P3DDirectory.IndexOf('\0');
            if (index >= 0) { P3DDirectory = P3DDirectory.Substring(0, index); }

            P3DMain_TextBox.Text = P3DDirectory;

            shaderDirectory = P3DDirectory + "ShadersHLSL\\";

            if (!Directory.Exists(shaderDirectory))
            {
                Log(ErrorType.Error, "P3D shader directory not found");
                ChangeMenuBarState(false);
                return;
            }
            P3DShaders_TextBox.Text = shaderDirectory;

            if (!Directory.Exists(cacheDirectory))
            {
                Log(ErrorType.Error, "Shader cache directory not found");
                ChangeMenuBarState(false);
                return;
            }
            ShaderCache_TextBox.Text = cacheDirectory;

            backupDirectory = currentDirectory + "\\Backup Shaders\\"; // Default directory
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load settings first
            if (File.Exists(currentDirectory + "\\" + FileIO.settingsFile))
            {
                fileData.LoadSettings(currentDirectory + "\\" + FileIO.settingsFile);
            }

            // Load preset
            if (activePresetPath != null)
            {
                if (File.Exists(activePresetPath))
                {
                    try
                    {
                        activePreset = new IniFile(activePresetPath);
                        loadedPreset = activePreset;
                        LoadedPreset_Label.Content = loadedPreset.filename;
                        LoadPreset(activePreset, false);
                        Log(ErrorType.None, "Preset [" + activePreset.filename + "] loaded");
                    }
                    catch (Exception ex)
                    {
                        Log(ErrorType.Error, "Failed to load preset file [" + activePresetPath + "]. " + ex.Message);
                    }
                }
                else
                {
                    Log(ErrorType.Error, "Active Preset file [" + activePresetPath + "] not found");
                }
            }
       
            // Load Theme
            Theme_ComboBox.ItemsSource = Enum.GetValues(typeof(Themes)).Cast<Themes>();
            Theme_ComboBox.SelectedItem = ((App)Application.Current).CurrentTheme;
            
            ShaderBackup_TextBox.Text = backupDirectory;

            if (Directory.Exists(backupDirectory))
            {
                if (fileData.CheckShaderBackup(backupDirectory))
                {
                    fileData.LoadShaderFiles(backupDirectory);
                }
                else
                {
                    Log(ErrorType.Error, "Missing shader files in " + backupDirectory + ". OpenShade can not run");
                    ChangeMenuBarState(false);
                }
            }
            else {

                if (Directory.Exists(shaderDirectory)) // This better be true
                {
                    MessageBoxResult result = MessageBox.Show("OpenShade will backup your Prepar3D shaders now.\r\nMake sure the files are the original ones or click 'Cancel' and manually select your backup folder in the application settings.", "Backup", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation, MessageBoxResult.OK); // TODO: Localization
                    if (result == MessageBoxResult.OK)
                    {
                        Directory.CreateDirectory("Backup Shaders");
                        if (fileData.CopyShaderFiles(shaderDirectory, backupDirectory))
                        {
                            Log(ErrorType.None, "Shaders backed up");                            
                        }
                        else
                        {
                            Log(ErrorType.Warning, "Shaders could not be backed up. OpenShade can not run.");
                            ChangeMenuBarState(false);
                        }
                    }
                    else
                    {
                        Log(ErrorType.Warning, "Shaders were not backed up. OpenShade can not run.");
                        ChangeMenuBarState(false);
                    }
                }
            }        
        }

        private void Window_Closed(object sender, EventArgs e) // important to use Closed() and not Closing() because this has to happen after any LostFocus() event to have all up-to-date parameters
        {
            if (HelperFunctions.GetDictHashCode(tweaks) != tweaksHash ||
                HelperFunctions.GetDictHashCode(customTweaks) != customTweaksHash || 
                HelperFunctions.GetDictHashCode(postProcesses) != postProcessesHash || 
                comment != commentHash)
            {
                MessageBoxResult result = MessageBox.Show("Some changes for the preset [" + loadedPreset.filename + "] were not saved.\r\nWould you like to save them now?", "Save", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (result == MessageBoxResult.Yes)
                {
                    SavePreset_Click(null, null);
                }
            }

            fileData.SaveSettings(currentDirectory + "\\" + FileIO.settingsFile);
        }


        #region MainTweaks
        private void TweakList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            List_SelectionChanged(typeof(Tweak), Tweak_List, TweakStack, TweakClearStack, TweakTitleTextblock, TweakDescriptionTextblock);
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
            else
            {
                CustomTweaks_Grid.Visibility = Visibility.Collapsed;
            }
        }

        private void AddCustomTweak(object sender, RoutedEventArgs e)
        {
            if (AddCustomTweak_TextBox.Text != "")
            {
                customTweaks.Add(new CustomTweak("CUSTOM_TWEAK" + (customTweaks.Count).ToString(), AddCustomTweak_TextBox.Text, FileIO.cloudFile, customTweaks.Count, "", "", false));
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
                var item = customTweaks.First(p => p == selectedTweak); // Not the best
                customTweaks.Remove(item);
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

        // DRAG & DROP ----------------------------------------------------
        private void PreviewMouseMoveEventHandler(object sender, MouseEventArgs e)
        {
            if (sender is ListViewItem && e.LeftButton == MouseButtonState.Pressed)
            {
                ListViewItem draggedItem = sender as ListViewItem;
                DragDrop.DoDragDrop(draggedItem, draggedItem.DataContext, DragDropEffects.Move);
                draggedItem.IsSelected = true;
            }

        }

        private void DropEventHandler(object sender, DragEventArgs e)
        {
            PostProcess droppedData = e.Data.GetData(typeof(PostProcess)) as PostProcess;
            PostProcess target = ((ListViewItem)sender).DataContext as PostProcess;

            int removedIdx = PostProcess_List.Items.IndexOf(droppedData);
            int targetIdx = PostProcess_List.Items.IndexOf(target);

            if (removedIdx < targetIdx)
            {
                postProcesses.Insert(targetIdx + 1, droppedData);
                postProcesses.RemoveAt(removedIdx);
            }
            else
            {
                int remIdx = removedIdx + 1;
                if (postProcesses.Count + 1 > remIdx)
                {
                    postProcesses.Insert(targetIdx, droppedData);
                    postProcesses.RemoveAt(remIdx);
                }
            }

            PostProcess_List.Items.Refresh();
        }

        #endregion

        #region PostProcesses        
        private void PostProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            List_SelectionChanged(typeof(PostProcess), PostProcess_List, PostProcessStack, PostClearStack, PostTitleTextblock, PostDescriptionTextblock);
        }
        #endregion

        #region ParametersUpdates
        private void List_SelectionChanged(Type type, ListView itemListview, StackPanel StackGrid, StackPanel clearStack, Label titleBlock, TextBlock descriptionBlock)
        {
            if (itemListview.SelectedItem != null)
            {
                StackGrid.Children.Clear();
                clearStack.Children.Clear();

                BaseTweak selectedEffect = (BaseTweak)itemListview.SelectedItem;                

                titleBlock.Content = selectedEffect.name;
                descriptionBlock.Text = selectedEffect.description;

                if (selectedEffect.parameters.Count > 0)
                {
                    Button resetButton = new Button();
                    resetButton.Content = "Reset default";
                    resetButton.ToolTip = "Reset parameters to their default value";
                    resetButton.Width = 100;
                    resetButton.Height = 25;
                    resetButton.VerticalAlignment = VerticalAlignment.Top;
                    resetButton.HorizontalAlignment = HorizontalAlignment.Right;
                    resetButton.Margin = new Thickness(0, 0, 0, 10);
                    resetButton.Click += new RoutedEventHandler(ResetParameters_Click);

                    clearStack.Children.Add(resetButton);

                    Button clearButton = new Button();
                    clearButton.Content = "Reset preset";
                    clearButton.ToolTip = "Reset parameters to their value in the active preset";
                    clearButton.Width = 100;
                    clearButton.Height = 25;
                    clearButton.VerticalAlignment = VerticalAlignment.Top;
                    clearButton.HorizontalAlignment = HorizontalAlignment.Right;
                    clearButton.Click += new RoutedEventHandler(ResetParametersPreset_Click);

                    clearStack.Children.Add(clearButton);

                    foreach (Parameter param in selectedEffect.parameters)
                    {
                        StackPanel rowStack = new StackPanel();
                        rowStack.Orientation = Orientation.Horizontal;

                        TextBlock txtBlock = new TextBlock();
                        txtBlock.Text = param.name;
                        txtBlock.TextWrapping = TextWrapping.Wrap;
                        txtBlock.Width = 170;
                        txtBlock.Height = 30;
                        txtBlock.Margin = new Thickness(0, 0, 10, 0);                        

                        rowStack.Children.Add(txtBlock);

                        if (param.control == UIType.Checkbox)
                        {
                            CheckBox checkbox = new CheckBox();
                            checkbox.IsChecked = ((param.value == "1") ? true : false);
                            checkbox.Uid = param.id;
                            checkbox.VerticalAlignment = VerticalAlignment.Center;
                            checkbox.Click += new RoutedEventHandler(Checkbox_Click);
                            rowStack.Children.Add(checkbox);
                        }
                        else if (param.control == UIType.RGB)
                        {
                            var group = new GroupBox();
                            group.Header = "RGB";

                            var container = new StackPanel();
                            container.Orientation = Orientation.Horizontal;

                            var Rtext = new NumericSpinner();
                            Rtext.Uid = param.id + "_R";
                            Rtext.Height = 25;
                            Rtext.Width = 70;
                            Rtext.Value = decimal.Parse(param.value.Split(',')[0]);
                            Rtext.MinValue = param.min;
                            Rtext.MaxValue = param.max;
                            Rtext.LostFocus += new RoutedEventHandler(RGB_LostFocus);

                            var Gtext = new NumericSpinner();
                            Gtext.Uid = param.id + "_G";
                            Gtext.Height = 25;
                            Gtext.Width = 70;
                            Gtext.Value = decimal.Parse(param.value.Split(',')[1]);
                            Gtext.MinValue = param.min;
                            Gtext.MaxValue = param.max;
                            Gtext.LostFocus += new RoutedEventHandler(RGB_LostFocus);

                            var Btext = new NumericSpinner();
                            Btext.Uid = param.id + "_B";
                            Btext.Height = 25;
                            Btext.Width = 70;
                            Btext.Value = decimal.Parse(param.value.Split(',')[2]);
                            Btext.MinValue = param.min;
                            Btext.MaxValue = param.max;
                            Btext.LostFocus += new RoutedEventHandler(RGB_LostFocus);

                            container.Children.Add(Rtext);
                            container.Children.Add(Gtext);
                            container.Children.Add(Btext);

                            group.Content = container;

                            rowStack.Children.Add(group);
                        }

                        else if (param.control == UIType.Text)
                        {
                            var spinner = new NumericSpinner();
                            spinner.Uid = param.id;
                            spinner.Width = 170;
                            spinner.Height = 25;
                            spinner.Decimals = 10;
                            spinner.MinValue = param.min;
                            spinner.MaxValue = param.max;
                            spinner.Step = 0.1m;                           
                            spinner.LostFocus += new RoutedEventHandler(ParameterSpinner_LostFocus);

                            var item = new MenuItem();
                            item.Header = "Make Custom";
                            item.SetResourceReference(Control.ForegroundProperty, "TextColor");
                            item.Tag = spinner;
                            item.Click += ParameterSwitch_Click;
                            spinner.ContextMenu = new ContextMenu();
                            spinner.ContextMenu.Items.Add(item);


                            var txtbox = new TextBox();
                            txtbox.Uid = param.id;
                            txtbox.Width = 170;
                            txtbox.Height = 50;
                            txtbox.VerticalContentAlignment = VerticalAlignment.Top;
                            //spinner.TextWrapping = TextWrapping.Wrap;
                            txtbox.Text = param.value;
                            txtbox.LostFocus += new RoutedEventHandler(ParameterText_LostFocus);

                            item = new MenuItem();
                            item.Header = "Make Default";
                            item.SetResourceReference(Control.ForegroundProperty, "TextColor");
                            item.Tag = txtbox;
                            item.Click += ParameterSwitch_Click;
                            txtbox.ContextMenu = new ContextMenu();
                            txtbox.ContextMenu.Items.Add(item);

                            decimal val;
                            if (decimal.TryParse(param.value, out val))
                            {
                                spinner.Value = val;
                                txtbox.Visibility = Visibility.Collapsed;
                            }
                            else
                            {
                                spinner.Value = decimal.Parse(param.defaultValue);
                                spinner.Visibility = Visibility.Collapsed;
                            }

                            rowStack.Children.Add(spinner);
                            rowStack.Children.Add(txtbox);
                        }

                        else if (param.control == UIType.Combobox)
                        {
                            var combo = new ComboBox();
                            combo.Uid = param.id;
                            combo.Width = 170;
                            combo.Height = 25;
                            combo.SetResourceReference(Control.ForegroundProperty, "TextColor");
                            foreach (var item in param.range)
                            {
                                combo.Items.Add(item);
                            }
                            combo.SelectedIndex = int.Parse(param.value);
                            combo.SelectionChanged += new SelectionChangedEventHandler(Combobox_SelectionChanged);

                            rowStack.Children.Add(combo);
                        }

                        if (param.hasChanged)
                        {
                            TextBox changeTxtbox = new TextBox();                           
                            changeTxtbox.IsReadOnly = true;
                            changeTxtbox.Background = Brushes.Transparent;
                            changeTxtbox.Foreground = Brushes.Orange;
                            changeTxtbox.BorderThickness = new Thickness(0);
                            changeTxtbox.Width = 170;
                            changeTxtbox.Height = 30;
                            changeTxtbox.Margin = new Thickness(10, 0, 10, 0);
                            
                            if (param.control == UIType.Checkbox)
                            {
                                changeTxtbox.Text = (param.oldValue == "1") ? "Enabled" : "Disabled";
                            }
                            else {
                                changeTxtbox.Text = param.oldValue;
                            }

                            rowStack.Children.Add(changeTxtbox);
                        }                        

                        StackGrid.Children.Add(rowStack);
                    }
                }
                else
                {
                    Label label = new Label();
                    label.Content = "No additional parameters";
                    StackGrid.Children.Add(label);
                }                
            }
        }

        private void Checkbox_Click(object sender, EventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            TabItem currentTab = HelperFunctions.FindAncestorOrSelf<TabItem>(checkbox);
            Parameter param = null;

            if (currentTab.Name == "Tweak_Tab") { param = ((Tweak)Tweak_List.SelectedItem).parameters.First(p => p.id == checkbox.Uid); }
            if (currentTab.Name == "Post_Tab") { param = ((PostProcess)PostProcess_List.SelectedItem).parameters.First(p => p.id == checkbox.Uid); }         

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
            TextBox txtBox = (TextBox)sender;
            TabItem currentTab = HelperFunctions.FindAncestorOrSelf<TabItem>(txtBox);            
            Parameter param = null;

            if (currentTab.Name == "Tweak_Tab") { param = ((Tweak)Tweak_List.SelectedItem).parameters.First(p => p.id == txtBox.Uid); }
            if (currentTab.Name == "Post_Tab") { param = ((PostProcess)PostProcess_List.SelectedItem).parameters.First(p => p.id == txtBox.Uid); }
            if (currentTab.Name == "Custom_Tab") { ((CustomTweak)CustomTweak_List.SelectedItem).name = txtBox.Text; } // NOTE: Maybe unify this to behave like tweaks and post-processes

            if (currentTab.Name != "Custom_Tab") { param.value = txtBox.Text; }
        }

        private void ParameterSpinner_LostFocus(object sender, EventArgs e)
        {
            NumericSpinner spinner = (NumericSpinner)sender;
            TabItem currentTab = HelperFunctions.FindAncestorOrSelf<TabItem>(spinner);
            Parameter param = null;

            if (currentTab.Name == "Tweak_Tab") { param = ((Tweak)Tweak_List.SelectedItem).parameters.First(p => p.id == spinner.Uid); }
            if (currentTab.Name == "Post_Tab") { param = ((PostProcess)PostProcess_List.SelectedItem).parameters.First(p => p.id == spinner.Uid); }            

            if (currentTab.Name != "Custom_Tab") { param.value = spinner.Value.ToString(); }
        }

        private void ParameterSwitch_Click(object sender, EventArgs e)
        {
            MenuItem item = (MenuItem)sender;

            TabItem currentTab = HelperFunctions.FindAncestorOrSelf<TabItem>((DependencyObject)item.Tag);
            StackPanel currentStack = null;

            if (currentTab.Name == "Tweak_Tab") { currentStack = TweakStack; }
            if (currentTab.Name == "Post_Tab") { currentStack = PostProcessStack; }            

            if (item.Tag.GetType() == typeof(NumericSpinner))
            {
                NumericSpinner control = (NumericSpinner)item.Tag;
                int index = currentStack.Children.IndexOf(control);

                control.Visibility = Visibility.Collapsed;
                currentStack.Children[index + 1].Visibility = Visibility.Visible;
            }

            else if (item.Tag.GetType() == typeof(TextBox))
            {
                TextBox control = (TextBox)item.Tag;
                int index = currentStack.Children.IndexOf(control);

                control.Visibility = Visibility.Collapsed;
                currentStack.Children[index - 1].Visibility = Visibility.Visible;
            }
        }

        private void RGB_LostFocus(object sender, EventArgs e)
        {
            NumericSpinner spinner = (NumericSpinner)sender;

            string uid = spinner.Uid.Split('_')[0];
            string channel = spinner.Uid.Split('_')[1];

            TabItem currentTab = HelperFunctions.FindAncestorOrSelf<TabItem>(spinner);
            Parameter param = null;

            if (currentTab.Name == "Tweak_Tab") { param = ((Tweak)Tweak_List.SelectedItem).parameters.First(p => p.id == uid); }
            if (currentTab.Name == "Post_Tab") { param = ((PostProcess)PostProcess_List.SelectedItem).parameters.First(p => p.id == uid); }
            
            string oldR = param.value.Split(',')[0];
            string oldG = param.value.Split(',')[1];
            string oldB = param.value.Split(',')[2];

            switch (channel)
            {
                case "R":
                    param.value = spinner.Value.ToString() + "," + oldG + "," + oldB;
                    break;
                case "G":
                    param.value = oldR + "," + spinner.Value.ToString() + "," + oldB;
                    break;
                case "B":
                    param.value = oldR + "," + oldG + "," + spinner.Value.ToString();
                    break;
            }
        }

        private void Combobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            TabItem currentTab = HelperFunctions.FindAncestorOrSelf<TabItem>(combo);
            Parameter param = null;

            if (currentTab.Name == "Tweak_Tab") { param = ((Tweak)Tweak_List.SelectedItem).parameters.First(p => p.id == combo.Uid); }
            if (currentTab.Name == "Post_Tab") { param = ((PostProcess)PostProcess_List.SelectedItem).parameters.First(p => p.id == combo.Uid); }
            if (currentTab.Name == "Custom_Tab") { ((CustomTweak)CustomTweak_List.SelectedItem).name = combo.Text; }

            if (currentTab.Name != "Custom_Tab") { param.value = combo.SelectedIndex.ToString(); }
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

            ListView currentList = null;

            if (currentTab.Name == "Tweak_Tab") { currentList = Tweak_List; }
            if (currentTab.Name == "Post_Tab") { currentList = PostProcess_List; }

            BaseTweak selectedEffect = (BaseTweak)currentList.SelectedItem;

            foreach (var param in selectedEffect.parameters)
            {
                param.value = param.defaultValue;
            }

            if (currentTab.Name == "Tweak_Tab") { List_SelectionChanged(typeof(Tweak), Tweak_List, TweakStack, TweakClearStack, TweakTitleTextblock, TweakDescriptionTextblock); ; }
            if (currentTab.Name == "Post_Tab") { List_SelectionChanged(typeof(PostProcess), PostProcess_List, PostProcessStack, PostClearStack, PostTitleTextblock, PostDescriptionTextblock); }
        }

        private void ResetParametersPreset_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            TabItem currentTab = HelperFunctions.FindAncestorOrSelf<TabItem>(btn);

            ListView currentList = null;

            if (currentTab.Name == "Tweak_Tab") { currentList = Tweak_List; }
            if (currentTab.Name == "Post_Tab") { currentList = PostProcess_List; }            

            BaseTweak selectedEffect = (BaseTweak)currentList.SelectedItem;

            foreach (var param in selectedEffect.parameters)
            {
                param.value = param.oldValue;
            }

            if (currentTab.Name == "Tweak_Tab") { List_SelectionChanged(typeof(Tweak), Tweak_List, TweakStack, TweakClearStack, TweakTitleTextblock, TweakDescriptionTextblock); ; }
            if (currentTab.Name == "Post_Tab") { List_SelectionChanged(typeof(PostProcess), PostProcess_List, PostProcessStack, PostClearStack, PostTitleTextblock, PostDescriptionTextblock); }            
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

        private void ShaderBackup_Button_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog dlg = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();

            dlg.IsFolderPicker = true;
            dlg.Multiselect = false;
            dlg.InitialDirectory = Directory.GetCurrentDirectory();
            dlg.Title = "Browse Backup Shader directory";
                       
            var result = dlg.ShowDialog();

            if (result == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
            {
                backupDirectory = dlg.FileName + "\\";
                if (fileData.CheckShaderBackup(backupDirectory))
                {
                    ChangeMenuBarState(true);
                    ShaderBackup_TextBox.Text = backupDirectory;
                    Log(ErrorType.None, "All shader files found");
                    Log(ErrorType.None, "Backup directory set to " + backupDirectory);
                }
                else {
                    ChangeMenuBarState(false);
                    ShaderBackup_TextBox.Text = backupDirectory;
                    Log(ErrorType.Error, "Missing shader files in " + backupDirectory + ". OpenShade can not run");
                }
            }
        }
        #endregion


        private void NewPreset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tweak in tweaks)
            {
                tweak.isEnabled = false;
                
                foreach (var param in tweak.parameters)
                {
                    param.value = param.defaultValue;
                }                
            }

            foreach (var post in postProcesses)
            {
                post.isEnabled = false;
                
                foreach (var param in post.parameters)
                {
                    param.value = param.defaultValue;
                }                
            }

            PresetComments_TextBox.Text = "";
            customTweaks.Clear();
            CustomTweak_List.Items.Refresh();

            loadedPresetPath = currentDirectory + "\\custom_preset.ini";
            loadedPreset = new IniFile(loadedPresetPath);
            LoadedPreset_Label.Content = loadedPreset.filename;

            Log(ErrorType.None, "New Preset [" + loadedPreset.filename + "] created");
        }

        private void OpenPreset_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            OpenFileDialog dlg = new OpenFileDialog();

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
                string backupPresetPath = loadedPresetPath; // used as backup in case the following load fails

                try
                {
                    loadedPresetPath = dlg.FileName;
                    loadedPreset = new IniFile(loadedPresetPath);
                    LoadedPreset_Label.Content = loadedPreset.filename;
                    LoadPreset(loadedPreset, true);
                    Log(ErrorType.None, "Preset [" + loadedPreset.filename + "] loaded");
                }
                catch (Exception ex)
                {
                    Log(ErrorType.Error, "Failed to load preset file [" + loadedPresetPath + "]. " + ex.Message);

                    // Revert to previous preset
                    loadedPresetPath = backupPresetPath;
                    loadedPreset = new IniFile(backupPresetPath);
                    LoadedPreset_Label.Content = loadedPreset.filename;
                    LoadPreset(loadedPreset, true);                    
                }    
            }
        }

        private void LoadPreset(IniFile preset, bool monitorChanges)
        {             
            fileData.LoadTweaks(tweaks, preset, monitorChanges);
            fileData.LoadCustomTweaks(customTweaks, preset, monitorChanges);
            fileData.LoadPostProcesses(postProcesses, preset, monitorChanges);
            PresetComments_TextBox.Text = fileData.LoadComments(preset);

            tweaksHash = HelperFunctions.GetDictHashCode(tweaks);
            customTweaksHash = HelperFunctions.GetDictHashCode(customTweaks);
            postProcessesHash = HelperFunctions.GetDictHashCode(postProcesses);
            commentHash = comment;

            Tweak_List.Items.Refresh();
            PostProcess_List.Items.Refresh();
            CustomTweak_List.Items.Refresh();                     
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                comment = PresetComments_TextBox.Text;
                fileData.SavePreset(tweaks, customTweaks, postProcesses, comment, loadedPreset);

                tweaksHash = HelperFunctions.GetDictHashCode(tweaks);
                customTweaksHash = HelperFunctions.GetDictHashCode(customTweaks);
                postProcessesHash = HelperFunctions.GetDictHashCode(postProcesses);
                commentHash = comment;

                Log(ErrorType.None, "Preset [" + loadedPreset.filename + "] saved in " + loadedPreset.filepath);
            }
            catch (Exception ex)
            {
                Log(ErrorType.Error, "Failed to save preset file [" + loadedPreset.filename + "]. " + ex.Message);
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
                string newPresetPath = dlg.FileName;
                IniFile newPreset = new IniFile(newPresetPath);
                try
                {
                    comment = PresetComments_TextBox.Text;
                    fileData.SavePreset(tweaks, customTweaks, postProcesses, comment, newPreset);

                    tweaksHash = HelperFunctions.GetDictHashCode(tweaks);
                    customTweaksHash = HelperFunctions.GetDictHashCode(customTweaks);
                    postProcessesHash = HelperFunctions.GetDictHashCode(postProcesses);
                    commentHash = comment;

                    loadedPresetPath = newPresetPath;
                    loadedPreset = newPreset;
                    LoadedPreset_Label.Content = loadedPreset.filename;
                    Log(ErrorType.None, "Preset [" + loadedPreset.filename + "] saved in " + loadedPreset.filepath);
                }
                catch (Exception ex)
                {
                    Log(ErrorType.Error, "Failed to save preset file [" + loadedPreset.filename + "]. " + ex.Message);
                }
            }
        }


        private void ApplyPreset(object sender, RoutedEventArgs e)
        {

            fileData.LoadShaderFiles(backupDirectory); // Always load the unmodified files;

            // NOTE: Not sure what is the best way to implement this... for now just handle each tweak on a case by case basis, which is a lot of code but fine for now
            // NOTE: This code is getting more awful by the minute

            int tweakCount = 0;
            int customCount = 0;
            int postCount = 0;

            foreach (var tweak in tweaks)
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
                            cloudText = cloudText.CommentOut(ref success, "if (fIntensity < -cb_mMedianLine)", "    fIntensity = clamp(fIntensity, 0, 1);", false);
                            cloudText = cloudText.AddBefore(ref success, "/*if (fIntensity < -cb_mMedianLine)", "fIntensity =  saturate(" + tweak.parameters[0].value.ToString() + " * fIntensity + " + tweak.parameters[1].value.ToString() + ");\r\n");

                            if (tweak.parameters[2].value == "1")
                            {
                                cloudText = cloudText.CommentOut(ref success, "float height = corner.y;", "float4 color = lerp(baseColor, topColor, s);", true);
                                cloudText = cloudText.ReplaceAll(ref success, "float4 colorIntensity = float4(fColor.r, fColor.g, fColor.b, saturate(alpha)) * color;", "float4 colorIntensity = float4(fColor.r, fColor.g, fColor.b, saturate(alpha));");
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
                        // NOTE: Object lighting and Aircraft lighting and saturation interract quite heavily with each other
                        // Making something clearer might be a decent idea to avoid headaches...
                        case "Objects lighting":
                            currentFile = FileIO.generalFile;

                            string aircraftLighting = "";
                            Tweak aircraft = tweaks.First(p => p.name == "Aircraft lighting and saturation");
                            if (aircraft.isEnabled)
                            {
                                aircraftLighting = "&&& ADD THE AIRCRAFT LIGHTING TWEAK HERE &&&";
                            }                  
                           
                            generalText = generalText.ReplaceAll(ref success, @"cDiffuse = cBase * (float4( saturate( 
                (cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + 
                (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);",

         $@"if (cb_mObjectType != 19) cDiffuse = cBase * (float4( saturate((cb_mSun.mAmbient.xyz * {tweak.parameters[0].value} + (shadowContrib * (sunDiffuse * {tweak.parameters[1].value} * fDotSun))) + 
			  (cb_mMoon.mAmbient.xyz * {tweak.parameters[2].value} + (shadowContrib * (moonDiffuse * {tweak.parameters[3].value} * fDotMoon)))), 1) + cDiffuse);
			{aircraftLighting}
            else
			  cDiffuse = cBase * (float4( saturate( (cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);");
                            
                            break;

                        case "Aircraft lighting and saturation":
                            currentFile = FileIO.generalFile;

                            string replaceText = @"cDiffuse = cBase * (float4( saturate( 
                (cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + 
                (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);";
                            string elseText = "";
                            string finalText = @"else
			  cDiffuse = cBase * (float4( saturate( (cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);";

                            if (generalText.IndexOf("&&& ADD THE AIRCRAFT LIGHTING TWEAK HERE &&&") >= 0)
                            {
                                replaceText = "&&& ADD THE AIRCRAFT LIGHTING TWEAK HERE &&&";
                                elseText = "else ";
                                finalText = "";
                            }                            

                            if (tweak.parameters[5].value == "1")
                            {
                                generalText = generalText.ReplaceAll(ref success, replaceText,

         $@"#if !defined(PS_NEEDS_TANSPACE)
			{elseText}if (cb_mObjectType == 19) cDiffuse = cBase * (float4( saturate((cb_mSun.mAmbient.xyz * {tweak.parameters[0].value} + (shadowContrib * (sunDiffuse * {tweak.parameters[1].value} * fDotSun))) + 
			  (cb_mMoon.mAmbient.xyz * {tweak.parameters[2].value} + (shadowContrib * (moonDiffuse * {tweak.parameters[3].value} * fDotMoon)))), 1) + cDiffuse);
			#else
			{elseText}if (cb_mObjectType == 19)
			  cDiffuse = cBase * (float4( saturate( (cb_mSun.mAmbient.xyz + (shadowContrib * (sunDiffuse * fDotSun))) + (cb_mMoon.mAmbient.xyz + (shadowContrib * (moonDiffuse * fDotMoon)))), 1) + cDiffuse);
			#endif
			{finalText}");
                            }
                            else
                            {
                                generalText = generalText.ReplaceAll(ref success, replaceText,

         $@"{elseText}if (cb_mObjectType == 19) cDiffuse = cBase * (float4( saturate((cb_mSun.mAmbient.xyz * {tweak.parameters[0].value} + (shadowContrib * (sunDiffuse * {tweak.parameters[1].value} * fDotSun))) + 
			  (cb_mMoon.mAmbient.xyz * {tweak.parameters[2].value} + (shadowContrib * (moonDiffuse * {tweak.parameters[3].value} * fDotMoon)))), 1) + cDiffuse);
			{finalText}");
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
                            Tweak rayleigh = tweaks.First(p => p.name == "Rayleigh scattering effect");
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
                    else if (!success && !supported)
                    {
                        Log(ErrorType.Warning, "Did not apply tweak [" + tweak.name + "] in " + currentFile + " file. Tweak is not supported.");
                    }
                    else
                    {
                        tweakCount++;
                        Log(ErrorType.None, "Tweak [" + tweak.name + "] applied.");
                    }
                }
            }

            foreach (CustomTweak custom in customTweaks)
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
                        customCount++;
                        Log(ErrorType.None, "Custom tweak [" + custom.name + "] applied.");
                    }
                }
            }


            if (postProcesses.Any(p => p.isEnabled == true))
            { // if at least one effect is applied
                bool success = false;
                HDRText = HDRText.ReplaceAll(ref success, "return float4(finalColor, 1.0f);", "float4 EndColor = float4(finalColor.rgb, 1);\r\nreturn EndColor;");

                if (!success)
                {
                    Log(ErrorType.Error, "Failed to apply post process function block in " + FileIO.HDRFile + " file.");
                }
            }

            foreach (PostProcess post in postProcesses)
            {
                if (post.isEnabled)
                {
                    bool success = false;

                    string daynightStringStart = "";
                    string daynightStringEnd = "\r\n";
                    switch (post.parameters[post.parameters.Count - 1].value) {                    
                        case "1":
                            daynightStringStart = "if (cb_mDayNightInterpolant == 0) {";
                            daynightStringEnd = "}\r\n";
                            break;
                        case "2":
                            daynightStringStart = "if (cb_mDayNightInterpolant > 0.89) {";
                            daynightStringEnd = "}\r\n";
                            break;
                        case "3":
                            daynightStringStart = "if ((cb_mDayNightInterpolant > 0.01) && (cb_mDayNightInterpolant < 0.89)) {";
                            daynightStringEnd = "}\r\n";
                            break;
                        case "4":
                            daynightStringStart = "if (cb_mDayNightInterpolant < 0.89) {";
                            daynightStringEnd = "}\r\n";
                            break;
                        case "5":
                            daynightStringStart = "if (cb_mDayNightInterpolant > 0.01) {";
                            daynightStringEnd = "}\r\n";
                            break;
                    }

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
}}
");
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", daynightStringStart + "EndColor = SepiaMain(vert, EndColor);" + daynightStringEnd);
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
}}
");
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", daynightStringStart + "EndColor = CurvesMain(vert, EndColor);" + daynightStringEnd);
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
}}
");
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", daynightStringStart + "EndColor = LevelsMain(vert, EndColor);" + daynightStringEnd);
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
}}
");
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", daynightStringStart + "EndColor = LiftGammaGainMain(vert, EndColor);" + daynightStringEnd);
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

    const float TechniAmount = { post.parameters[0].value };
    const float TechniPower = { post.parameters[1].value };
    const float redNegativeAmount = { post.parameters[2].value };
    const float greenNegativeAmount = { post.parameters[3].value };
    const float blueNegativeAmount = { post.parameters[4].value };
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
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", daynightStringStart +  "EndColor = TechnicolorMain(vert, EndColor);" + daynightStringEnd);
                            break;

                        case "Technicolor 2":
                            HDRText = HDRText.AddBefore(ref success, "// Applies exposure and tone mapping to the input, and combines it with the",
$@"float4 TechnicolorMain2(PsQuad vert, float4 color) : SV_Target
{{
    const float3 ColorStrength = float3({post.parameters[0].value});
    const float Brightness = {post.parameters[1].value};
    const float Saturation = {post.parameters[2].value};
    const float Strength = {post.parameters[3].value};

    float4 colorInput = color;
    float3 tcol = saturate(colorInput.rgb);
	
	float3 temp = 1.0 - tcol;
	float3 target = temp.grg;
	float3 target2 = temp.bbr;
	float3 temp2 = tcol * target;
	temp2 *= target2;

	temp = temp2 * ColorStrength;
	temp2 *= Brightness;

	target = temp.grg;
	target2 = temp.bbr;

	temp = tcol - target;
	temp += temp2;
	temp2 = temp - target2;

	tcol = lerp(tcol, temp2, Strength);
	colorInput.rgb = lerp(dot(tcol, 0.333), tcol, Saturation);

    return colorInput;
}}
");
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", daynightStringStart + "EndColor = TechnicolorMain2(vert, EndColor);" + daynightStringEnd);
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
}}
");
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", daynightStringStart + "EndColor = VibranceMain(vert, EndColor);" + daynightStringEnd);
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

float3 RGB_Curve = float3({post.parameters[0].value});
float3 RGB_C = float3({post.parameters[1].value});

const float Contrast = {post.parameters[2].value};
const float Saturation = {post.parameters[3].value};
const float Colorfulness = {post.parameters[4].value};
const float Strength  = {post.parameters[5].value};

    float4 InputColor = Inp_color;

    float3 B = InputColor.rgb;
	B = B * (1.0 - Contrast) + (0.5 * Contrast);
	float3 Btemp = (1.0 / (1.0 + exp(RGB_Curve / 2.0)));
	B = ((1.0 / (1.0 + exp(-RGB_Curve * (B - RGB_C)))) / (-2.0 * Btemp + 1.0)) + (-Btemp / (-2.0 * Btemp + 1.0));

	float value = max(max(B.r, B.g), B.b);
	float3 color = B / value;
	color = pow(abs(color), 1.0 / Colorfulness);

	float3 c0 = color * value;
	c0 = mul(XYZ, c0);
	float luma = dot(c0, float3(0.30, 0.59, 0.11));
	c0 = (1.0 - Saturation) * luma + Saturation * c0;
	c0 = mul(RGB, c0);

    InputColor.rgb = lerp(InputColor.rgb, c0, Strength);
	return InputColor;
}}
");
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", daynightStringStart + "EndColor = DPXMain(vert, EndColor);" + daynightStringEnd);
                            break;

                        // TODO: May be bugged
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
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", daynightStringStart + "EndColor = TonemapMain(vert, EndColor);" + daynightStringEnd);
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
}}
");
                            HDRText = HDRText.AddBefore(ref success, "return EndColor;", daynightStringStart + "EndColor = LumaSharpenMain(vert, EndColor);" + daynightStringEnd);
                            break;

                    }

                    if (!success)
                    {
                        Log(ErrorType.Error, "Failed to apply post process [" + post.name + "] in " + FileIO.HDRFile + " file.");
                        return;
                    }
                    else
                    {
                        postCount++;
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

            activePresetPath = loadedPresetPath;
            activePreset = loadedPreset;
            ActivePreset_Label.Content = activePreset.filename;

            Log(ErrorType.None, "Preset [" + activePreset.filename + "] applied. " 
                + tweakCount + "/" + tweaks.Count(p => p.isEnabled == true) + " tweaks applied. " 
                + customCount + "/" + customTweaks.Count(p => p.isEnabled == true) + " custom tweaks applied. " 
                + postCount + "/" + postProcesses.Count(p => p.isEnabled == true) + " post-processes applied.");

            try
            {
                fileData.ClearDirectory(cacheDirectory);
                Log(ErrorType.None, "Shader cache cleared");
            }
            catch
            {
                Log(ErrorType.Error, "Could not clear shader cache.");                
            }

            ResetChanges(tweaks);
            ResetChanges(postProcesses);
        }

        private void ResetShaderFiles(object sender, RoutedEventArgs e)
        {
            if (fileData.CopyShaderFiles(backupDirectory, shaderDirectory))
            {
                Log(ErrorType.None, "Shader files restored");
                if (fileData.ClearDirectory(cacheDirectory))
                {
                    Log(ErrorType.None, "Shader cache cleared");
                }
            }
        }

        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tweak in tweaks)
            {                
                foreach (var param in tweak.parameters)
                {
                    param.value = param.defaultValue;
                }                
            }

            foreach (var post in postProcesses)
            {               
                foreach (var param in post.parameters)
                {
                    param.value = param.defaultValue;
                }                
            }
        }

        private void ClearShaders_Click(object sender, RoutedEventArgs e)
        {
            if (fileData.ClearDirectory(cacheDirectory))
            {
                Log(ErrorType.None, "Shader cache cleared");
            }
            else
            {
                Log(ErrorType.Error, "Could not clear shader cache.");
            }
        }


        private void ResetChanges<T>(List<T> effectsList) {
            foreach (T entry in effectsList)
            {
                BaseTweak effect = entry as BaseTweak;
                foreach (var param in effect.parameters) {
                    param.hasChanged = false;
                }
                effect.stateChanged = false; // NOTE: This has to be here at the end, because this can raise a property change even in the tweak class,
                                             //       after parameters have has their 'hasChanged' cleared to 'false'
            }           
        }

        public void ChangeMenuBarState(bool enable)
        {
            NewPreset_btn.IsEnabled = enable;
            OpenPreset_btn.IsEnabled = enable;
            SavePreset_btn.IsEnabled = enable;
            SavePresetAs_btn.IsEnabled = enable;
            ApplyPreset_btn.IsEnabled = enable;
            ResetShaderFiles_btn.IsEnabled = enable;
            ClearShaders_btn.IsEnabled = enable;
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
