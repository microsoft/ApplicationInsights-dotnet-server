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
    md $folder
}


CreateTargetDirectory $binSkimDirectory

Get-ChildItem -Path $buildDirectory -Recurse *.dll |
 Where-Object {$_.DirectoryName -notMatch "Test"} |
 %{
    # copy files, maintaining subdirectories
    Write-Host " "

    Write-Host "fullName:" $_.FullName
    $sourceDir = $_.DirectoryName
    $destDir =  $sourceDir.Replace($buildDirectory, $binSkimDirectory)
    $destFile = $_.FullName.Replace($buildDirectory, $binSkimDirectory)
    Write-Host "sourceDir:" $sourceDir
    Write-Host "destDir:" $destDir

    if (!(Test-Path $destDir)) { md $destDir }
    if (!(Test-Path $destFile)) { Copy-Item -Path $_.FullName -Destination $destDir }
    #Copy-Item -Path $_.FullName -Destination $destDir
} 


# summary for log output
$count = Get-ChildItem -Path $binSkimDirectory -Recurse -File | Measure-Object | %{$_.Count}
Write-Host " "
Write-Host "Total Files:" $count