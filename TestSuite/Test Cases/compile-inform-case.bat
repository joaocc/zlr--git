@echo off
if not exist inform6.exe goto noinform
if "%1"=="" goto usage
if not exist "%1.inf" goto nocode
if not exist Compiled\nul md Compiled
inform6 -wkD +include_path=InformLibrary "%1.inf" "Compiled\%1.zcode"
move /y gameinfo.dbg "Compiled\%1.dbg"
exit /b

:usage
echo Usage: compile-game.bat {base}
echo Reads {base}.inf in the current directory.
echo Produces {base}.zcode and {base}.dbg in Compiled.
pause
exit /b

:noinform
echo Inform6.exe is missing. Please copy the compiler from a
echo recent Inform 7 build and call it Inform6.exe. I7 installs
echo the compiler by default at:
echo C:\Program Files\Inform 7\Compilers\inform-631.exe
pause
exit /b

:nocode
echo The source file "%1.inf" does not exist.
pause
exit /b
