set fdir=%WINDIR%\Microsoft.NET\Framework
set msbuild=%fdir%\v4.0.30319\msbuild.exe

%msbuild% /property:Configuration=Debug "Chaos.NaCl-master\Chaos.NaCl\Chaos.NaCl.csproj"
%msbuild% /property:Configuration=Release "Chaos.NaCl-master\Chaos.NaCl\Chaos.NaCl.csproj"

move "Chaos.NaCl-master\Chaos.NaCl\bin" "..\"
move "Chaos.NaCl-master\Chaos.NaCl\obj" "..\"

::	Show this:
echo "See ".dll"-files in "..\bin" and "..\obj" folders..."

pause