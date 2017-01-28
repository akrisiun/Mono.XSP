mkdir Packages
nuget Install -outputDirectory Packages Mono.Posix 
nuget Install -outputDirectory Packages Mono.Security 

@REM Update-Package -Reinstall
nuget restore src\Mono.WebServer.Test\Mono.WebServer.Test.csproj
nuget restore xsp.sln

@PAUSE
