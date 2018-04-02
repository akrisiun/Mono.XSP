# pwsh

$msbuild15 = "msbuild15.cmd"
& $msbuild15 /v:m ../src/Mono.WebServer.XSP/Mono.WebServer.XSP.csproj

copy-item  ../src/Mono.WebServer/bin/Mono.WebServer.dll                                 bin -force
copy-item  ../src/Mono.WebServer.XSP/bin/Mono.WebServer.XSP.exe  bin -force

mono  --profile  --debug ./bin/Mono.WebServer.XSP.exe --printlog