set fdir=%WINDIR%\Microsoft.NET\Framework

::	Compile from .csproj, using MSBuild.exe
::set msbuild=%fdir%\v4.0.30319\msbuild.exe
::%msbuild% /property:Configuration=Release Chaos.NaCl.csproj

::	Compile from .cs, using csc.exe
set csc=%fdir%\v4.0.30319\csc.exe
%csc% fractalgen.cs

::	Do not close window, after compilation, and wait to press any key.
pause