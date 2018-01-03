[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)]
    [string]$buildDirectory,
    
    [Parameter(Mandatory=$True)]
    [string]$binSkimDirectory
)

#$buildDirectory = "C:\Repos\SDK\bin\Release\Src\"
#$binSkimDirectory = "C:\Repos\SDK\binSkim\"

# these are dlls that end up in the bin, but do not belong to us and don't need to be scanned.
$excludedFiles = @("Microsoft.Web.Infrastructure.dll")

Write-Host "`nPARAMETERS:"  -ForegroundColor DarkCyan;
Write-Host "`tbuildDirectory:" $buildDirectory;
Write-Host "`tbinSkimDirectory:" $binSkimDirectory;

# don't need to clean folder on build server, but is needed for local dev
Write-Host "`nCreate BinSkim Directory..." -ForegroundColor DarkCyan;
if (Test-Path $binSkimDirectory) { Remove-Item $binSkimDirectory -Recurse; }

# copy all
Write-Host "`nCopy all files..." -ForegroundColor DarkCyan;
Copy-Item -Path $buildDirectory -Filter "*.dll" -Destination $binSkimDirectory -Recurse;

# delete test directories
Write-Host "`nDelete any 'Test' directories..." -ForegroundColor DarkCyan;
Get-ChildItem -Path $binSkimDirectory -Recurse -Directory | 
    Where-Object {$_ -match "Test"} |
    Remove-Item -Recurse;

# delete excluded files
if ($excludedFiles.Count -gt 0) {
    Write-Host "`nDelete excluded files..." -ForegroundColor DarkCyan;
    Get-ChildItem -Path $binSkimDirectory -Recurse -File | 
        ForEach-Object { 
            if ($excludedFiles.Contains($_.Name)) {
                Write-Host "Excluded File:" $_.FullName;
                Remove-Item $_.FullName;
            }
        } 
}

# summary for log output (file list and count)
Write-Host "`nCopied Files:" -ForegroundColor DarkCyan;

$count = 0;
Get-ChildItem -Path $binSkimDirectory -Recurse -File | 
    ForEach-Object { 
        Write-Host "`t"$_.FullName; 
        $count++;
    } 

Write-Host "`nTOTAL FILES:" $count -ForegroundColor DarkCyan;