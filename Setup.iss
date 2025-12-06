; MermaidEditor Inno Setup script — updated with .mmd file association

[Setup]
AppName=MermaidEditor
AppVersion=1.0
DefaultDirName={pf}\MermaidEditor
DefaultGroupName=MermaidEditor
OutputDir=.\installer
OutputBaseFilename=MermaidEditorSetup
Compression=lzma
SolidCompression=yes
SetupIconFile=c:\Users\ajayd\Documents\Project\Mermaid\Assets\Mermaid.ico
UninstallDisplayIcon={app}\MermaidEditor.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "c:\Users\ajayd\Documents\Project\Mermaid\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: desktopicon; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Icons]
Name: "{group}\MermaidEditor"; Filename: "{app}\MermaidEditor.exe"
Name: "{commondesktop}\MermaidEditor"; Filename: "{app}\MermaidEditor.exe"; Tasks: desktopicon

; -----------------------------------------------------------
; FILE ASSOCIATION FOR .mmd
; -----------------------------------------------------------

[Registry]
; Link .mmd extension to MermaidEditor.mmdfile
Root: HKCR; Subkey: ".mmd"; ValueType: string; ValueData: "MermaidEditor.mmdfile"; Flags: uninsdeletevalue

; Define display name
Root: HKCR; Subkey: "MermaidEditor.mmdfile"; ValueType: string; ValueData: "Mermaid Diagram File"; Flags: uninsdeletekey

; Icon for .mmd files
Root: HKCR; Subkey: "MermaidEditor.mmdfile\DefaultIcon"; ValueType: string; ValueData: "{app}\MermaidEditor.exe,0"

; Open command
Root: HKCR; Subkey: "MermaidEditor.mmdfile\Shell\Open\Command"; ValueType: string; ValueData: """{app}\MermaidEditor.exe"" ""%1"""

[Run]
Filename: "{app}\MermaidEditor.exe"; Description: "Launch MermaidEditor"; Flags: nowait postinstall skipifsilent
