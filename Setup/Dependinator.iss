; Script generated by the Inno Script Studio Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!


	#define AppVersion = "0.0"
#endif

#ifndef ProductVersion
	#define ProductVersion = "0.0.0.0"
#endif

#ifndef IsSigning
	#define IsSigning = "False"
#endif

#define AppName "Dependinator"
#define AppPublisher "Michael Reichenauer"
#define AppURL "https://github.com/michael-reichenauer/Dependinator"
#define AppExeName "Dependinator.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{ee48e8b2-701f-4881-815f-dc7fd8139061}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
AppCopyright= {#AppPublisher}
DefaultDirName={pf64}\{#AppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=..
OutputBaseFilename=DependinatorSetup
VersionInfoVersion={#ProductVersion}
SetupIconFile=..\Dependinator\MainWindowViews\Application.ico     
UninstallDisplayIcon={app}\Dependinator.exe
Compression=lzma
SolidCompression=yes
AllowCancelDuringInstall=false
DisableWelcomePage=False
WizardImageFile=WizModernImage-IS.bmp
WizardSmallImageFile=WizModernSmallImage-IS.bmp
RestartIfNeededByRun=False
MinVersion=0,6.1
UsePreviousGroup=False
UsePreviousAppDir= False
AppendDefaultGroupName=False
DisableReadyMemo=True
#if (IsSigning == 'True')
SignedUninstaller=yes
SignedUninstallerDir=Sign
#endif

[Files]
; Copy all release output files to version folder
Source: "..\Dependinator\bin\Release\*"; DestDir: "{app}\{#ProductVersion}"; Flags: ignoreversion
Source: "..\Updater\bin\Release\*"; DestDir: "{app}\{#ProductVersion}"; Flags: ignoreversion
Source: "..\DependinatorVse\bin\Release\DependinatorVse.vsix"; DestDir: "{app}\{#ProductVersion}"; Flags: ignoreversion

; Copy example files as well 
Source: "..\Dependinator\bin\Release\*"; DestDir: "{app}\Example"; Flags: ignoreversion
Source: "..\Dependinator\bin\Release\Dependinator.exe"; DestDir: "{app}\Example";  DestName:"Example.exe"; Flags: ignoreversion
Source: "..\Dependinator\bin\Release\Dependinator.xml"; DestDir: "{app}\Example";  DestName:"Example.xml"; Flags: ignoreversion
Source: "..\Dependinator.dpnr"; DestDir: "{app}\Example";  DestName:"Example.dpnr"; Flags: ignoreversion

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "enableextension"; Description: "Integrate with Visual Studio"; GroupDescription: "Integration:";

[Dirs]
Name: {commonappdata}\{#AppName}; Permissions: users-modify; Flags: uninsalwaysuninstall

[Icons]
Name: "{userstartmenu}\{#AppName}"; Filename: "{app}\{#AppExeName}"     
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Run install helper tasks (add  /installextension" if enableextension task is checked
Filename: "{app}\{#ProductVersion}\{#AppExeName}"; Parameters: "/install /silent"; Flags: runhidden runminimized; Tasks: not enableextension
Filename: "{app}\{#ProductVersion}\{#AppExeName}"; Parameters: "/install /silent /installextension"; Flags: runhidden runminimized; Tasks: enableextension

; Run to register updater tasks
Filename: "{app}\{#ProductVersion}\Updater.exe"; Parameters: "/register"; Flags: runhidden runminimized

; Start program (unless silent or unchecked)
Filename: "{app}\{#AppExeName}"; Flags: nowait postinstall skipifsilent; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"

[UninstallRun]
; Delete update and renew tasks (regardless of version)
Filename: "schtasks"; Parameters: "/Delete /F /TN ""{#AppName} Update"""; Flags: runhidden runminimized
Filename: "schtasks"; Parameters: "/Delete /F /TN ""{#AppName} Renew"""; Flags: runhidden runminimized
Filename: "{app}\{#ProductVersion}\{#AppExeName}"; Parameters: "/uninstall /silent"; Flags: runhidden runminimized;

[UninstallDelete]
; Delete application program files and program data folders
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{commonappdata}\{#AppName}"
