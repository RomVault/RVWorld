# RVWorld (RomVaultWorld)

## The home of RomVault 3.0

RomVaultX is the DeDup'ed version of RomVault, where everything is stored using the files SHA1

* Compress         --  7Zip & Zip compression libraries
* DATReader        --  Reads DAT files into a Class structure
* DATReaderTest    --  Stand alone DATReader Test
* Dir2Dat          --  Use DATReader to perform Dir2Dat (Experimental)
* FileHeaderReader --  No-Intro File reader code.
* ROMVault         --  The UI code for ROMVault3
* RVCore           --  The Core Engine for ROMVault3
* RVIO             --  File code that enabled long filenames
* RomVaultX        --  DeDup'ed Sqlite version of RomVault
* Trrntzip         --  Trrntzip core library code
* TrrntzipCMD      --  Commandline version of Trrntzip
* TrrntzipUI       --  UI version of Trrntzip

## Compiling from source on Linux

The project requires [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1) to build and the [.NET Core 3.1 Runtime](https://dotnet.microsoft.com/download/dotnet-core/3.1). In addition to this a standard flavor of `make` is required. If you are running Ubuntu or other Debian-based systems `make` is easiest installed via the `build-essential` package.

Compile and create release binaries in the `./out` folder:
```bash
$ make
``` 

Install tools on the local system:
```bash
$ sudo make install
``` 
This will install the tools `rvcmd` and `trrntzip` in your `/usr/local/bin` folder.

Use the following command to uninstall:
```bash
$ sudo make uninstall
```

We suggest the use of [JetBrains Rider](https://www.jetbrains.com/rider/) for development on Linux. Please note that the GUI applications are dependent on the WinForms libraries and is not officially supported on Linux. Multiple users have great success running the .NET Framework binaries with [Mono](https://www.mono-project.com/download/stable/). 

### Experimental Mono support

It is also possible to build experimental binaries using `make` or *JetBrains Rider*. Building release packages is currently not supported.

Compile binaries:
```
$ make build-gui
```

Binaries are then found in their respective subfolders in the `./out` directory.

```
$ mono /out/RomVault/ROMVault3.exe
```

**Note!** If you mix development of `netcore` with `mono` you might get some weird compilation errors. Problems are ususally solved by purging the `obj` and `bin` directories. This is automatically done as part of the `make clean` process.