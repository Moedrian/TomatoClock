@echo off

dotnet publish .\src\TomatoClock -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o artifacts\TomatoClock

pause