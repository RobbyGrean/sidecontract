#define MyAppName "sidecontract"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "sidecontract"
#define DistRoot "dist\sidecontract"

[Setup]
AppId=sidecontract
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\sidecontract
DefaultGroupName={#MyAppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no
Uninstallable=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\sidecontract.exe
SetupIconFile=app-icon.ico
VersionInfoVersion=1.0.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Standard Installer
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
OutputBaseFilename=sidecontract-Setup

[Files]
Source: "{#DistRoot}\sidecontract.exe"; DestDir: "{app}"; Flags: ignoreversion restartreplace
Source: "{#DistRoot}\template_tags.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DistRoot}\app_database.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#DistRoot}\Config\*"; DestDir: "{app}\Config"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#DistRoot}\Template\*"; DestDir: "{app}\Template"; Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall
Source: "{#DistRoot}\Defaults\Template\*"; DestDir: "{app}\Defaults\Template"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
Type: files; Name: "{app}\Uninstall sidecontract.exe"
Type: files; Name: "{app}\Template\Procurement\9. ใบสั่งจ้าง.docx"
Type: files; Name: "{app}\Defaults\Template\Procurement\9. ใบสั่งจ้าง.docx"

[Dirs]
Name: "{app}\Data"
Name: "{app}\Output\เงินเดือน"
Name: "{app}\Output\เอกสารจัดจ้าง"

[Icons]
Name: "{userdesktop}\sidecontract"; Filename: "{app}\sidecontract.exe"; WorkingDir: "{app}"
Name: "{userprograms}\sidecontract\sidecontract"; Filename: "{app}\sidecontract.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\sidecontract.exe"; Description: "เปิด sidecontract"; Flags: postinstall skipifsilent nowait

[Code]
function CopyDirectoryContents(const SourceDir, TargetDir: String): Boolean;
var
  FindData: TFindRec;
  SourcePath, TargetPath: String;
begin
  Result := True;
  if not ForceDirectories(TargetDir) then
  begin
    Result := False;
    Exit;
  end;

  if FindFirst(AddBackslash(SourceDir) + '*', FindData) then
  try
    repeat
      if (FindData.Name <> '.') and (FindData.Name <> '..') then
      begin
        SourcePath := AddBackslash(SourceDir) + FindData.Name;
        TargetPath := AddBackslash(TargetDir) + FindData.Name;
        if (FindData.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          if not CopyDirectoryContents(SourcePath, TargetPath) then
          begin
            Result := False;
            Break;
          end;
        end
        else if not CopyFile(SourcePath, TargetPath, False) then
        begin
          Result := False;
          Break;
        end;
      end;
    until not FindNext(FindData);
  finally
    FindClose(FindData);
  end;
end;

function BackupUserTemplates(): Boolean;
var
  SourceDir, BackupRoot, BackupDir: String;
begin
  Result := True;
  SourceDir := ExpandConstant('{app}\Template');
  if not DirExists(SourceDir) then Exit;

  BackupRoot := ExpandConstant('{app}\Data\TemplateBackups');
  if not ForceDirectories(BackupRoot) then
  begin
    Result := False;
    Exit;
  end;

  BackupDir := AddBackslash(BackupRoot) + 'Installer-' + GetDateTimeString('yyyymmdd-hhnnss', '-', ':');
  Result := CopyDirectoryContents(SourceDir, BackupDir);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not BackupUserTemplates() then
    Result := 'ไม่สามารถสำรอง Template เดิมก่อนติดตั้งได้ จึงหยุดการติดตั้งเพื่อป้องกันข้อมูลสูญหาย';
end;
