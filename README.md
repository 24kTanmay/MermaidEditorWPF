<p align="center">
  <img src="docs/repo-logo.png" alt="Mermaid Editor Logo" width="200"/>
</p>

# Mermaid Diagram Editor

A WPF application for creating and rendering Mermaid diagrams with live preview, zoom & pan support, and Dark Mode.

## 📐 Code Architecture

For developers: Check out **[CODEBASE_ARCHITECTURE.mmd](CODEBASE_ARCHITECTURE.mmd)** - A comprehensive Mermaid diagram documenting the entire codebase workflow, including:
- Application initialization and module structure
- File operations and tab management
- Editor features and rendering pipeline
- Pan/zoom and mode toggle functionality
- Export system and theme handling
- All keyboard shortcuts and event flows

Open this file in the Mermaid Editor itself to visualize the complete architecture!

## Features
- ✅ Syntax highlighting with AvalonEdit (line numbers, undo/redo)
- ✅ Live preview using WebView2
- ✅ **Dark Mode** - Toggle between light and dark themes for UI and diagrams
- ✅ **Zoom & Pan controls** - Mouse wheel zoom, click & drag to pan, double-click to reset
- ✅ **Pan/Select modes** - Switch between panning (H) and text selection (V)
- ✅ **Keyboard shortcuts** - Extensive shortcuts for all operations
- ✅ **Font size controls** - Adjust editor font size with toolbar buttons or keyboard
- ✅ **Multi-tab support** - Work with multiple diagrams simultaneously
- ✅ Export to SVG and PNG
- ✅ Open/Save .mmd files
- ✅ Recent files menu
- ✅ Find and Replace
- ✅ Go to Line

## Setup Instructions

### 1. Download Required Libraries
The project automatically downloads and includes:
- **mermaid.js** - For rendering Mermaid diagrams
- **svg-pan-zoom.js** - For zoom and pan functionality

If they're not present, run these PowerShell commands in your project directory:
```powershell
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.min.js" -OutFile "mermaid.js"
Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/npm/svg-pan-zoom@3.6.1/dist/svg-pan-zoom.min.js" -OutFile "svg-pan-zoom.js"
```

### 2. Build and Run
- Build the solution (F6)
- Run the application (F5)

## Usage

### Editor
1. Type your Mermaid diagram syntax in the left editor
2. Line numbers and syntax highlighting are enabled by default
3. Use Ctrl+Z/Y for undo/redo

### Dark Mode 🌙
- Click the **🌙 Dark** button in the toolbar to switch to Dark Mode
- The button changes to **☀️ Light** when in Dark Mode
- Dark Mode affects:
  - Application UI (toolbar, editor, background)
  - Mermaid diagram theme (renders with dark theme)
  - WebView2 preview pane
- Theme persists during the session and applies to newly rendered diagrams

### Rendering
- Click **Render (F5)** button or press **F5** to preview
- The diagram will appear in the right pane with zoom & pan controls
- Diagrams automatically use the current theme (light or dark)

### Pan & Select Modes
- **✋ Pan Mode (H)**: Default mode - click and drag to pan, zoom with mouse wheel
- **⊙ Select Mode (V)**: Switch to this mode to select and copy text from diagrams
- Toggle between modes using the buttons in the zoom controls or press **H** / **V** keys

### Zoom & Pan Controls
- 🔍 **Mouse Wheel**: Zoom in/out
- ✋ **Click + Drag**: Pan around the diagram (in Pan mode)
- 🔄 **Double Click**: Reset view (fit and center)

### Keyboard Shortcuts

#### File Operations
- **Ctrl+N**: New tab
- **Ctrl+O**: Open file
- **Ctrl+S**: Save file
- **Ctrl+Shift+S**: Save As
- **Ctrl+Alt+S**: Save All tabs
- **Ctrl+W**: Close tab

#### Editing
- **Ctrl+Z**: Undo
- **Ctrl+Y**: Redo
- **Ctrl+F**: Find
- **Ctrl+H**: Replace
- **Ctrl+G**: Go to Line
- **Ctrl+/**: Toggle comment
- **F3**: Find next
- **Shift+F3**: Find previous

#### View & Theme
- **Ctrl+1**: Split view (Editor + Preview)
- **Ctrl+2**: Editor only
- **Ctrl+3**: Preview only
- **Ctrl+Shift+T**: Toggle Editor theme
- **Ctrl+Alt+T**: Toggle Preview theme

#### Pan & Select Modes
- **H**: Pan mode (hand)
- **V**: Select mode (text cursor)

#### Rendering & Export
- **F5** or **Ctrl+Enter**: Render diagram
- **Ctrl+Shift+V**: Export SVG
- **Ctrl+Shift+P**: Export PNG

#### Zoom Controls
- **Ctrl++** or **Ctrl+=**: Zoom in
- **Ctrl+-**: Zoom out
- **Ctrl+0**: Fit to view

### Font Size
- Use toolbar buttons **A-** and **A+** to adjust editor font size
- Current font size is displayed between the buttons
- Range: 8pt - 48pt

### Export
- **Export SVG**: Save diagram as vector graphics (preserves current theme colors)
- **Export PNG**: Save diagram as screenshot (captures current theme)

## Theme Details

### Light Mode
- Clean white background
- Black text for maximum readability
- Standard Mermaid default theme
- Light gray toolbar and splitter

### Dark Mode
- Dark background (#1E1E1E) - matches Visual Studio Code
- Light gray text (#D4D4D4) for comfortable reading
- Mermaid dark theme with adjusted colors
- Dark toolbar and UI elements
- Reduced eye strain in low-light environments

## Troubleshooting
- **"mermaid.js not found"**: Ensure mermaid.js is in the bin/Debug/net8.0-windows directory
- **"svg-pan-zoom.js not found"**: Ensure svg-pan-zoom.js is copied to output
- **Syntax error**: Check your Mermaid diagram syntax at https://mermaid.js.org
- **WebView2 error**: Install WebView2 Runtime from https://developer.microsoft.com/microsoft-edge/webview2/
- **Zoom not working**: Make sure svg-pan-zoom.js is properly loaded (check the controls overlay)
- **Theme not applying**: Click Render (F5) after switching themes to update the diagram

## Large Diagrams
For complex diagrams:
1. Render the diagram (F5)
2. Use mouse wheel to zoom in/out
3. Click and drag to navigate
4. Double-click to reset and fit the entire diagram in view
5. Use Dark Mode for better contrast on complex diagrams

## Tips
- Use Dark Mode when working at night or in low-light environments
- Switch to Light Mode for printing or taking screenshots for documentation
- Font size changes apply to the editor only (diagrams scale with zoom)
- Exported SVG/PNG files capture the current theme colors
