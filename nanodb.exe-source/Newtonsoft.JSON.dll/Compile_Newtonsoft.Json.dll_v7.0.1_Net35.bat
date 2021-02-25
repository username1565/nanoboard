set fdir=%WINDIR%\Microsoft.NET\Framework
set msbuild=%fdir%\v4.0.30319\msbuild.exe

::	Debug
%msbuild% /property:Configuration=Debug "Newtonsoft.Json-7.0.1\Src\Newtonsoft.Json\Newtonsoft.Json.Net35.csproj"
::	Release
%msbuild% /property:Configuration=Release "Newtonsoft.Json-7.0.1\Src\Newtonsoft.Json\Newtonsoft.Json.Net35.csproj"

::	copy bin, using xcopy
xcopy "Newtonsoft.Json-7.0.1\Src\Newtonsoft.Json\bin\Debug\Net35" "..\bin\Debug" /E /H /C /I
xcopy "Newtonsoft.Json-7.0.1\Src\Newtonsoft.Json\bin\Release\Net35" "..\bin\Release" /E /H /C /I

::	copy obj, using xcopy
xcopy "Newtonsoft.Json-7.0.1\Src\Newtonsoft.Json\obj\Debug\Net35" "..\obj\Debug" /E /H /C /I
xcopy "Newtonsoft.Json-7.0.1\Src\Newtonsoft.Json\obj\Release\Net35" "..\obj\Release" /E /H /C /I

::	Show this:
echo "See ".dll"-files in "..\bin" and "..\obj" folders..."

pause
