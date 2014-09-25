
.PHONY: default release tags

default:
	'C:\Windows\system32\cmd.exe' '//C' 'build.bat'

clean:
	'C:\Windows\system32\cmd.exe' '//C' 'clean.bat'
	'C:\Windows\system32\cmd.exe' '//C' 'clean.bat Release'

release:
	'C:\Windows\system32\cmd.exe' '//C' 'build.bat Release'

release_test:
	'C:\Windows\system32\cmd.exe' '//C' 'build.bat Release_Test'

WINDOWS_STDLIB = /c/Program\ Files\ \(x86\)/Microsoft\ Visual\ Studio\ 10.0/VC
tags:
	@ctags -R . 
	@ctags -R -a --c-kinds=+p $(WINDOWS_STDLIB)/atlmfc/include $(WINDOWS_STDLIB)/include $(WINDOWS_SDK)/Include $(CG_SDK)/include
