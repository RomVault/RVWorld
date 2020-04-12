RUNTIME := linux-x64
PUBLISH_FLAGS := --runtime $(RUNTIME) --no-build -p:PublishSingleFile=true --self-contained=true
PUBLISH_DIR := ./out

default: build publish

build:
	#dotnet build --runtime $(RUNTIME) ./DATReaderTest/DATReaderTest.csproj
	dotnet build --runtime $(RUNTIME) ./Dir2Dat/Dir2Dat.csproj
	dotnet build --runtime $(RUNTIME) ./RVCmd/RVCmd.csproj
	dotnet build --runtime $(RUNTIME) ./TrrntZipCMD/TrrntZipCMD.csproj

build-gui:
	msbuild -p:OutputPath=../$(PUBLISH_DIR)/RomVault ./ROMVault/ROMVault.csproj 
	msbuild -p:OutputPath=../$(PUBLISH_DIR)/RomVaultX ./RomVaultX/RomVaultX.csproj
	msbuild -p:OutputPath=../$(PUBLISH_DIR)/TrrntZipUI ./TrrntZipUI/TrrntZipUI.csproj

publish:
	#dotnet publish $(PUBLISH_FLAGS) --output=$(PUBLISH_DIR) ./DATReaderTest/DATReaderTest.csproj
	dotnet publish $(PUBLISH_FLAGS) --output=$(PUBLISH_DIR) ./Dir2Dat/Dir2Dat.csproj
	dotnet publish $(PUBLISH_FLAGS) --output=$(PUBLISH_DIR) ./RVCmd/RVCmd.csproj
	dotnet publish $(PUBLISH_FLAGS) --output=$(PUBLISH_DIR) ./TrrntZipCMD/TrrntZipCMD.csproj
	
install:
	cp ./out/RVCmd /usr/local/bin/rvcmd
	cp ./out/TrrntZipCMD /usr/local/bin/trrntzip
	ln -s /usr/local/bin/trrntzip /usr/local/bin/torrentzip

uninstall:
	rm /usr/local/bin/rvcmd
	rm /usr/local/bin/trrntzip
	rm /usr/local/bin/torrentzip

clean:
	rm -rf ./DATReaderTest/obj/
	rm -rf ./Dir2Dat/obj/
	rm -rf ./RVCmd/obj/
	rm -rf ./TrrntZipCMD/obj/
	rm -rf ./out