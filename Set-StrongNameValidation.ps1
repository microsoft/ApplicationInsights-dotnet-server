Param(
	[Parameter(Mandatory=$false)]
	[string]
	$SnPath,
	[Parameter(Mandatory=$false)]
	[string]
	$SnPathx64,
	[Parameter(Mandatory=$false)]
	[string]
	$PublicKeyTokens="*,31bf3856ad364e35",
	[Parameter(Mandatory=$false)]
	[switch]
	$Disable
)

function Find-SnPath
{
	// get Get-Command, see if it magically can find it:
	$snCmd = Get-Command -Name "sn" -ErrorAction SilentlyContinue
	if ($snCmd)
	{
		$sn64Cmd = Join-Path -Path (Split-Path -Path $snCmd) -ChildPath "x64\sn.exe"
	}
	else
	{
		Write-Verbose "Could not find sn.exe in PATH, trying some usual suspects"
		if ($Env:WindowsSDK_ExecutablePath_x86 -and $Env:WindowsSDK_ExecutablePath_x64)
		{
			$snCmd = Join-Path -Path $Env:WindowsSDK_ExecutablePath_x86 -ChildPath "sn.exe"
			$sn64Cmd = Join-Path -Path $Env:WindowsSDK_ExecutablePath_x64 -ChildPath "sn.exe"
		}
	}

	return $snCmd,$sn64Cmd
}

if (-not $SnPath)
{
	$SnPath,$SnPathx64 = Find-SnPath
}

$command = "-Vu"
if ($Disable)
{
	$command = "-Vr"
}

& $SnPathx64 $Command $PublicKeyTokens
& $SnPath $command $PublicKeyTokens
# running both the above as a hack which is known to work. Its not clear why both are needed.
