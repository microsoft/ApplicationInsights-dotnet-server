param ([string] $msiPath)

function Download-AgentMsi
{
	param (
		[string] $msiPath
	)

	Write-Host "Start - Agent MSI download process."

	$downloadLink = "https://statusmonitorproddiag.blob.core.windows.net/version1/ApplicationInsightsAgent.msi"
	
	Write-Host "Downloading from $downloadLink"
	$wc = New-Object System.Net.WebClient
	$wc.DownloadFile($downloadLink, $msiPath)
	
	Write-Host "End - Agent MSI download process."
}

Download-AgentMsi $msiPath