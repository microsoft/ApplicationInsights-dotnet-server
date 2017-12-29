[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)]
    [string]$buildDirectory,
	
    [Parameter(Mandatory=$True)]
    [string]$binSkimDirectory
)

#$buildDirectory = "C:\Repos\SDK\bin\Release\Src\"
#$binSkimDirectory = "C:\Repos\SDK\binSkim\"

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

    #Write-Host $_.FullName
    $sourceDir = $_.DirectoryName
    $destDir =  $sourceDir.Replace($buildDirectory, $binSkimDirectory)
    #Write-Host $sourceDir
    #Write-Host $destDir
    if (!(Test-Path $destDir)) { md $destDir }
    Copy-Item -Path $_.FullName -Destination $destDir
} 
