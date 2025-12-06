using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Editing;

namespace Mermaid
{
    public partial class MainWindow : Window
    {
        public static readonly DependencyProperty MenuIconProperty =
            DependencyProperty.RegisterAttached("MenuIcon", typeof(string), typeof(MainWindow), new PropertyMetadata(null));

        public static string GetMenuIcon(DependencyObject obj)
        {
            return (string)obj.GetValue(MenuIconProperty);
        }

        public static void SetMenuIcon(DependencyObject obj, string value)
        {
            obj.SetValue(MenuIconProperty, value);
        }

        private const double MinFontSize = 8;
        private const double MaxFontSize = 48;
        private const int MaxRecentFiles = 10;
        private bool _isEditorDarkMode = true;
        private bool _isPreviewDarkMode = true;
        private int _untitledCount = 1;
        
        // Recent Files
        private List<string> _recentFiles = new List<string>();
        private string RecentFilesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MermaidProDesktop", "recent.txt");
        
        // Search State
        private List<int> _searchResults = new List<int>();
        private int _currentResultIndex = -1;

        // Helper to get the currently active editor
        private TextEditor? CurrentEditor
        {
            get
            {
                if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor editor)
                {
                    return editor;
                }
                return null;
            }
        }

        // Helper to get the file path of the current tab
        private string? CurrentFilePath
        {
            get
            {
                if (EditorTabs.SelectedItem is TabItem tab)
                {
                    return tab.Tag as string;
                }
                return null;
            }
            set
            {
                if (EditorTabs.SelectedItem is TabItem tab)
                {
                    tab.Tag = value;
                    UpdateTabHeader(tab);
                }
            }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public MainWindow()
        {
            InitializeComponent();
            
            // Load recent files from storage
            LoadRecentFiles();
            
            // Fix: Force Left-Aligned Menus (overrides Tablet/Touch Windows settings)
            var menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (SystemParameters.MenuDropAlignment && menuDropAlignmentField != null)
            {
                menuDropAlignmentField.SetValue(null, false);
            }
            
            if (!string.IsNullOrEmpty(App.StartupFile) && File.Exists(App.StartupFile))
            {
                try
                {
                    string content = File.ReadAllText(App.StartupFile);
                    AddNewTab(Path.GetFileName(App.StartupFile), content, App.StartupFile);
                    AddToRecentFiles(App.StartupFile);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening file: {ex.Message}");
                    AddNewTab($"Untitled-{_untitledCount++}.mmd", "", null);
                }
            }
            else
            {
                // Initialize with one default tab
                AddNewTab($"Untitled-{_untitledCount++}.mmd", "", null);
            }

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeWebView();

            // Enable dark title bar
            var windowInteropHelper = new WindowInteropHelper(this);
            var hwnd = windowInteropHelper.Handle;
            int useImmersiveDarkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
        }

        private void UpdateTabHeader(TabItem tab)
        {
            string? filePath = tab.Tag as string;
            var editor = tab.Content as TextEditor;
            bool isModified = editor != null && !editor.Document.UndoStack.IsOriginalFile;

            string title;
            if (filePath != null)
            {
                title = Path.GetFileName(filePath);
            }
            else
            {
                // Preserve existing untitled name if possible, or just strip *
                title = tab.Header.ToString().TrimEnd('*');
            }

            tab.Header = title + (isModified ? "*" : "");
        }

        private void UpdateCursorStatus()
        {
            var editor = CurrentEditor;
            if (editor != null)
            {
                int line = editor.TextArea.Caret.Line;
                int col = editor.TextArea.Caret.Column;
                int totalLines = editor.Document.LineCount;
                CursorPositionText.Text = $"Ln {line} of {totalLines}, Col {col}";
            }
            else
            {
                CursorPositionText.Text = "Ln 0 of 0, Col 0";
            }
        }

        private void AddNewTab(string title, string content, string? filePath)
        {
            var editor = new TextEditor
            {
                FontFamily = new FontFamily("JetBrains Mono, Consolas, Monospace"),
                FontSize = 16,
                ShowLineNumbers = true,
                Background = Brushes.Transparent,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#e4e4e4"),
                LineNumbersForeground = (Brush)new BrushConverter().ConvertFrom("#454545"),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                WordWrap = true,
                Text = content
            };

            // Editor Options
            editor.Options.ShowColumnRuler = false;
            editor.Options.AllowScrollBelowDocument = true;

            // Custom Current Line Highlight
            editor.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineHighlightRenderer(editor));

            // Customize Line Number Margin
            editor.Loaded += (s, e) =>
            {
                foreach (var margin in editor.TextArea.LeftMargins)
                {
                    if (margin is LineNumberMargin lineNumberMargin)
                    {
                        lineNumberMargin.MinWidth = 35;
                        lineNumberMargin.Margin = new Thickness(0, 0, 10, 0);
                    }
                }
            };

            // Events
            editor.PreviewMouseWheel += TxtEditor_PreviewMouseWheel;
            editor.TextChanged += TxtEditor_TextChanged;
            editor.TextArea.Caret.PositionChanged += (s, e) => 
            {
                UpdateCursorStatus();
                editor.TextArea.TextView.InvalidateVisual();
            };

            // Apply Syntax Highlighting
            ApplySyntaxHighlighting(editor, _isEditorDarkMode);

            // Mark as original since it's just loaded/created
            editor.Document.UndoStack.MarkAsOriginalFile();

            var tab = new TabItem
            {
                Header = title,
                Content = editor,
                Tag = filePath,
                Style = (Style)Application.Current.Resources["EditorTabItemStyle"]
            };

            EditorTabs.Items.Add(tab);
            EditorTabs.SelectedItem = tab;
        }

        private void CloseTab(TabItem tab)
        {
            if (!ConfirmClose(tab)) return;

            EditorTabs.Items.Remove(tab);
            // if (EditorTabs.Items.Count == 0)
            // {
            //     // Ensure at least one tab exists
            //     AddNewTab($"Untitled-{_untitledCount++}.mmd", "", null);
            // }
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Button btn && btn.Tag is TabItem tab)
            {
                CloseTab(tab);
            }
        }

        private bool ConfirmClose(TabItem tab)
        {
            if (tab.Content is TextEditor editor && !editor.Document.UndoStack.IsOriginalFile)
            {
                string fileName = tab.Header.ToString().TrimEnd('*');
                
                var dialog = new ConfirmSaveDialog(fileName);
                dialog.Owner = this;
                dialog.ShowDialog();
                var result = dialog.Result;
                
                if (result == MessageBoxResult.Yes)
                {
                    return SaveTab(tab);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return false;
                }
            }
            return true;
        }

        private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RenderDiagram();
            UpdateCursorStatus();
        }

        private void TxtEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var editor = sender as TextEditor;
            if (editor == null) return;

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                // Horizontal Scroll
                if (e.Delta < 0)
                {
                    editor.ScrollToHorizontalOffset(editor.HorizontalOffset + 40);
                }
                else
                {
                    editor.ScrollToHorizontalOffset(editor.HorizontalOffset - 40);
                }
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Zoom (Font Size)
                if (e.Delta > 0)
                {
                    if (editor.FontSize < MaxFontSize)
                        editor.FontSize += 1;
                }
                else
                {
                    if (editor.FontSize > MinFontSize)
                        editor.FontSize -= 1;
                }
                e.Handled = true;
            }
        }

        private void ApplySyntaxHighlighting(TextEditor editor, bool isDark)
        {
            string lightTheme = @"
<SyntaxDefinition name='Mermaid' xmlns='http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008'>
    <Color name='Comment' foreground='#008000' />
    <Color name='String' foreground='#A31515' />
    <Color name='Keyword' foreground='#0000FF' fontWeight='bold' />
    <Color name='Arrow' foreground='#000000' fontWeight='bold' />
    <Color name='Class' foreground='#2B91AF' />
    <RuleSet>
        <Span color='Comment' begin='%%' />
        <Span color='String'>
            <Begin>""</Begin>
            <End>""</End>
        </Span>
        <Keywords color='Keyword'>
            <Word>graph</Word>
            <Word>subgraph</Word>
            <Word>end</Word>
            <Word>classDef</Word>
            <Word>style</Word>
            <Word>click</Word>
            <Word>TD</Word>
            <Word>LR</Word>
            <Word>TB</Word>
            <Word>RL</Word>
        </Keywords>
        <Rule color='Arrow'>
            (-{1,2}>?)|(={1,2}>?)|(-\.->?)|(=\.=>?)
        </Rule>
        <Rule color='Class'>
            :::\w+
        </Rule>
    </RuleSet>
</SyntaxDefinition>";

            string darkTheme = @"
<SyntaxDefinition name='Mermaid' xmlns='http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008'>
    <Color name='Comment' foreground='#6A9955' />
    <Color name='String' foreground='#CE9178' />
    <Color name='Keyword' foreground='#C586C0' />
    <Color name='Direction' foreground='#569CD6' />
    <Color name='Arrow' foreground='#D4D4D4' />
    <Color name='Node' foreground='#9CDCFE' />
    <Color name='Label' foreground='#CE9178' />
    <Color name='Class' foreground='#4EC9B0' />
    
    <RuleSet>
        <Span color='Comment' begin='%%' />
        
        <Span color='String'>
            <Begin>""</Begin>
            <End>""</End>
        </Span>

        <!-- Node Labels -->
        <Span color='Label' multiline='true'>
            <Begin>\[</Begin>
            <End>\]</End>
        </Span>
        <Span color='Label' multiline='true'>
            <Begin>\(</Begin>
            <End>\)</End>
        </Span>
        <Span color='Label' multiline='true'>
            <Begin>\{</Begin>
            <End>\}</End>
        </Span>

        <Keywords color='Keyword'>
            <Word>graph</Word>
            <Word>subgraph</Word>
            <Word>end</Word>
            <Word>classDef</Word>
            <Word>style</Word>
            <Word>click</Word>
            <Word>linkStyle</Word>
        </Keywords>

        <Keywords color='Direction'>
            <Word>TD</Word>
            <Word>LR</Word>
            <Word>TB</Word>
            <Word>RL</Word>
            <Word>BT</Word>
        </Keywords>

        <Rule color='Arrow'>
            [-=.]{1,3}>?|[-=.]{1,3}\|
        </Rule>

        <Rule color='Class'>
            :::\w+
        </Rule>
        
        <!-- Catch-all for Node IDs (must be after keywords) -->
        <Rule color='Node'>
            \b[\w]+\b
        </Rule>
    </RuleSet>
</SyntaxDefinition>";

            string themeToUse = isDark ? darkTheme : lightTheme;

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(themeToUse)))
            {
                using (var reader = new XmlTextReader(stream))
                {
                    editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
        }

        private void UpdateSyntaxHighlighting(bool isDark)
        {
            foreach (TabItem tab in EditorTabs.Items)
            {
                if (tab.Content is TextEditor editor)
                {
                    ApplySyntaxHighlighting(editor, isDark);
                }
            }
        }

        private async void InitializeWebView()
        {
            try
            {
                // Use a fresh user data folder to avoid lock/corruption issues
                var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MermaidEditor_Data");
                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await PreviewWebView.EnsureCoreWebView2Async(env);
                
                // Enable context menu for text selection and copy
                PreviewWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                
                // Handle Web Messages (Zoom updates)
                PreviewWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                // Map the current directory (where the exe and js files are) to a virtual host
                PreviewWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "mermaid.editor", 
                    AppDomain.CurrentDomain.BaseDirectory, 
                    CoreWebView2HostResourceAccessKind.Allow
                );

                RenderDiagram();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 initialization failed: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}");
            }
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var jsonString = e.WebMessageAsJson;
                using (var doc = System.Text.Json.JsonDocument.Parse(jsonString))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "zoom")
                    {
                        // Zoom level is now displayed inside WebView2 HTML
                    }
                }
            }
            catch { }
        }

        // Editor Theme Toggle
        private void ToggleEditorTheme_Click(object sender, RoutedEventArgs e)
        {
            _isEditorDarkMode = !_isEditorDarkMode;
            ApplyEditorTheme(_isEditorDarkMode, sender as System.Windows.Controls.Button);
            UpdateSyntaxHighlighting(_isEditorDarkMode);
        }

        // Preview Theme Toggle
        private void TogglePreviewTheme_Click(object sender, RoutedEventArgs e)
        {
            _isPreviewDarkMode = !_isPreviewDarkMode;
            
            // Update button text
            if (sender is System.Windows.Controls.Button btn)
            {
                btn.Content = _isPreviewDarkMode ? "Graph Theme: Dark" : "Graph Theme: Light";
            }

            RenderDiagram(); // Re-render with new theme
        }

        // Layout Selection
        private void CboLayout_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RenderDiagram();
        }

        private void ApplyEditorTheme(bool isDark, System.Windows.Controls.Button? toggleButton)
        {
            var resources = Application.Current.Resources;

            if (isDark)
            {
                // Apply Dark Theme
                resources["AppBackground"] = resources["DarkBackground"];
                resources["AppForeground"] = resources["DarkForeground"];
                resources["ToolbarBackground"] = resources["DarkToolbarBackground"];
                resources["EditorBackground"] = resources["DarkEditorBackground"];
                resources["EditorForeground"] = resources["DarkEditorForeground"];
                resources["LineNumbersForeground"] = resources["DarkLineNumbersForeground"];
                resources["SplitterBackground"] = resources["DarkSplitterBackground"];
                resources["CurrentLineBackground"] = resources["DarkCurrentLineBackground"];
            }
            else
            {
                // Apply Light Theme
                resources["AppBackground"] = resources["LightBackground"];
                resources["AppForeground"] = resources["LightForeground"];
                resources["ToolbarBackground"] = resources["LightToolbarBackground"];
                resources["EditorBackground"] = resources["LightEditorBackground"];
                resources["EditorForeground"] = resources["LightEditorForeground"];
                resources["LineNumbersForeground"] = resources["LightLineNumbersForeground"];
                resources["SplitterBackground"] = resources["LightSplitterBackground"];
                resources["CurrentLineBackground"] = resources["LightCurrentLineBackground"];
            }
        }

        // Keyboard shortcut handler
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // File Operations
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // New Tab
                AddNewTab($"Untitled-{_untitledCount++}.mmd", "", null);
                e.Handled = true;
            }
            else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (EditorTabs.SelectedItem is TabItem tab)
                {
                    CloseTab(tab);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Open_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Save_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                SaveAs_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
            {
                SaveAll_Click(sender, e);
                e.Handled = true;
            }

            // Workflow
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Render_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.P && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                SavePng_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.V && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                SaveSvg_Click(sender, e);
                e.Handled = true;
            }

            // Editor Controls
            else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CurrentEditor?.Undo();
                e.Handled = true;
            }
            else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CurrentEditor?.Redo();
                e.Handled = true;
            }
            else if (e.Key == Key.OemQuestion && Keyboard.Modifiers == ModifierKeys.Control) // Ctrl + /
            {
                ToggleComment();
                e.Handled = true;
            }

            // View & Navigation
            else if ((e.Key == Key.Add || e.Key == Key.OemPlus) && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ZoomIn_Click(sender, e);
                e.Handled = true;
            }
            else if ((e.Key == Key.Subtract || e.Key == Key.OemMinus) && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ZoomOut_Click(sender, e);
                e.Handled = true;
            }
            else if ((e.Key == Key.D0 || e.Key == Key.NumPad0) && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Fit_Click(sender, e); // Reset Zoom
                e.Handled = true;
            }
            
            // View Layout Shortcuts
            else if (e.Key == Key.D1 && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ViewSplit_Click(sender, e); // Split View
                e.Handled = true;
            }
            else if (e.Key == Key.D2 && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ViewEditorOnly_Click(sender, e); // Editor Only
                e.Handled = true;
            }
            else if (e.Key == Key.D3 && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ViewPreviewOnly_Click(sender, e); // Preview Only
                e.Handled = true;
            }
            
            // Theme Toggle Shortcuts
            else if (e.Key == Key.T && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                ToggleEditorTheme_Click(sender, e); // Toggle Editor Theme
                e.Handled = true;
            }
            else if (e.Key == Key.T && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
            {
                TogglePreviewTheme_Click(sender, e); // Toggle Preview Theme
                e.Handled = true;
            }
            
            // Legacy F5
            else if (e.Key == Key.F5)
            {
                Render_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                OpenSearch();
                e.Handled = true;
            }
            else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
            {
                OpenReplace();
                e.Handled = true;
            }
            else if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
            {
                GoToLine_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.F3)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                    FindPrevious();
                else
                    FindNext();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (SearchPanel.Visibility == Visibility.Visible)
                {
                    CloseSearch();
                    e.Handled = true;
                }
            }
        }

        // Search Implementation
        private void OpenSearch()
        {
            SearchPanel.Visibility = Visibility.Visible;
            ReplaceRow.Visibility = Visibility.Collapsed;
            SearchBox.Focus();
            SearchBox.SelectAll();
            PerformSearch();
        }

        private void OpenReplace()
        {
            SearchPanel.Visibility = Visibility.Visible;
            ReplaceRow.Visibility = Visibility.Visible;
            SearchBox.Focus();
            SearchBox.SelectAll();
            PerformSearch();
        }

        private void CloseSearch()
        {
            SearchPanel.Visibility = Visibility.Collapsed;
            CurrentEditor?.Focus();
        }

        private void PerformSearch()
        {
            _searchResults.Clear();
            _currentResultIndex = -1;
            SearchStatus.Text = "No results";

            var editor = CurrentEditor;
            if (editor == null || string.IsNullOrEmpty(SearchBox.Text)) return;

            string text = editor.Text;
            string query = SearchBox.Text;
            
            int index = 0;
            while ((index = text.IndexOf(query, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                _searchResults.Add(index);
                index += query.Length;
            }

            if (_searchResults.Count > 0)
            {
                // Try to find the result closest to current caret
                int caretOffset = editor.CaretOffset;
                _currentResultIndex = 0;
                
                for (int i = 0; i < _searchResults.Count; i++)
                {
                    if (_searchResults[i] >= caretOffset)
                    {
                        _currentResultIndex = i;
                        break;
                    }
                }

                HighlightCurrentResult();
            }
            
            UpdateSearchStatus();
        }

        private void HighlightCurrentResult()
        {
            if (_currentResultIndex >= 0 && _currentResultIndex < _searchResults.Count)
            {
                var editor = CurrentEditor;
                if (editor != null)
                {
                    int offset = _searchResults[_currentResultIndex];
                    int length = SearchBox.Text.Length;
                    editor.Select(offset, length);
                    editor.ScrollToLine(editor.Document.GetLineByOffset(offset).LineNumber);
                }
            }
        }

        private void UpdateSearchStatus()
        {
            if (_searchResults.Count == 0)
            {
                SearchStatus.Text = "No results";
            }
            else
            {
                SearchStatus.Text = $"{_currentResultIndex + 1} of {_searchResults.Count}";
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            PerformSearch();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    FindPrevious();
                }
                else
                {
                    FindNext();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CloseSearch();
                e.Handled = true;
            }
        }

        private void ReplaceBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
                {
                    ReplaceAll();
                }
                else
                {
                    ReplaceOne();
                }
                e.Handled = true;
            }
        }

        private void FindNext_Click(object sender, RoutedEventArgs e)
        {
            SearchMenuPopup.IsOpen = false;
            FindNext();
        }

        private void FindPrevious_Click(object sender, RoutedEventArgs e)
        {
            SearchMenuPopup.IsOpen = false;
            FindPrevious();
        }

        private void CloseSearch_Click(object sender, RoutedEventArgs e)
        {
            CloseSearch();
        }

        private void SearchIcon_Click(object sender, MouseButtonEventArgs e)
        {
            OpenSearch();
        }

        private void SearchMenu_Click(object sender, RoutedEventArgs e)
        {
            SearchMenuPopup.IsOpen = true;
        }

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            SearchMenuPopup.IsOpen = false;
            OpenSearch();
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            SearchMenuPopup.IsOpen = false;
            OpenReplace();
        }

        private void GoToLine_Click(object sender, RoutedEventArgs e)
        {
            SearchMenuPopup.IsOpen = false;
            var dialog = new GoToLineDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                var editor = CurrentEditor;
                if (editor != null)
                {
                    int line = dialog.LineNumber;
                    if (line < 1) line = 1;
                    if (line > editor.Document.LineCount) line = editor.Document.LineCount;
                    
                    var lineSegment = editor.Document.GetLineByNumber(line);
                    editor.ScrollToLine(line);
                    editor.Select(lineSegment.Offset, 0);
                }
            }
        }

        // View Menu Handlers
        private void ViewMenu_Click(object sender, RoutedEventArgs e)
        {
            ViewMenuPopup.IsOpen = true;
        }

        private void ViewEditorOnly_Click(object sender, RoutedEventArgs e)
        {
            ViewMenuPopup.IsOpen = false;
            PreviewColumn.MinWidth = 0;
            EditorColumn.Width = new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = new GridLength(0);
            PreviewColumn.Width = new GridLength(0);
        }

        private void ViewPreviewOnly_Click(object sender, RoutedEventArgs e)
        {
            ViewMenuPopup.IsOpen = false;
            EditorColumn.MinWidth = 0;
            EditorColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
            PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
        }

        private void ViewSplit_Click(object sender, RoutedEventArgs e)
        {
            ViewMenuPopup.IsOpen = false;
            EditorColumn.MinWidth = 250;
            PreviewColumn.MinWidth = 250;
            EditorColumn.Width = new GridLength(1, GridUnitType.Star);
            SplitterColumn.Width = new GridLength(4);
            PreviewColumn.Width = new GridLength(1.2, GridUnitType.Star);
        }

        private void ToggleReplace_Click(object sender, RoutedEventArgs e)
        {
            if (ReplaceRow.Visibility == Visibility.Visible)
            {
                ReplaceRow.Visibility = Visibility.Collapsed;
            }
            else
            {
                ReplaceRow.Visibility = Visibility.Visible;
            }
        }

        private void ReplaceOne_Click(object sender, RoutedEventArgs e)
        {
            ReplaceOne();
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            ReplaceAll();
        }

        private void FindNext()
        {
            if (_searchResults.Count == 0) return;
            
            _currentResultIndex++;
            if (_currentResultIndex >= _searchResults.Count)
            {
                _currentResultIndex = 0; // Wrap around
            }
            
            HighlightCurrentResult();
            UpdateSearchStatus();
        }

        private void FindPrevious()
        {
            if (_searchResults.Count == 0) return;

            _currentResultIndex--;
            if (_currentResultIndex < 0)
            {
                _currentResultIndex = _searchResults.Count - 1; // Wrap around
            }

            HighlightCurrentResult();
            UpdateSearchStatus();
        }

        private void ReplaceOne()
        {
            var editor = CurrentEditor;
            if (editor == null || _searchResults.Count == 0 || _currentResultIndex < 0) return;

            int offset = _searchResults[_currentResultIndex];
            int length = SearchBox.Text.Length;
            string newText = ReplaceBox.Text;

            editor.Document.Replace(offset, length, newText);
            
            // Re-run search to update indices
            PerformSearch();
        }

        private void ReplaceAll()
        {
            var editor = CurrentEditor;
            if (editor == null || string.IsNullOrEmpty(SearchBox.Text)) return;

            string query = SearchBox.Text;
            string newText = ReplaceBox.Text;
            string text = editor.Text;

            // Simple replace all
            string updatedText = text.Replace(query, newText);
            
            if (text != updatedText)
            {
                editor.Text = updatedText;
                PerformSearch(); // Should clear results
            }
        }

        private void ToggleComment()
        {
            var editor = CurrentEditor;
            if (editor == null) return;

            var document = editor.Document;
            var startLine = document.GetLineByOffset(editor.SelectionStart);
            var endLine = document.GetLineByOffset(editor.SelectionStart + editor.SelectionLength);

            using (document.RunUpdate())
            {
                for (int i = startLine.LineNumber; i <= endLine.LineNumber; i++)
                {
                    var line = document.GetLineByNumber(i);
                    var text = document.GetText(line);
                    if (text.TrimStart().StartsWith("%%"))
                    {
                        // Uncomment
                        int index = text.IndexOf("%%");
                        if (index >= 0)
                            document.Remove(line.Offset + index, 2);
                    }
                    else
                    {
                        // Comment
                        document.Insert(line.Offset, "%% ");
                    }
                }
            }
        }

        // Font size controls
        private void IncreaseFontSize_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentEditor != null && CurrentEditor.FontSize < MaxFontSize)
            {
                CurrentEditor.FontSize += 4;
            }
        }

        private void DecreaseFontSize_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentEditor != null && CurrentEditor.FontSize > MinFontSize)
            {
                CurrentEditor.FontSize -= 2;
            }
        }

        private void Render_Click(object sender, RoutedEventArgs e)
        {
            RenderDiagram();
        }

        private void TxtEditor_TextChanged(object? sender, EventArgs e)
        {
            if (sender is TextEditor editor)
            {
                UpdateCursorStatus();
                foreach (TabItem tab in EditorTabs.Items)
                {
                    if (tab.Content == editor)
                    {
                        UpdateTabHeader(tab);
                        break;
                    }
                }
            }
        }

        private string GetEmbeddedResource(string filename)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Mermaid." + filename;

            using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return string.Empty;
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private void RenderDiagram(bool? forceTheme = null)
        {
            // Ensure controls are initialized before accessing them
            if (PreviewWebView == null || PreviewWebView.CoreWebView2 == null) return;
            if (CboLayout == null) return;

            bool isDark = forceTheme.HasValue ? forceTheme.Value : _isPreviewDarkMode;

            string mermaidCode = CurrentEditor?.Text ?? string.Empty;
            
            // Don't render if empty
            if (string.IsNullOrWhiteSpace(mermaidCode))
            {
                var emptyBg = isDark ? "#1E1E1E" : "#FFFFFF";
                var emptyFg = isDark ? "#D4D4D4" : "#666666";
                PreviewWebView.NavigateToString($@"
                    <!DOCTYPE html>
                    <html>
                    <body style='display:flex;align-items:center;justify-content:center;height:100vh;font-family:sans-serif;color:{emptyFg};background-color:{emptyBg};'>
                        <div>Enter Mermaid diagram code and click Render (F5)</div>
                    </body>
                    </html>");
                return;
            }

            // Encode for HTML to preserve special characters and prevent injection
            string safeCode = WebUtility.HtmlEncode(mermaidCode);

            // Theme-based colors
            string theme = isDark ? "dark" : "default";
            string bodyBg = isDark ? "#1E1E1E" : "#FFFFFF";
            string bodyColor = isDark ? "#D4D4D4" : "#000000";
            string dotColor = isDark ? "#333333" : "#E5E5E5";

            // Layout
            string layout = "dagre"; // default
            string layoutConfig = "";
            if (CboLayout.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                layout = tag;
                if (layout == "elk")
                {
                    layoutConfig = ", flowchart: { defaultRenderer: 'elk' }";
                }
            }
            
            string htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        html, body {{ 
            width: 100%;
            height: 100%;
            background-color: {bodyBg} !important; 
            background-image: radial-gradient({dotColor} 1px, transparent 1px);
            background-size: 20px 20px;
            color: {bodyColor} !important; 
            font-family: 'Segoe UI', sans-serif;
            overflow: hidden;
        }}
        #diagram-container {{ 
            width: 100%; 
            height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
            overflow: hidden;
        }}
        #mermaid-diagram {{
            width: 100%;
            height: 100%;
            display: flex;
            justify-content: center;
            align-items: center;
        }}
        .mermaid {{
            width: 100%;
            height: 100%;
            display: flex;
            justify-content: center;
            align-items: center;
        }}
        .mermaid svg {{
            width: 100% !important;
            height: 100% !important;
            max-width: none !important;
            filter: drop-shadow(0 0 15px rgba(0,0,0,0.1));
        }}
        /* Enable text selection in SVG */
        .mermaid svg text,
        .mermaid svg tspan {{
            pointer-events: auto !important;
        }}
        /* Pan Mode - default */
        body.pan-mode .mermaid svg {{
            cursor: grab;
            pointer-events: auto;
        }}
        body.pan-mode .mermaid svg:active {{
            cursor: grabbing;
        }}
        body.pan-mode .mermaid svg text,
        body.pan-mode .mermaid svg tspan {{
            user-select: none !important;
            -webkit-user-select: none !important;
            cursor: grab !important;
            pointer-events: none !important;
        }}
        /* Select Mode */
        body.select-mode .mermaid svg {{
            cursor: default;
            pointer-events: none;
        }}
        body.select-mode .mermaid svg text,
        body.select-mode .mermaid svg tspan {{
            user-select: text !important;
            -webkit-user-select: text !important;
            -moz-user-select: text !important;
            -ms-user-select: text !important;
            cursor: text !important;
            pointer-events: auto !important;
        }}
        body.select-mode .mermaid svg * {{
            pointer-events: auto;
        }}
        .error {{
            color: {(isDark ? "#F48771" : "#FF0000")};
            padding: 20px;
            border: 2px solid {(isDark ? "#F48771" : "#FF0000")};
            border-radius: 5px;
            background-color: {(isDark ? "#3E1A17" : "#FFF0F0")};
        }}
        /* Zoom Controls */
        #zoom-controls {{
            position: fixed;
            bottom: 16px;
            right: 16px;
            display: flex;
            align-items: center;
            gap: 4px;
            background: {(isDark ? "rgba(28, 28, 30, 0.95)" : "rgba(255, 255, 255, 0.95)")};
            border: 1px solid {(isDark ? "#3f3f46" : "#e0e0e0")};
            border-radius: 8px;
            padding: 8px 12px;
            box-shadow: 0 4px 16px rgba(0,0,0,0.4);
            z-index: 9999;
            font-family: 'Segoe UI', sans-serif;
        }}
        #zoom-controls button {{
            background: transparent;
            border: none;
            color: {(isDark ? "#d4d4d8" : "#333")};
            font-size: 18px;
            font-weight: bold;
            width: 32px;
            height: 32px;
            cursor: pointer;
            border-radius: 4px;
            display: flex;
            align-items: center;
            justify-content: center;
        }}
        #zoom-controls button:hover {{
            background: {(isDark ? "#3f3f46" : "#e8e8e8")};
        }}
        #zoom-controls button.active {{
            background: {(isDark ? "#3f3f46" : "#0078d4")};
            color: {(isDark ? "#fff" : "#fff")};
        }}
        #zoom-controls .separator {{
            width: 1px;
            height: 24px;
            background: {(isDark ? "#3f3f46" : "#e0e0e0")};
            margin: 0 4px;
        }}
        #zoom-level {{
            color: {(isDark ? "#d4d4d8" : "#333")};
            font-size: 13px;
            font-weight: 500;
            min-width: 50px;
            text-align: center;
            margin: 0 4px;
        }}
    </style>
    <script src='https://mermaid.editor/mermaid.js'></script>
    <script src='https://mermaid.editor/svg-pan-zoom.js'></script>
</head>
<body class='pan-mode'>
    <div id='diagram-container'>
        <div id='mermaid-diagram' class='mermaid'>
{safeCode}
        </div>
    </div>
    <!-- Zoom Controls -->
    <div id='zoom-controls'>
        <button id='pan-mode' class='active' title='Pan Mode (H)'>✋</button>
        <button id='select-mode' title='Select Mode (V)'>⊙</button>
        <div class='separator'></div>
        <button id='zoom-out' title='Zoom Out'>−</button>
        <span id='zoom-level'>100%</span>
        <button id='zoom-in' title='Zoom In'>+</button>
        <button id='zoom-fit' title='Fit to View'>⊡</button>
    </div>
    <script>
        try {{
            if (typeof mermaid !== 'undefined') {{
                mermaid.initialize({{ 
                    startOnLoad: true, 
                    theme: '{theme}',
                    layout: '{layout}',
                    securityLevel: 'loose'
                    {layoutConfig}
                }});
                
                // Wait for Mermaid to render, then add pan-zoom
                setTimeout(function() {{
                    var svgElement = document.querySelector('.mermaid svg');
                    if (svgElement && typeof svgPanZoom !== 'undefined') {{
                        // Initialize svg-pan-zoom
                        var panZoomInstance = svgPanZoom(svgElement, {{
                            zoomEnabled: false, // Disable default zoom to handle it manually
                            controlIconsEnabled: false,
                            fit: true,
                            center: true,
                            minZoom: 0.01,
                            maxZoom: 100,
                            zoomScaleSensitivity: 0.5,
                            onZoom: function(newZoom) {{ notifyZoom(newZoom); }},
                            onPan: function(newPan) {{ notifyZoom(panZoomInstance.getZoom()); }}
                        }});
                        
                        function notifyZoom(zoom) {{
                            // Update HTML zoom display
                            var zoomLevel = document.getElementById('zoom-level');
                            if (zoomLevel) {{
                                zoomLevel.textContent = Math.round(zoom * 100) + '%';
                            }}
                            // Notify WPF
                            if (window.chrome && window.chrome.webview) {{
                                window.chrome.webview.postMessage({{ type: 'zoom', value: zoom }});
                            }}
                        }}
                        
                        // Expose to window for external control
                        window.panZoomInstance = panZoomInstance;
                        
                        // Initial zoom notification
                        notifyZoom(panZoomInstance.getZoom());

                        // Zoom Controls Event Handlers
                        document.getElementById('zoom-in').addEventListener('click', function() {{
                            panZoomInstance.zoomIn();
                            notifyZoom(panZoomInstance.getZoom());
                        }});
                        document.getElementById('zoom-out').addEventListener('click', function() {{
                            panZoomInstance.zoomOut();
                            notifyZoom(panZoomInstance.getZoom());
                        }});
                        document.getElementById('zoom-fit').addEventListener('click', function() {{
                            panZoomInstance.resetZoom();
                            panZoomInstance.center();
                            panZoomInstance.fit();
                            notifyZoom(panZoomInstance.getZoom());
                        }});

                        // Mode Toggle Handlers
                        var panEnabled = true;
                        var wheelHandler = null;
                        var dblClickHandler = null;
                        
                        function enablePanMode() {{
                            panEnabled = true;
                            panZoomInstance.enablePan();
                            panZoomInstance.enableDrag();
                            document.body.classList.remove('select-mode');
                            document.body.classList.add('pan-mode');
                            document.getElementById('pan-mode').classList.add('active');
                            document.getElementById('select-mode').classList.remove('active');
                            
                            // Re-attach wheel and double-click handlers
                            if (wheelHandler) svgElement.addEventListener('wheel', wheelHandler);
                            if (dblClickHandler) svgElement.addEventListener('dblclick', dblClickHandler);
                        }}
                        
                        function enableSelectMode() {{
                            panEnabled = false;
                            panZoomInstance.disablePan();
                            panZoomInstance.disableDrag();
                            document.body.classList.remove('pan-mode');
                            document.body.classList.add('select-mode');
                            document.getElementById('pan-mode').classList.remove('active');
                            document.getElementById('select-mode').classList.add('active');
                            
                            // Remove wheel and double-click handlers to prevent interference
                            if (wheelHandler) svgElement.removeEventListener('wheel', wheelHandler);
                            if (dblClickHandler) svgElement.removeEventListener('dblclick', dblClickHandler);
                        }}
                        
                        // Store handlers for removal
                        wheelHandler = function(e) {{
                            if (!panEnabled) return; // Don't process if in select mode
                            e.preventDefault();
                            
                            if (e.ctrlKey) {{
                                var zoomFactor = 0.1;
                                var zoomScale = e.deltaY < 0 ? (1 + zoomFactor) : (1 - zoomFactor);
                                panZoomInstance.zoomAtPointBy(zoomScale, {{x: e.clientX, y: e.clientY}});
                            }} else {{
                                panZoomInstance.panBy({{x: -e.deltaX, y: -e.deltaY}});
                            }}
                        }};
                        
                        dblClickHandler = function() {{
                            if (!panEnabled) return; // Don't process if in select mode
                            panZoomInstance.resetZoom();
                            panZoomInstance.center();
                            panZoomInstance.fit();
                        }};
                        
                        svgElement.addEventListener('wheel', wheelHandler);
                        svgElement.addEventListener('dblclick', dblClickHandler);
                        
                        document.getElementById('pan-mode').addEventListener('click', enablePanMode);
                        document.getElementById('select-mode').addEventListener('click', enableSelectMode);
                        
                        // Keyboard shortcuts for mode toggle
                        document.addEventListener('keydown', function(e) {{
                            if (e.key === 'h' || e.key === 'H') {{
                                enablePanMode();
                            }} else if (e.key === 'v' || e.key === 'V') {{
                                enableSelectMode();
                            }}
                        }});

                        // Mark as ready for export
                        document.body.setAttribute('data-rendered', 'true');
                    }}
                }}, 500);
            }} else {{
                document.getElementById('diagram-container').innerHTML = '<div class=""error"">mermaid.js not found. Please ensure it is embedded or in the app folder.</div>';
            }}
        }} catch (e) {{
            document.getElementById('diagram-container').innerHTML = '<div class=""error"">Error: ' + e.message + '</div>';
        }}
    </script>
</body>
</html>";

            PreviewWebView.NavigateToString(htmlContent);
        }

        private async void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewWebView.CoreWebView2 != null)
            {
                await PreviewWebView.CoreWebView2.ExecuteScriptAsync("if(window.panZoomInstance) window.panZoomInstance.zoomIn();");
            }
        }

        private async void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewWebView.CoreWebView2 != null)
            {
                await PreviewWebView.CoreWebView2.ExecuteScriptAsync("if(window.panZoomInstance) window.panZoomInstance.zoomOut();");
            }
        }

        private async void Fit_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewWebView.CoreWebView2 != null)
            {
                await PreviewWebView.CoreWebView2.ExecuteScriptAsync("if(window.panZoomInstance) { window.panZoomInstance.fit(); window.panZoomInstance.center(); }");
            }
        }

        private void FileMenu_Click(object sender, RoutedEventArgs e)
        {
            FileMenuPopup.IsOpen = true;
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            AddNewTab($"Untitled-{_untitledCount++}.mmd", "", null);
        }

        private void OpenRecent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                // Create and show the recent files submenu
                var popup = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = btn,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                    StaysOpen = false,
                    AllowsTransparency = true,
                    PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.Slide
                };

                var border = new Border
                {
                    Background = (Brush)FindResource("BgHeader"),
                    BorderBrush = (Brush)FindResource("BorderColor"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(4, 6, 4, 6),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 10,
                        ShadowDepth = 2,
                        Direction = 270,
                        Color = Colors.Black,
                        Opacity = 0.3
                    }
                };

                var stackPanel = new StackPanel { MinWidth = 300 };

                if (_recentFiles.Count == 0)
                {
                    var emptyItem = new TextBlock
                    {
                        Text = "No recent files",
                        Foreground = (Brush)new BrushConverter().ConvertFrom("#71717a"),
                        FontSize = 13,
                        Padding = new Thickness(12, 8, 12, 8)
                    };
                    stackPanel.Children.Add(emptyItem);
                }
                else
                {
                    foreach (var filePath in _recentFiles)
                    {
                        var menuBtn = new Button
                        {
                            Style = (Style)FindResource("MenuButtonStyle"),
                            Content = filePath
                        };

                        menuBtn.Click += (s, args) =>
                        {
                            popup.IsOpen = false;
                            FileMenuPopup.IsOpen = false;
                            OpenRecentFile(filePath);
                        };
                        stackPanel.Children.Add(menuBtn);
                    }

                    // Add separator and clear option
                    stackPanel.Children.Add(new Border
                    {
                        Height = 1,
                        Background = (Brush)FindResource("BorderColor"),
                        Margin = new Thickness(0, 6, 0, 6)
                    });

                    var clearBtn = new Button
                    {
                        Style = (Style)FindResource("MenuButtonStyle"),
                        Content = "Clear Recent Files"
                    };
                    clearBtn.Click += (s, args) =>
                    {
                        _recentFiles.Clear();
                        SaveRecentFiles();
                        popup.IsOpen = false;
                    };
                    stackPanel.Children.Add(clearBtn);
                }

                border.Child = stackPanel;
                popup.Child = border;
                popup.IsOpen = true;
            }
        }

        private string ShortenPath(string path, int maxLength)
        {
            if (path.Length <= maxLength) return path;
            
            var parts = path.Split(Path.DirectorySeparatorChar);
            if (parts.Length <= 3) return path;

            // Keep drive/root, ellipsis, and last 2 parts
            var shortened = parts[0] + Path.DirectorySeparatorChar + "..." + 
                           Path.DirectorySeparatorChar + parts[parts.Length - 2] + 
                           Path.DirectorySeparatorChar + parts[parts.Length - 1];
            return shortened.Length < path.Length ? shortened : path;
        }

        private void OpenRecentFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                var result = MessageBox.Show(
                    $"The file '{Path.GetFileName(filePath)}' no longer exists.\n\nRemove it from recent files?",
                    "File Not Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _recentFiles.Remove(filePath);
                    SaveRecentFiles();
                }
                return;
            }

            try
            {
                // Check if file is already open in a tab
                foreach (TabItem tab in EditorTabs.Items)
                {
                    if (tab.Tag as string == filePath)
                    {
                        EditorTabs.SelectedItem = tab;
                        return;
                    }
                }

                // Check if current tab is empty and untitled
                if (CurrentEditor != null && string.IsNullOrEmpty(CurrentEditor.Text) && CurrentFilePath == null && CurrentEditor.Document.UndoStack.IsOriginalFile)
                {
                    CurrentFilePath = filePath;
                    CurrentEditor.Text = File.ReadAllText(filePath);
                    CurrentEditor.Document.UndoStack.MarkAsOriginalFile();
                    UpdateTabHeader(EditorTabs.SelectedItem as TabItem);
                }
                else
                {
                    AddNewTab(Path.GetFileName(filePath), File.ReadAllText(filePath), filePath);
                }

                AddToRecentFiles(filePath);
                RenderDiagram();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddToRecentFiles(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            // Remove if already exists (we'll re-add at top)
            _recentFiles.Remove(filePath);

            // Add to beginning
            _recentFiles.Insert(0, filePath);

            // Trim to max
            if (_recentFiles.Count > MaxRecentFiles)
            {
                _recentFiles = _recentFiles.Take(MaxRecentFiles).ToList();
            }

            SaveRecentFiles();
        }

        private void LoadRecentFiles()
        {
            try
            {
                if (File.Exists(RecentFilesPath))
                {
                    _recentFiles = File.ReadAllLines(RecentFilesPath)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Take(MaxRecentFiles)
                        .ToList();
                }
            }
            catch
            {
                _recentFiles = new List<string>();
            }
        }

        private void SaveRecentFiles()
        {
            try
            {
                var dir = Path.GetDirectoryName(RecentFilesPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllLines(RecentFilesPath, _recentFiles);
            }
            catch
            {
                // Silently fail - not critical
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (EditorTabs.SelectedItem is TabItem tab)
            {
                CloseTab(tab);
            }
        }

        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (TabItem tab in EditorTabs.Items)
            {
                SaveTab(tab);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void EditMenu_Click(object sender, RoutedEventArgs e)
        {
            EditMenuPopup.IsOpen = true;
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            CurrentEditor?.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            CurrentEditor?.Redo();
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            CurrentEditor?.Cut();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            CurrentEditor?.Copy();
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            CurrentEditor?.Paste();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            CurrentEditor?.SelectAll();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "Mermaid Files|*.mmd;*.txt|All Files|*.*" };
            if (dialog.ShowDialog() == true)
            {
                // Check if current tab is empty and untitled, if so, use it
                if (CurrentEditor != null && string.IsNullOrEmpty(CurrentEditor.Text) && CurrentFilePath == null && CurrentEditor.Document.UndoStack.IsOriginalFile)
                {
                    CurrentFilePath = dialog.FileName;
                    CurrentEditor.Text = File.ReadAllText(dialog.FileName);
                    CurrentEditor.Document.UndoStack.MarkAsOriginalFile();
                    UpdateTabHeader(EditorTabs.SelectedItem as TabItem);
                }
                else
                {
                    // Open in new tab
                    AddNewTab(Path.GetFileName(dialog.FileName), File.ReadAllText(dialog.FileName), dialog.FileName);
                }
                AddToRecentFiles(dialog.FileName);
                RenderDiagram();
            }
        }

        private bool SaveTab(TabItem tab)
        {
            string? filePath = tab.Tag as string;
            var editor = tab.Content as TextEditor;
            
            if (editor == null) return false;

            if (string.IsNullOrEmpty(filePath))
            {
                return SaveAsTab(tab);
            }
            else
            {
                try
                {
                    File.WriteAllText(filePath, editor.Text);
                    editor.Document.UndoStack.MarkAsOriginalFile();
                    UpdateTabHeader(tab);
                    AddToRecentFiles(filePath);
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}");
                    return false;
                }
            }
        }

        private bool SaveAsTab(TabItem tab)
        {
            var editor = tab.Content as TextEditor;
            if (editor == null) return false;

            string? filePath = tab.Tag as string;
            string defaultName = filePath != null ? Path.GetFileName(filePath) : tab.Header.ToString().TrimEnd('*');

            var dialog = new SaveFileDialog { Filter = "Mermaid Files|*.mmd|Text Files|*.txt", FileName = defaultName };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, editor.Text);
                    tab.Tag = dialog.FileName; // Update file path
                    editor.Document.UndoStack.MarkAsOriginalFile();
                    UpdateTabHeader(tab);
                    AddToRecentFiles(dialog.FileName);
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (EditorTabs.SelectedItem is TabItem tab)
            {
                SaveTab(tab);
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (EditorTabs.SelectedItem is TabItem tab)
            {
                SaveAsTab(tab);
            }
        }

        private async void SaveSvg_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewWebView.CoreWebView2 == null) return;

            // We want to export in Light Mode always.
            // If we are currently in Dark Mode, we need to re-render in Light Mode temporarily.
            bool needTemporaryRender = _isPreviewDarkMode;

            try
            {
                if (needTemporaryRender)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = null;
                    handler = (s, args) => 
                    {
                        tcs.TrySetResult(true);
                    };
                    
                    PreviewWebView.CoreWebView2.NavigationCompleted += handler;
                    
                    try 
                    {
                        // Force Light Theme
                        RenderDiagram(false); 
                        await tcs.Task; // Wait for HTML to load
                    }
                    finally
                    {
                        PreviewWebView.CoreWebView2.NavigationCompleted -= handler;
                    }
                    
                    // Wait for renderer to signal readiness (mermaid rendering + panzoom init)
                    int retryCount = 0;
                    while (retryCount < 30) // 6 seconds max
                    {
                        await Task.Delay(200);
                        string result = await PreviewWebView.CoreWebView2.ExecuteScriptAsync("document.body.getAttribute('data-rendered')");
                        if (result == "\"true\"")
                        {
                            break;
                        }
                        retryCount++;
                    }
                }

                string svgContent = await PreviewWebView.CoreWebView2.ExecuteScriptAsync("document.querySelector('.mermaid svg')?.outerHTML || 'null';");
                
                if (svgContent == "null" || string.IsNullOrEmpty(svgContent))
                {
                    MessageBox.Show("No rendered diagram found. Please render a diagram first!");
                    return;
                }
                
                svgContent = System.Text.Json.JsonSerializer.Deserialize<string>(svgContent);
                
                var dialog = new SaveFileDialog { Filter = "SVG Image|*.svg", FileName = "diagram.svg" };
                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllText(dialog.FileName, svgContent ?? string.Empty);
                    var msg = new MessageDialog("Export Successful", "SVG Saved Successfully!");
                    msg.Owner = this;
                    msg.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                var msg = new MessageDialog("Export Failed", $"Could not export SVG.\nError: {ex.Message}");
                msg.Owner = this;
                msg.ShowDialog();
            }
            finally
            {
                // Restore original theme if we changed it
                if (needTemporaryRender)
                {
                    RenderDiagram(); // Uses _isPreviewDarkMode (which is still true)
                }
            }
        }

        private async void SavePng_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewWebView.CoreWebView2 == null) return;

            bool needTemporaryRender = _isPreviewDarkMode;

            try
            {
                if (needTemporaryRender)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = null;
                    handler = (s, args) => 
                    {
                        tcs.TrySetResult(true);
                    };
                    
                    PreviewWebView.CoreWebView2.NavigationCompleted += handler;
                    
                    try 
                    {
                        RenderDiagram(false);
                        await tcs.Task;
                    }
                    finally
                    {
                        PreviewWebView.CoreWebView2.NavigationCompleted -= handler;
                    }
                    
                    int retryCount = 0;
                    while (retryCount < 30)
                    {
                        await Task.Delay(200);
                        string result = await PreviewWebView.CoreWebView2.ExecuteScriptAsync("document.body.getAttribute('data-rendered')");
                        if (result == "\"true\"")
                        {
                            break;
                        }
                        retryCount++;
                    }
                }

                var dialog = new SaveFileDialog { Filter = "PNG Image|*.png", FileName = "diagram.png" };
                if (dialog.ShowDialog() == true)
                {
                    using (var stream = File.Create(dialog.FileName))
                    {
                        await PreviewWebView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
                    }
                    var msg = new MessageDialog("Export Successful", "PNG Saved Successfully!");
                    msg.Owner = this;
                    msg.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                var msg = new MessageDialog("Export Failed", $"Error saving PNG.\nError: {ex.Message}");
                msg.Owner = this;
                msg.ShowDialog();
            }
            finally
            {
                if (needTemporaryRender)
                {
                    RenderDiagram();
                }
            }
        }
        private void HelpMenu_Click(object sender, RoutedEventArgs e)
        {
            HelpMenuPopup.IsOpen = true;
        }

        private void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://github.com/knsv/mermaid/releases");
        }

        private void VisitSite_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("https://mermaid.js.org/");
        }

        private void KeyboardShortcuts_Click(object sender, RoutedEventArgs e)
        {
            var shortcuts = new StringBuilder();
            shortcuts.AppendLine("File:");
            shortcuts.AppendLine("  New Diagram\tCtrl+N");
            shortcuts.AppendLine("  Open...\t\tCtrl+O");
            shortcuts.AppendLine("  Close\t\tCtrl+W");
            shortcuts.AppendLine("  Save\t\tCtrl+S");
            shortcuts.AppendLine("  Save As...\t\tCtrl+Shift+S");
            shortcuts.AppendLine("  Save All\t\tCtrl+Alt+S");
            shortcuts.AppendLine("  Exit\t\tAlt+F4");
            shortcuts.AppendLine();
            shortcuts.AppendLine("Edit:");
            shortcuts.AppendLine("  Undo\t\tCtrl+Z");
            shortcuts.AppendLine("  Redo\t\tCtrl+Y");
            shortcuts.AppendLine("  Cut\t\tCtrl+X");
            shortcuts.AppendLine("  Copy\t\tCtrl+C");
            shortcuts.AppendLine("  Paste\t\tCtrl+V");
            shortcuts.AppendLine("  Select All\t\tCtrl+A");
            shortcuts.AppendLine();
            shortcuts.AppendLine("Search:");
            shortcuts.AppendLine("  Find...\t\tCtrl+F");
            shortcuts.AppendLine("  Find Next\t\tF3");
            shortcuts.AppendLine("  Find Previous\tShift+F3");
            shortcuts.AppendLine("  Replace...\t\tCtrl+H");
            shortcuts.AppendLine("  Go to Line...\tCtrl+G");
            shortcuts.AppendLine();
            shortcuts.AppendLine("Workflow:");
            shortcuts.AppendLine("  Render Diagram\tCtrl+Enter");
            shortcuts.AppendLine("  Export PNG\tCtrl+Shift+P");
            shortcuts.AppendLine("  Export SVG\tCtrl+Shift+V");

            MessageBox.Show(shortcuts.ToString(), "Keyboard Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                BtnMaximize.Content = "\uE923"; // Restore icon
                BtnMaximize.ToolTip = "Restore Down";
                TitleBarBorder.Padding = new Thickness(0, 6, 0, 0);
                TitleBarBorder.Height = 42; // 35 + 7 extra padding/space
                
                // Adjust for maximized window overflow (resize border compensation)
                RootGrid.Margin = new Thickness(8);
                
                // Adjust Status Bar for Maximized State
                StatusBarRow.Height = new GridLength(32);
                StatusBarBorder.Padding = new Thickness(10, 0, 10, 8);
            }
            else
            {
                BtnMaximize.Content = "\uE922"; // Maximize icon
                BtnMaximize.ToolTip = "Maximize";
                TitleBarBorder.Padding = new Thickness(0);
                TitleBarBorder.Height = 35;
                
                // Remove maximized margin
                RootGrid.Margin = new Thickness(0);

                // Restore Status Bar
                StatusBarRow.Height = new GridLength(24);
                StatusBarBorder.Padding = new Thickness(10, 0, 10, 0);
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open URL: {ex.Message}");
            }
        }
    }

    public class CurrentLineHighlightRenderer : IBackgroundRenderer
    {
        private readonly TextEditor _editor;

        public CurrentLineHighlightRenderer(TextEditor editor)
        {
            _editor = editor;
        }

        public KnownLayer Layer
        {
            get { return KnownLayer.Background; }
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_editor.Document == null) return;

            textView.EnsureVisualLines();
            var currentLine = _editor.Document.GetLineByOffset(_editor.CaretOffset);

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, currentLine))
            {
                var brush = Application.Current.Resources["CurrentLineBackground"] as Brush;
                if (brush != null)
                {
                    drawingContext.DrawRectangle(brush, null, new Rect(0, rect.Top, textView.ActualWidth, rect.Height));
                }
            }
        }
    }
}
