[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)]
    [string]$buildDirectory,
    
    [Parameter(Mandatory=$True)]
    [string]$binSkimDirectory
)

#$buildDirectory = "C:\Repos\SDK\bin\Release\Src\"
#$binSkimDirectory = "C:\Repos\SDK\binSkim\"

Write-Host "`nPARAMETERS:";
Write-Host "`tbuildDirectory:" $buildDirectory;
Write-Host "`tbinSkimDirectory:" $binSkimDirectory;

# don't need to clean folder on build server, but is needed for local dev
if (Test-Path $binSkimDirectory) { Remove-Item $binSkimDirectory -Recurse; }

# copy all
Copy-Item -Path $buildDirectory -Filter "*.dll" -Destination $binSkimDirectory -Recurse;

# delete test directories
Get-ChildItem -Path $binSkimDirectory -Recurse -Directory | 
 Where-Object {$_ -match "Test"} |
 Remove-Item -Recurse;

# summary for log output (file list and count)
Write-Host "`nCOPIED FILES:";

$count = 0;
Get-ChildItem -Path $binSkimDirectory -Recurse -File | 
    ForEach-Object { 
        Write-Host "`t"$_.FullName; 
        $count++;
    } 

Write-Host "`nTOTAL FILES:" $count;