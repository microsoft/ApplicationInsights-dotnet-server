[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)]
    [string]$buildDirectory,
	
    [Parameter(Mandatory=$True)]
    [string]$binSkimDirectory
)

#$buildDirectory = "C:\Repos\SDK\bin\Release\Src\"
#$binSkimDirectory = "C:\Repos\SDK\binSkim\"

Write-Host "buildDir:" $buildDirectory;
Write-Host "binSkimDir:" $binSkimDirectory;


Function CreateTargetDirectory ($folder) {
    # don't need to clean folder on build server, but is needed for local dev
    if (Test-Path $folder) { Remove-Item $folder -Recurse; }
    mkdir $folder
}


CreateTargetDirectory $binSkimDirectory

# copy all
Copy-Item -Path $buildDirectory -Filter "*.dll" -Destination $binSkimDirectory -Recurse

# delete test directories
Get-ChildItem -Path $binSkimDirectory -Recurse -Directory | 
 Where-Object {$_ -match "Test"} |
 Remove-Item -Recurse

# summary for log output
$count = Get-ChildItem -Path $binSkimDirectory -Recurse -File | Measure-Object | ForEach-Object {$_.Count}
Write-Host " "
Write-Host "Total Files:" $count
