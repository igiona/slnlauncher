$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$SlnLauncherExe = "$toolsDir\SlnLauncher.exe"
Install-ChocolateyFileAssociation ".slnx" $SlnLauncherExe