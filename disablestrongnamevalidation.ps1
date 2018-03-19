Param(
	[Parameter(Mandatory=$false)]
	[String]
	$SnPath=$(Join-Path -Path $Env:WindowsSDK_ExecutablePath_x86 -ChildPath "sn.exe"),
	[Parameter(Mandatory=$false)]
	[String]
	$SnPathx64=$(Join-Path -Path $Env:WindowsSDK_ExecutablePath_x64 -ChildPath "sn.exe")
)

& $SnPathx64 -Vr *,31bf3856ad364e35
& $SnPath -Vr *,31bf3856ad364e35
# running both the above as a hack which is known to work. Its not clear why both are needed.
