# Current build issues with EWDK 1903:

## Resgen.exe tool not found

	```
	Traceback (most recent call last):
	  File "C:\Development\win-pv-drivers\win-installer\build.py", line 1234, in <module>
		make_mgmtagent_msi(outbuilds,signname)
	  File "C:\Development\win-pv-drivers\win-installer\build.py", line 732, in make_mgmtagent_msi
		setup_brandsat_dll(support_dll_path,culture)
	  File "C:\Development\win-pv-drivers\win-installer\build.py", line 713, in setup_brandsat_dll
		shutil.copy(source_name,target_name)
	  File "C:\Development\Python311\Lib\shutil.py", line 419, in copy
		copyfile(src, dst, follow_symlinks=follow_symlinks)
	  File "C:\Development\Python311\Lib\shutil.py", line 256, in copyfile
		with open(src, 'rb') as fsrc:
			 ^^^^^^^^^^^^^^^
	FileNotFoundError: [Errno 2] No such file or directory: 'C:\\Development\\win-pv-drivers\\BrandSupport\\brandsat.en-us.dll'
	Error encountered: Command '['python', 'win-installer/build.py', '--local', 'C:\\Users\\JRMIEB~1\\AppData\\Local\\Temp\\xen_installer_7h1a6b15', '--sign', 'XCP-ng(test)-1705585361112945900']' returned non-zero exit status 1.
	ERROR: failed: python build.py --local C:\Users\JRMIEB~1\AppData\Local\Temp\xen_installer_7h1a6b15 --sign XCP-ng(test)-1705585361112945900
	ERROR:root:failed: python build.py --local C:\Users\JRMIEB~1\AppData\Local\Temp\xen_installer_7h1a6b15 --sign XCP-ng(test)-1705585361112945900
	```
	
	This error is triggered in the build of win-installer, more specifically in make_brand_dll()
	
# Current build issues with EWDK 22H2

## make_brand_dll() fails to generate branding dll, thougt resgen.exe correctly works