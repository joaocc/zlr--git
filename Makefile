# Makefile for Mono

ConsoleZLR.exe: force_build
	cd ZLR; make ZLR.VM.dll
	cd ConsoleZLR; make ConsoleZLR.exe
	cp ZLR/ZLR.VM.dll ConsoleZLR/ConsoleZLR.exe .

force_build:
	true
