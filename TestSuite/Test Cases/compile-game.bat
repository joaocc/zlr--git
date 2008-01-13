@echo off
if not exist inform6.exe goto noinform
if "%1"=="" goto usage
inform6 -wkDv5 +include_path=Natural "%1.inf" "%1.z5"
move /y "%1.z5" "ConsoleZLR\resources\%1.z5"
move /y gameinfo.dbg "ConsoleZLR\resources\%1.dbg"
exit /b

:usage
echo Usage: compile-game.bat {base}
echo Reads {base}.inf in the current directory.
echo Produces {base}.z5 and {base}.dbg in ConsoleZLR\resources.
pause
exit /b

:noinform
echo Inform6.exe is missing. Please copy the compiler from a
echo recent Inform 7 build and call it Inform6.exe. I7 installs
echo the compiler by default at:
echo C:\Program Files\Inform 7\Compilers\inform-631.exe
pause
exit /b
