using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Media.Animation;

namespace FileRenamer
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<FileItem> _files = null!;
        private ICollectionView _filesView = null!;
        private const string SettingsFile = "FileRenamerSettings.json";
        private Stack<List<FileItem>> _undoStack = new Stack<List<FileItem>>();
        
        public ICommand EditNewNameCommand { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            EditNewNameCommand = new RelayCommand(EditNewName);
            InitializeData();
            SetupEventHandlers();
            LoadSettings();
        }

        private void InitializeData()
        {
            _files = new ObservableCollection<FileItem>();
            _filesView = CollectionViewSource.GetDefaultView(_files);
            lvFiles.ItemsSource = _filesView;
            UpdateFileCount();
        }

        private void SetupEventHandlers()
        {
            // 重命名模式切换事件
            rbCustom.Checked += (s, e) => { UpdateParameterVisibility(); SaveSettings(); };
            rbPrefix.Checked += (s, e) => { UpdateParameterVisibility(); SaveSettings(); };
            rbSuffix.Checked += (s, e) => { UpdateParameterVisibility(); SaveSettings(); };
            rbReplace.Checked += (s, e) => { UpdateParameterVisibility(); SaveSettings(); };
            rbNumber.Checked += (s, e) => { UpdateParameterVisibility(); SaveSettings(); };
            rbDateTime.Checked += (s, e) => { UpdateParameterVisibility(); SaveSettings(); };
            rbCase.Checked += (s, e) => { UpdateParameterVisibility(); SaveSettings(); };
            rbRemove.Checked += (s, e) => { UpdateParameterVisibility(); SaveSettings(); };
            rbRandom.Checked += (s, e) => { UpdateParameterVisibility(); SaveSettings(); };
            rbExtract.Checked += (s, e) => { UpdateParameterVisibility(); SaveSettings(); };

            // 参数变化时自动预览和保存
            txtCustomName.TextChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            txtPrefix.TextChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            txtSuffix.TextChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            txtFind.TextChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            txtReplace.TextChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            txtStartNumber.TextChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            cmbNumberFormat.SelectionChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            cmbDateTimeFormat.SelectionChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            cmbDateTimePosition.SelectionChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            cmbCaseType.SelectionChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            txtRemoveText.TextChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            cmbRemoveType.SelectionChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            txtRandomLength.TextChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            cmbRandomType.SelectionChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            cmbRandomPosition.SelectionChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            cmbExtractType.SelectionChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            txtExtractRegex.TextChanged += (s, e) => { AutoPreview(); SaveSettings(); };
            chkPreserveExtension.Checked += (s, e) => { AutoPreview(); SaveSettings(); };
            chkPreserveExtension.Unchecked += (s, e) => { AutoPreview(); SaveSettings(); };
            chkCaseSensitive.Checked += (s, e) => { AutoPreview(); SaveSettings(); };
            chkCaseSensitive.Unchecked += (s, e) => { AutoPreview(); SaveSettings(); };

            // 窗口关闭时保存设置
            this.Closed += (s, e) => SaveSettings();
        }

        #region 设置保存和加载

        private void SaveSettings()
        {
            try
            {
                var settings = new UserSettings
                {
                    RenameMode = GetCurrentRenameMode(),
                    CustomName = txtCustomName.Text,
                    Prefix = txtPrefix.Text,
                    Suffix = txtSuffix.Text,
                    FindText = txtFind.Text,
                    ReplaceText = txtReplace.Text,
                    StartNumber = txtStartNumber.Text,
                    NumberFormat = cmbNumberFormat.SelectedIndex,
                    PreserveExtension = chkPreserveExtension.IsChecked ?? true,
                    CaseSensitive = chkCaseSensitive.IsChecked ?? false
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                // 静默处理设置保存错误，不影响主程序功能
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<UserSettings>(json);
                    
                    if (settings != null)
                    {
                        // 恢复重命名模式
                        SetRenameMode(settings.RenameMode);
                        
                        // 恢复参数值
                        txtCustomName.Text = settings.CustomName ?? "文件{n}";
                        txtPrefix.Text = settings.Prefix ?? "新_";
                        txtSuffix.Text = settings.Suffix ?? "_新";
                        txtFind.Text = settings.FindText ?? "旧";
                        txtReplace.Text = settings.ReplaceText ?? "新";
                        txtStartNumber.Text = settings.StartNumber ?? "1";
                        
                        // 恢复选项
                        if (settings.NumberFormat >= 0 && settings.NumberFormat < cmbNumberFormat.Items.Count)
                        {
                            cmbNumberFormat.SelectedIndex = settings.NumberFormat;
                        }
                        chkPreserveExtension.IsChecked = settings.PreserveExtension;
                        chkCaseSensitive.IsChecked = settings.CaseSensitive;
                    }
                }
            }
            catch (Exception ex)
            {
                // 静默处理设置加载错误，使用默认值
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}");
            }
        }

        private string GetCurrentRenameMode()
        {
            if (rbCustom.IsChecked == true) return "Custom";
            if (rbPrefix.IsChecked == true) return "Prefix";
            if (rbSuffix.IsChecked == true) return "Suffix";
            if (rbReplace.IsChecked == true) return "Replace";
            if (rbNumber.IsChecked == true) return "Number";
            if (rbDateTime.IsChecked == true) return "DateTime";
            if (rbCase.IsChecked == true) return "Case";
            if (rbRemove.IsChecked == true) return "Remove";
            if (rbRandom.IsChecked == true) return "Random";
            if (rbExtract.IsChecked == true) return "Extract";
            return "Custom";
        }

        private void SetRenameMode(string mode)
        {
            switch (mode)
            {
                case "Custom":
                    rbCustom.IsChecked = true;
                    break;
                case "Prefix":
                    rbPrefix.IsChecked = true;
                    break;
                case "Suffix":
                    rbSuffix.IsChecked = true;
                    break;
                case "Replace":
                    rbReplace.IsChecked = true;
                    break;
                case "Number":
                    rbNumber.IsChecked = true;
                    break;
                case "DateTime":
                    rbDateTime.IsChecked = true;
                    break;
                case "Case":
                    rbCase.IsChecked = true;
                    break;
                case "Remove":
                    rbRemove.IsChecked = true;
                    break;
                case "Random":
                    rbRandom.IsChecked = true;
                    break;
                case "Extract":
                    rbExtract.IsChecked = true;
                    break;
                default:
                    rbCustom.IsChecked = true;
                    break;
            }
        }

        #endregion

        private void UpdateParameterVisibility()
        {
            pnlCustom.Visibility = rbCustom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            pnlPrefix.Visibility = rbPrefix.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            pnlSuffix.Visibility = rbSuffix.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            pnlReplace.Visibility = rbReplace.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            pnlNumber.Visibility = rbNumber.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            pnlDateTime.Visibility = rbDateTime.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            pnlCase.Visibility = rbCase.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            pnlRemove.Visibility = rbRemove.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            pnlRandom.Visibility = rbRandom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            pnlExtract.Visibility = rbExtract.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            
            AutoPreview();
        }

        private void AutoPreview()
        {
            if (_files.Count > 0)
            {
                try
                {
                    foreach (var file in _files)
                    {
                        file.NewName = GenerateNewName(file);
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"预览失败: {ex.Message}");
                }
            }
        }

        #region 文件选择

        private void btnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Title = "选择要重命名的文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                AddFiles(openFileDialog.FileNames);
            }
        }

        private void btnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择要重命名的文件夹"
            };

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var files = Directory.GetFiles(folderDialog.SelectedPath, "*.*", SearchOption.AllDirectories);
                AddFiles(files);
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_files.Count > 0)
            {
                // 保存当前状态到撤销栈
                SaveUndoState();
                _files.Clear();
                UpdateFileCount();
                UpdateStatus("文件列表已清空");
            }
        }

        private void btnUndo_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count > 0)
            {
                var previousState = _undoStack.Pop();
                _files.Clear();
                foreach (var item in previousState)
                {
                    _files.Add(item);
                }
                UpdateFileCount();
                UpdateStatus("已撤销上一步操作");
            }
            else
            {
                UpdateStatus("没有可撤销的操作");
            }
        }

        private void SaveUndoState()
        {
            var currentState = _files.Select(f => new FileItem(f.FullPath) 
            { 
                NewName = f.NewName 
            }).ToList();
            _undoStack.Push(currentState);
        }

        #endregion

        #region 拖拽功能

        private void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
        {
            DropHint.Visibility = Visibility.Collapsed;
            
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                AddFiles(files);
            }
        }

        private void DropZone_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                DropHint.Visibility = Visibility.Visible;
            }
        }

        private void DropZone_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            DropHint.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region 文件处理

        private void AddFiles(string[] filePaths)
        {
            var newFiles = new List<FileItem>();
            
            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                {
                    newFiles.Add(new FileItem(path));
                }
                else if (Directory.Exists(path))
                {
                    // 如果是文件夹，添加文件夹中的所有文件
                    var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
                    newFiles.AddRange(files.Select(f => new FileItem(f)));
                }
            }

            // 去重并添加到列表
            var existingPaths = _files.Select(f => f.FullPath).ToHashSet();
            var uniqueFiles = newFiles.Where(f => !existingPaths.Contains(f.FullPath));
            
            foreach (var file in uniqueFiles)
            {
                _files.Add(file);
            }

            UpdateFileCount();
            UpdateStatus($"已添加 {uniqueFiles.Count()} 个文件");
            AutoPreview();
        }

        #endregion

        #region 重命名逻辑

        private void btnRename_Click(object sender, RoutedEventArgs e)
        {
            if (_files.Count == 0)
            {
                UpdateStatus("请先添加文件");
                return;
            }

            // 保存重命名前的状态
            SaveUndoState();
            ExecuteRename();
        }

        private void ExecuteRename()
        {
            var successCount = 0;
            var errorCount = 0;
            var errors = new List<string>();
            var processedFiles = new HashSet<string>(); // 跟踪已处理的文件

            foreach (var file in _files)
            {
                try
                {
                    var directory = Path.GetDirectoryName(file.FullPath)!;
                    var newPath = Path.Combine(directory, file.NewName);
                    
                    // 如果新路径与原路径相同，跳过
                    if (newPath.Equals(file.FullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        successCount++;
                        continue;
                    }
                    
                    // 检查新文件名是否已存在（在已处理的文件中）
                    if (processedFiles.Contains(newPath, StringComparer.OrdinalIgnoreCase))
                    {
                        // 自动添加序号避免冲突
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(file.NewName);
                        var extension = Path.GetExtension(file.NewName);
                        var counter = 1;
                        var finalNewName = file.NewName;
                        
                        while (processedFiles.Contains(Path.Combine(directory, finalNewName), StringComparer.OrdinalIgnoreCase))
                        {
                            finalNewName = $"{nameWithoutExt}_{counter}{extension}";
                            counter++;
                        }
                        
                        file.NewName = finalNewName;
                        newPath = Path.Combine(directory, finalNewName);
                    }
                    
                    // 检查文件系统中是否已存在同名文件
                    if (File.Exists(newPath) && !newPath.Equals(file.FullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // 自动添加序号避免冲突
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(file.NewName);
                        var extension = Path.GetExtension(file.NewName);
                        var counter = 1;
                        var finalNewName = file.NewName;
                        
                        while (File.Exists(Path.Combine(directory, finalNewName)))
                        {
                            finalNewName = $"{nameWithoutExt}_{counter}{extension}";
                            counter++;
                        }
                        
                        file.NewName = finalNewName;
                        newPath = Path.Combine(directory, finalNewName);
                    }

                    // 执行重命名
                    File.Move(file.FullPath, newPath);
                    
                    // 更新文件信息
                    file.FullPath = newPath;
                    file.OriginalName = Path.GetFileName(newPath);
                    
                    processedFiles.Add(newPath);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"重命名失败 {file.OriginalName}: {ex.Message}");
                    errorCount++;
                }
            }

            // 构建状态消息
            var statusMessage = $"重命名完成: 成功 {successCount} 个";
            if (errorCount > 0)
            {
                statusMessage += $"，失败 {errorCount} 个";
                if (errors.Count > 0)
                {
                    statusMessage += $" - {errors.First()}";
                    if (errors.Count > 1)
                    {
                        statusMessage += $" 等 {errors.Count} 个错误";
                    }
                }
            }
            
            UpdateStatus(statusMessage);
        }

        private string GenerateNewName(FileItem file)
        {
            var originalName = file.OriginalName;
            var extension = Path.GetExtension(originalName);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalName);
            var newName = "";

            if (rbCustom.IsChecked == true)
            {
                newName = txtCustomName.Text;
                if (newName.Contains("{n}"))
                {
                    var index = _files.IndexOf(file) + 1;
                    newName = newName.Replace("{n}", index.ToString());
                }
            }
            else if (rbPrefix.IsChecked == true)
            {
                newName = txtPrefix.Text + originalName;
            }
            else if (rbSuffix.IsChecked == true)
            {
                newName = nameWithoutExtension + txtSuffix.Text + extension;
            }
            else if (rbReplace.IsChecked == true)
            {
                var findText = txtFind.Text;
                var replaceText = txtReplace.Text;
                
                if (string.IsNullOrEmpty(findText))
                {
                    // 如果查找文本为空，直接返回原名
                    newName = originalName;
                }
                else if (chkCaseSensitive.IsChecked == true)
                {
                    newName = originalName.Replace(findText, replaceText);
                }
                else
                {
                    newName = Regex.Replace(originalName, Regex.Escape(findText), replaceText, RegexOptions.IgnoreCase);
                }
            }
            else if (rbNumber.IsChecked == true)
            {
                var startNumber = int.TryParse(txtStartNumber.Text, out var num) ? num : 1;
                var index = _files.IndexOf(file) + startNumber;
                var format = cmbNumberFormat.SelectedIndex switch
                {
                    0 => "D",
                    1 => "D2",
                    2 => "D3",
                    _ => "D"
                };
                
                newName = index.ToString(format) + extension;
            }
            else if (rbDateTime.IsChecked == true)
            {
                var dateTimeFormat = cmbDateTimeFormat.SelectedIndex switch
                {
                    0 => "yyyyMMdd",
                    1 => "yyyy-MM-dd",
                    2 => "MM/dd/yyyy",
                    3 => "MMM d, yyyy",
                    4 => "MMMM d, yyyy",
                    5 => "dddd, MMMM d, yyyy",
                    6 => "dddd, MMMM d, yyyy h:mm tt",
                    7 => "dddd, MMMM d, yyyy h:mm:ss tt",
                    _ => "yyyyMMdd"
                };
                var dateTimePosition = cmbDateTimePosition.SelectedIndex switch
                {
                    0 => "Prefix",
                    1 => "Suffix",
                    2 => "Replace",
                    _ => "Prefix"
                };
                var dateTime = DateTime.Now.ToString(dateTimeFormat);
                newName = dateTimeFormat switch
                {
                    "yyyyMMdd" => dateTime,
                    _ => dateTimePosition switch
                    {
                        "Prefix" => dateTime + txtPrefix.Text,
                        "Suffix" => txtSuffix.Text + dateTime,
                        "Replace" => Regex.Replace(originalName, Regex.Escape(dateTimeFormat), dateTime, RegexOptions.IgnoreCase),
                        _ => originalName
                    }
                };
            }
            else if (rbCase.IsChecked == true)
            {
                var caseType = cmbCaseType.SelectedIndex switch
                {
                    0 => "Upper",
                    1 => "Lower",
                    2 => "Title",
                    _ => "Upper"
                };
                newName = caseType switch
                {
                    "Upper" => originalName.ToUpper(),
                    "Lower" => originalName.ToLower(),
                    "Title" => new CultureInfo("en-US").TextInfo.ToTitleCase(originalName),
                    _ => originalName
                };
            }
            else if (rbRemove.IsChecked == true)
            {
                var removeText = txtRemoveText.Text;
                var removeType = cmbRemoveType.SelectedIndex switch
                {
                    0 => "All",
                    1 => "Start",
                    2 => "End",
                    _ => "All"
                };
                newName = removeType switch
                {
                    "All" => originalName.Replace(removeText, ""),
                    "Start" => originalName.Remove(0, removeText.Length),
                    "End" => originalName.Substring(0, originalName.Length - removeText.Length),
                    _ => originalName
                };
            }
            else if (rbRandom.IsChecked == true)
            {
                var randomLength = int.TryParse(txtRandomLength.Text, out var length) ? length : 10;
                var randomType = cmbRandomType.SelectedIndex switch
                {
                    0 => "Alphanumeric",
                    1 => "Numeric",
                    2 => "Alphabetical",
                    _ => "Alphanumeric"
                };
                var randomPosition = cmbRandomPosition.SelectedIndex switch
                {
                    0 => "Prefix",
                    1 => "Suffix",
                    2 => "Replace",
                    _ => "Prefix"
                };
                newName = randomType switch
                {
                    "Alphanumeric" => GenerateRandomString(length),
                    "Numeric" => GenerateRandomNumericString(length),
                    "Alphabetical" => GenerateRandomAlphabeticalString(length),
                    _ => GenerateRandomString(length)
                };
                newName = randomPosition switch
                {
                    "Prefix" => newName + txtPrefix.Text,
                    "Suffix" => txtSuffix.Text + newName,
                    "Replace" => Regex.Replace(originalName, Regex.Escape(originalName), newName, RegexOptions.IgnoreCase),
                    _ => newName
                };
            }
            else if (rbExtract.IsChecked == true)
            {
                var extractType = cmbExtractType.SelectedIndex switch
                {
                    0 => "Substring",
                    1 => "Regex",
                    _ => "Substring"
                };
                var extractRegex = txtExtractRegex.Text;
                newName = extractType switch
                {
                    "Substring" => ExtractSubstring(originalName, extractRegex),
                    "Regex" => ExtractRegex(originalName, extractRegex),
                    _ => ExtractSubstring(originalName, extractRegex)
                };
            }

            // 处理扩展名
            if (chkPreserveExtension.IsChecked == true && !string.IsNullOrEmpty(extension))
            {
                if (!newName.EndsWith(extension))
                {
                    newName += extension;
                }
            }
            else if (chkPreserveExtension.IsChecked == false)
            {
                newName = Path.GetFileNameWithoutExtension(newName);
            }

            // 清理文件名中的非法字符
            newName = CleanFileName(newName);
            
            // 确保文件名不为空
            if (string.IsNullOrWhiteSpace(newName))
            {
                newName = originalName;
            }
            
            return newName;
        }

        private string CleanFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;
                
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleaned = invalidChars.Aggregate(fileName, (current, invalidChar) => current.Replace(invalidChar, '_'));
            
            // 移除开头和结尾的空格、点号
            cleaned = cleaned.Trim(' ', '.');
            
            // 如果清理后为空，返回原文件名
            if (string.IsNullOrWhiteSpace(cleaned))
                return fileName;
                
            return cleaned;
        }

        private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string GenerateRandomNumericString(int length)
        {
            const string chars = "0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string GenerateRandomAlphabeticalString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string ExtractSubstring(string text, string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                return text;
            
            var index = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? text.Substring(index, pattern.Length) : text;
        }

        private string ExtractRegex(string text, string regexPattern)
        {
            if (string.IsNullOrEmpty(regexPattern))
                return text;
            
            try
            {
                var match = Regex.Match(text, regexPattern);
                return match.Success ? match.Value : text;
            }
            catch
            {
                return text;
            }
        }

        #endregion

        #region UI更新

        private void UpdateFileCount()
        {
            txtFileCount.Text = $"{_files.Count} 个文件";
        }

        private void UpdateStatus(string message)
        {
            txtStatus.Text = message;
        }

        #endregion

        #region 新文件名输入框事件处理

        private void NewNameTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                // 只改变颜色，保持下划线粗细不变
                textBox.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243));
            }
        }

        private void NewNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                // 恢复默认颜色
                textBox.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204));
            }
        }

        #endregion

        private void EditNewName(object? parameter)
        {
            // 获取当前点击的文件项
            if (parameter is FileItem fileItem)
            {
                // 找到对应的ListViewItem
                var listViewItem = lvFiles.ItemContainerGenerator.ContainerFromItem(fileItem) as System.Windows.Controls.ListViewItem;
                if (listViewItem != null)
                {
                    // 在ListViewItem中查找TextBox和TextBlock
                    var textBlock = FindVisualChild<TextBlock>(listViewItem, "NewNameTextBlock");
                    var textBox = FindVisualChild<System.Windows.Controls.TextBox>(listViewItem, "NewNameTextBox");
                    
                    if (textBlock != null && textBox != null)
                    {
                        textBlock.Visibility = Visibility.Hidden;
                        textBox.Visibility = Visibility.Visible;
                        textBox.Focus();
                        textBox.SelectAll();
                    }
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T element && element.Name == name)
                {
                    return element;
                }
                
                var result = FindVisualChild<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }
    }

    public class FileItem : INotifyPropertyChanged
    {
        private string _originalName = string.Empty;
        private string _newName = string.Empty;
        private string _fullPath = string.Empty;
        private bool _isEditing;

        public string OriginalName
        {
            get => _originalName;
            set
            {
                _originalName = value;
                OnPropertyChanged(nameof(OriginalName));
            }
        }

        public string NewName
        {
            get => _newName;
            set
            {
                _newName = value;
                OnPropertyChanged(nameof(NewName));
            }
        }

        public string FullPath
        {
            get => _fullPath;
            set
            {
                _fullPath = value;
                OnPropertyChanged(nameof(FullPath));
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; OnPropertyChanged(nameof(IsEditing)); }
        }

        public FileItem(string filePath)
        {
            _fullPath = filePath;
            _originalName = Path.GetFileName(filePath);
            _newName = _originalName;
            
            // 手动触发属性变化通知，确保UI能正确显示
            OnPropertyChanged(nameof(FullPath));
            OnPropertyChanged(nameof(OriginalName));
            OnPropertyChanged(nameof(NewName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class UserSettings
    {
        public string RenameMode { get; set; } = "Custom";
        public string? CustomName { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public string? FindText { get; set; }
        public string? ReplaceText { get; set; }
        public string? StartNumber { get; set; }
        public int NumberFormat { get; set; }
        public bool PreserveExtension { get; set; } = true;
        public bool CaseSensitive { get; set; } = false;
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !(bool)value;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => !(bool)value;
    }
} 