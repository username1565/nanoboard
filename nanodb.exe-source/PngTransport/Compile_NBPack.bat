set fdir=%WINDIR%\Microsoft.NET\Framework

::	Compile from .csproj, using MSBuild.exe
::set msbuild=%fdir%\v4.0.30319\msbuild.exe
::%msbuild% /property:Configuration=Release Chaos.NaCl.csproj

::	Compile from .cs, using csc.exe
set csc=%fdir%\v4.0.30319\csc.exe

::		Include all dependencies (long line)
%csc% /main:nbpack.NBPackMain /reference:"..\bin\Debug\Newtonsoft.Json.dll" /reference:"..\bin\Debug\Chaos.NaCl.dll" NBPack.cs fractalgen.cs GZipUtil.cs ByteEncryptionUtil.cs HashCalculator.cs NanoPost.cs NanoPostPackUtil.cs Aggregator.cs WebClientX.cs CurlWebClient.cs PngUtils.cs PngStegoUtil.cs Post.cs Posts.cs Salsa20.cs "..\DataBase\PostDb.cs" "..\DataBase\PostsValidator.cs" "..\DataBase\DbPostRef.cs" "..\DataBase\HashCalculator.cs" "..\DataBase\Index.cs" "..\DataBase\Post.cs" "..\DataBase\FileUtil.cs" "..\Server\DbApiHandler.cs" "..\Server\HttpRequest.cs" "..\Server\HttpResponse.cs" "..\Server\HttpConnection.cs" "..\Server\IRequestHandler.cs" "..\Server\Configurator.cs" "..\Server\MimeType.cs" "..\Server\ErrorHandler.cs" "..\Server\StylesHandler.cs" "..\Server\FileHandler.cs" "..\Server\NotificationHandler.cs" "..\Server\StatusCode.cs" "..\Captcha\PostSignatureExt.cs" "..\Captcha\BitmapConvert.cs" "..\Captcha\ByteStringExt.cs" "..\Captcha\Captcha.cs"

::	Do not close window, after compilation, and wait to press any key.
pause