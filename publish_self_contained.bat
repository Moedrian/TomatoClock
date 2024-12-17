@echo off

dotnet publish .\src\Tomato -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts\Tomato_dotnet8