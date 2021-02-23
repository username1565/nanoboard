if "%1" EQU "run" goto just_run

echo "Wait for compilation process...";
:: Set pathway with msbuild.exe into variable %msbuild%
set fdir=%WINDIR%\Microsoft.NET\Framework
set msbuild=%fdir%\v4.0.30319\msbuild.exe

:: Compilation:

::Without adding pathway to path
::%msbuild% /p:Configuration=Release nanodb.csproj

::x86 and x64
::%msbuild% /p:OutputPath="../x64/" /p:IntermediateOutputPath="../x86/" /property:Configuration=Release nanodb.csproj

::x86 only, in the main folder. Ready to start.
::	old line, not working now
::%msbuild% /p:IntermediateOutputPath="../" /property:Configuration=Release nanodb.csproj

::____________________________________________________________________________________________________________________________________________
::		copy this files from their folders to "../"-folder, where libraties will be exists
::		because without save this files in their folders, each exe is rewritted, after next compilation.
::____________________________________________________________________________________________________________________________________________
::		FractalGen.exe
::%msbuild% /property:Configuration=Release /property:ReferencePath="bin\Debug\\" /property:StartupObject=fractalgen.Program /property:AssemblyName=FractalGen /p:IntermediateOutputPath="bin\\Release\\FractalGen\\" nanodb.csproj
::COPY /D /V /Y "bin\Release\fractalgen\FractalGen.exe" "..\FractalGen.exe"

::		NPBack.exe
::%msbuild% /property:Configuration=Release /property:ReferencePath="bin\Debug\\" /property:StartupObject=nbpack.NBPackMain /property:AssemblyName=NBPack /p:IntermediateOutputPath="bin\\Release\\NBPack\\" nanodb.csproj
::COPY /D /V /Y "bin\Release\nbpack\NBPack.exe" "..\NBPack.exe"

::		Now, no need to save three .exe-files, and nanodb.exe can be runned as "nanodb NBPack [nbpack_args]" or "nanodb FractalGen [FractalGen_args]", and this cases was added in Program.cs.
::		Compile as nanodb.exe
%msbuild% /property:Configuration=Release /property:ReferencePath="bin\Debug\\" /property:StartupObject=NDB.MainClass /property:AssemblyName=nanodb nanodb.csproj
COPY /D /V /Y "bin\Release\nanodb.exe" "..\nanodb.exe"
::____________________________________________________________________________________________________________________________________________

::		copy dll libraries in "../"-folder, because sometimes this not copied after compilation.
@IF EXIST "..\Newtonsoft.Json.dll" (
  REM if already exists - show this
  echo "Do not copy Newtonsoft.Json.dll, because already exists!"
) ELSE (
  REM else, copy
  COPY /D /V /Y "bin\Release\Newtonsoft.Json.dll" "..\Newtonsoft.Json.dll"
)

@IF EXIST "..\Chaos.NaCl.dll" (
  REM if already exists - show this
  echo "Do not copy Chaos.NaCl.dll, because already exists!"
) ELSE (
  REM else, copy
  COPY /D /V /Y "bin\Release\Chaos.NaCl.dll" "..\Chaos.NaCl.dll"
)
::____________________________________________________________________________________________________________________________________________

::x64 only, in the main folder. Ready to start.
::%msbuild% /p:OutputPath="../" /property:Configuration=Release nanodb.csproj

::%msbuild% nanodb.csproj



::Generate ../pages/version.txt, like in old version of nanoboard.
::Previous - string format:
::Thu Feb  14 06:35:54 EST 2019
::      (12) - two whitespaces.
::+ "LF" (in the end) - not CRLF
@echo off
REM Get day of week number, Sunday = 0
for /f "skip=2 tokens=2 delims=," %%a in ('WMIC Path Win32_LocalTime Get DayOfWeek /Format:csv') do set /a DowNum=%%a + 1
REM Convert day of week number to text abbreviation
for /f "tokens=%DowNum%" %%a in ("Sun Mon Tue Wed Thu Fri Sat") do set DOW=%%a
::Show day of week
::echo day_of_week = %DOW%

REM Get Month number, Jan = 0;
for /f "skip=1 tokens=2 delims=," %%b in ('WMIC Path Win32_LocalTime Get Month /Format:csv') do set /a MonNum=%%b
REM Convert day of week number to text abbreviation
for /f "tokens=%MonNum%" %%b in ("Jan Feb Mar Apr May Jun Jul Aug Sep Oct Nov Dec") do set Mon=%%b
::Show Month number
::echo Month = %Mon%

REM Just Get DAY number;
for /f "skip=1 tokens=2 delims=," %%c in ('WMIC Path Win32_LocalTime Get Day /Format:csv') do set /a Day=%%c
::Show day number
::echo Day = %Day%

REM Time without milliseconds
::Show Time without milliseconds
::echo TIME_WITHOUT_MILLISECONDS = %TIME:~0,-3%
FOR /F "skip=1 tokens=2 delims=," %%g IN ('WMIC Path Win32_LocalTime Get Year /Format:csv') DO set /a YEAR=%%g
::Show year YYYY
::echo YEAR = %YEAR%

::LF without CRLF
setlocal EnableDelayedExpansion
(set \n=^
%=Do not remove this line=%
)
::Usage:
::(echo Line1!\n!Line2
::echo Works also with quotes "!\n!line2") > ../pages/version2.txt
::But CRLF in the end.

::Create version.txt and write date and time in old canonical format
::Write multistring to file, using relative pathway

::Delete previous file if exists
del "../pages/version.txt"
::Write date and time - with LF and without CRLF in the end:
set /p ="%DOW% %Mon%  %Day% %TIME:~0,-3% EST %YEAR%!\n!"<nul >> "../pages/version.txt"

::Delete previous file if exists
del "../pages_lite/version.txt"
::Write date and time - with LF and without CRLF in the end:
set /p ="%DOW% %Mon%  %Day% %TIME:~0,-3% EST %YEAR%!\n!"<nul >> "../pages_lite/version.txt"

:: if no need to running, after compilation
::	goto end
if "%1" EQU "compile" goto end

:just_run
	echo "Just running..."

:: go to parent directory
cd ".."
::    And run compiled nanodb.exe with command-line arguments:
::  Run without lite-server and without auto-parsing JSON from large data of POST-requests to /api/upload-posts/
::"nanodb.exe" old large_POST_mode0 notif_mode0

::  Run this with lite-server and with auto-parsing JSON from large data of POST-requests to /api/upload-posts/ and UploadPosts by reading JSON from tcp-stream.
::"nanodb.exe" lite large_POST_mode1 notif_mode1

::  Run nanodb.exe with lite-serever (make this public), and enable auto-parsing JSON after write this in cache-file (writting in file is faster, because client can be disconnected).
nanodb.exe public large_POST_mode2 notif_mode2 lite_images_timeout1800000
::TEST
::nanodb.exe public large_POST_mode1 notif_mode2 lite_images_timeout1800000 bypassValidation allowReput

::don't close window, after all
:end
	pause
	exit