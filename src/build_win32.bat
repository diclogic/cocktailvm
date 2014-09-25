@echo off
set CONFIG=%1
if "%CONFIG%"=="" set CONFIG=Debug
set MSBUILD=%systemroot%/Microsoft.NET/Framework/v4.0.30319/msbuild.exe
set DOTNETFRAMEWORK=v4.0
%MSBUILD% /t:Build /p:Configuration=%CONFIG%;TargetFrameworkVersion=%DOTNETFRAMEWORK% Cocktail.NET.sln
