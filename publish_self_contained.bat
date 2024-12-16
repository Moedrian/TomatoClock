@echo off

dotnet publish .\src\Tomato -c Release -r win-x64 --self-contained true -o artifacts\Tomato_dotnet8