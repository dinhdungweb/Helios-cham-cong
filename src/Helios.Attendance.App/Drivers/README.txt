HOFFICE driver folder

Place vendor SDK files here before publishing a full offline package.

Recommended layout:

drivers\zk\sdk.zip
drivers\zk\zkemkeeper.dll
drivers\dtc\sdk.zip
drivers\dtc\zkemkeeper.dll
drivers\zkteco\zkemkeeper.dll
drivers\ronald-jack\zkemkeeper.dll

Prefer copying the vendor sdk.zip or the whole vendor SDK folder, not only
zkemkeeper.dll. The installer copies the 32-bit DLL set to Windows SysWOW64 and
registers zkemkeeper.dll from there, matching common ZK/DTC SDK installer scripts.

Do not commit proprietary DLL/OCX files to Git unless you have distribution rights.
