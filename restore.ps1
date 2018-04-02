nuget install Mono.Posix -outputdirectory lib 
nuget install Mono.Security -outputdirectory lib 

copy-item lib\Mono.Posix.4.0.0.0\lib\net40\Mono.Posix.dll       lib -force
copy-item lib\Mono.Security.3.2.3.0\lib\net45\Mono.Security.dll lib -force
          