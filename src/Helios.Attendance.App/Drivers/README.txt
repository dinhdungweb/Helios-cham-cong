HOFFICE driver folder

Place vendor SDK files here before publishing a full offline package.

Recommended layout:

drivers\zk\zkemkeeper.dll
drivers\dtc\zkemkeeper.dll
drivers\zkteco\zkemkeeper.dll
drivers\ronald-jack\zkemkeeper.dll

Copy the whole vendor SDK folder, not only zkemkeeper.dll, because the DLL may need
other files in the same directory to register and run.

Do not commit proprietary DLL/OCX files to Git unless you have distribution rights.
