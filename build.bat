@echo off
rem Compila o assistente com o compilador C# que ja vem no Windows (.NET Framework 4).
rem Nao precisa de Visual Studio, instalador nem administrador.
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Nao achei o csc.exe do .NET Framework 4 em %WINDIR%\Microsoft.NET.
  exit /b 1
)

rem Instalar por cima encerra o assistente aberto (o .exe fica travado enquanto roda).
taskkill /im Assistente.exe /f >/dev/null 2>&1

if not exist dist mkdir dist
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:dist\Assistente.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll src\*.cs
if errorlevel 1 (
  echo.
  echo Erro na compilacao. Copie as mensagens acima e mande para o Claude.
  exit /b 1
)

xcopy /y /q /i modelos dist\modelos >nul
echo.
echo Pronto: dist\Assistente.exe
echo Para abrir: clique duas vezes nele ou rode  start "" dist\Assistente.exe
