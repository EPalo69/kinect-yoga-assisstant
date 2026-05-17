; ---------------------------------------------------------------
;  Inno Setup Script — YourApp + Kinect Runtime v1.8
; ---------------------------------------------------------------

#define AppName      "YAMAS"
#define AppVersion   "1.0.0"
#define AppPublisher "Evan Palo"
#define AppExeName   "YAMAS.exe"
#define AppSourceDir "..\bin\Release"
#define RedistDir    "redist"

[Setup]
AppId={{2eff106b-4692-4f3e-90c8-76be9141b9cd}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=output
OutputBaseFilename={#AppName}_Setup_v{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
<<<<<<< HEAD
<<<<<<< HEAD
PrivilegesRequired=admin
=======
>>>>>>> a788163 (feat(app): added installer)
=======
PrivilegesRequired=admin
>>>>>>> 681d108 (fix(trans): made the transition more smealess)

; Minimum Windows version (Windows 7 = 6.1)
MinVersion=6.1

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
; --- Your application files ---
Source: "{#AppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; --- Kinect Runtime redistributable (extracted to temp, deleted after install) ---
<<<<<<< HEAD
<<<<<<< HEAD
Source: "{#RedistDir}\KinectRuntime-v1.8-Setup.exe"; DestDir: "{tmp}";
=======
Source: "{#RedistDir}\KinectRuntime-v1.8-Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
>>>>>>> a788163 (feat(app): added installer)
=======
Source: "{#RedistDir}\KinectRuntime-v1.8-Setup.exe"; DestDir: "{tmp}";
>>>>>>> 681d108 (fix(trans): made the transition more smealess)

[Icons]
Name: "{group}\{#AppName}";          Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";  Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; --- Install Kinect Runtime only if not already present ---
Filename: "{tmp}\KinectRuntime-v1.8-Setup.exe"; \
  Parameters: "/passive /norestart"; \
  StatusMsg: "Installing Kinect for Windows Runtime v1.8..."; \
  Check: KinectRuntimeNotInstalled; \
<<<<<<< HEAD
<<<<<<< HEAD
  Flags: waituntilterminated runascurrentuser
=======
  Flags: waituntilterminated
>>>>>>> a788163 (feat(app): added installer)
=======
  Flags: waituntilterminated runascurrentuser
>>>>>>> 681d108 (fix(trans): made the transition more smealess)
  
; --- Launch app after install (optional) ---
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[Code]
// -------------------------------------------------------------------
//  Check if Kinect Runtime v1.8 is already installed on this machine.
//  Skips the runtime installer if it finds the registry key.
// -------------------------------------------------------------------
function KinectRuntimeNotInstalled: Boolean;
var
  InstalledVersion: String;
begin
  // Check 64-bit registry first, then fall back to 32-bit
  if RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\Kinect', 'Version', InstalledVersion) then
  begin
    Log('Kinect Runtime found: version ' + InstalledVersion + ' — skipping install.');
    Result := False;
  end
  else if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\Kinect', 'Version', InstalledVersion) then
  begin
    Log('Kinect Runtime found (WOW64): version ' + InstalledVersion + ' — skipping install.');
    Result := False;
  end
  else
  begin
    Log('Kinect Runtime not found — will install.');
    Result := True;
  end;
end;

// -------------------------------------------------------------------
//  Warn the user to plug in the Kinect before finishing.
// -------------------------------------------------------------------
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssDone then
    MsgBox(
      'Installation complete!' + #13#10 + #13#10 +
      'Please make sure your Kinect sensor is plugged into a powered USB port ' +
      'and that its power adapter is connected before launching the app.',
      mbInformation,
      MB_OK
    );
end;