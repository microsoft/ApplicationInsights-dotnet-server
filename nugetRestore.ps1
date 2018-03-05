Param(
	[Parameter(Mandatory=$false)]
	[string]
	$NugetPath = ".\.tools\nuget.exe",
	[Parameter(Mandatory=$false)]
	[switch]
	$CleanRestore,
	[Parameter(Mandatory=$false)]
	[string[]]
	$NugetOpts,
	[Parameter(Mandatory=$false)]
	[switch]
	$SkipInit
)

Write-Verbose "Options received:"
Write-Verbose "  NugetPath: '$NugetPath'"
Write-Verbose "             Is present: $(Test-Path $NugetPath)"
Write-Verbose "  CleanRestore: $CleanRestore"
Write-Verbose "  NugetOpts: '$NugetOpts'"
Write-Verbose "  SkipInit: '$SkipInit'"

$allSlnFiles = Get-ChildItem -Recurse -Filter *.sln -Path .\
Write-Verbose "Got $($allSlnFiles.Count) SLN files."

if (-not $SkipInit) {
	& .\init.ps1
}

if ($CleanRestore) {
	& $NugetPath local all -clear
	$NugetOpts += @("-DirectDownload")
	$NugetOpts += @("-NoCache")
	
	Write-Verbose "Clean restore preparation complete."
	Write-Verbose "-- NugetOpts is now '$($NugetOpts)'"
}

$allSlnFiles | ForEach-Object {
	$sln = $PSItem
	Write-Verbose "Restoring SLN file '$($sln.Name)'"
	
	Write-Verbose "-- Command line:"
	Write-Verbose "-- $NugetPath restore $($sln.FullName) $NugetOpts"
	& $NugetPath restore $sln.FullName $NugetOpts
}
