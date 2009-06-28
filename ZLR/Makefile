# Makefile for Mono

SRC=Debugger.cs Input.cs Opcodes_0OP.cs Opcodes.cs Opcodes_VAR.cs Quetzal.cs \
	DebugInfo.cs LruCache.cs Opcodes_1OP.cs Opcodes_EXT.cs Output.cs \
	IFF.cs Objects.cs Opcodes_2OP.cs Opcodes_Impl.cs ZMachine.cs \
	Properties/AssemblyInfo.cs

CSC=csc

ZLR.VM.dll: $(SRC)
	$(CSC) -out:ZLR.VM.dll -target:library $(SRC)


