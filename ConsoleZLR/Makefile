# Makefile for Mono

SRC=ConsoleIO.cs DumbIO.cs Program.cs Properties/AssemblyInfo.cs \
	Properties/Resources.Designer.cs

CSC=csc

ConsoleZLR.exe: $(SRC) ../ZLR/ZLR.VM.dll
	$(CSC) -out:ConsoleZLR.exe -reference:../ZLR/ZLR.VM.dll $(SRC)
