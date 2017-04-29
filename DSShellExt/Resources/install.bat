@ECHO OFF
ECHO #######################################################
ECHO ##            DS Shell Extension                     ##
ECHO ##                                                   ##
ECHO ##           https://github.com/kkguo/DSShellExt     ##
ECHO #######################################################

REM === check and get the UAC for administrator privilege ===
REM === code from https://sites.google.com/site/eneerge/scripts/batchgotadmin
:: BatchGotAdmin
:-------------------------------------
REM  --> Check for permissions
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"

REM --> If error flag set, we do not have admin.
if '%errorlevel%' NEQ '0' (
	if '%1' EQU '1' (
		echo Cannot elevate administrator privilege
		echo Please try again with "Run as Administrator"
		echo Installation failed.
		pause
		exit /B
	) else (
		echo Requesting administrative privileges...
		goto UACPrompt
	)
) else ( goto gotAdmin )

:UACPrompt
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    echo UAC.ShellExecute "%~s0", "1", "", "runas", 1 >> "%temp%\getadmin.vbs"

    "%temp%\getadmin.vbs"
    exit /B
	
:gotAdmin
    if exist "%temp%\getadmin.vbs" ( del "%temp%\getadmin.vbs" )
    pushd "%CD%"
    CD /D "%~dp0"
:--------------------------------------

REM SYSTEMINFO | FIND /i "x64-based pc"
echo %PROCESSOR_IDENTIFIER% | FIND /i "x86"

set FRAMEWORK=%windir%\Microsoft.NET\Framework
set DOTNETVERSION=v4.0.30319
IF %ERRORLEVEL%==1 (
  set FRAMEWORK=%FRAMEWORK%64
)
set REGASM="%FRAMEWORK%\%DOTNETVERSION%\regasm.exe"
set DLL="DSShellExt.dll"
%REGASM% /codebase "%~dp0\%DLL%"

ECHO Done!
ECHO.
ECHO /-------------------------------------------------------------------\
ECHO  DSShellExt is an open-source project,
ECHO  ApkShellExt is a sister project of DSShellExt
ECHO  Please visit https://github.com/kkguo/DSShellExt for more information
ECHO \-------------------------------------------------------------------/

PAUSE
@ECHO ON
