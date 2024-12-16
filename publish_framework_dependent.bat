@echo off

dotnet publish .\src\Tomato -c Release -r win-x64 --self-contained false -o artifacts\Tomato_dotnet8