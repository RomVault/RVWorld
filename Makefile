RUNTIME := linux-x64
PUBLISH_FLAGS := --runtime $(RUNTIME) --no-build -p:PublishSingleFile=true --self-contained=false
PUBLISH_DIR := ./out

default: build publish

build:
	#dotnet build --runtime $(RUNTIME) ./DATReaderTest/DATReaderTest.csproj
	dotnet build --runtime $(RUNTIME) ./Dir2Dat/Dir2Dat.csproj
	dotnet build --runtime $(RUNTIME) ./RVCmd/RVCmd.csproj
	dotnet build --runtime $(RUNTIME) ./TrrntZipCMD/TrrntZipCMD.csproj


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
	rm -r ./DATReaderTest/obj/
	rm -r ./Dir2Dat/obj/
	rm -r ./RVCmd/obj/
	rm -r ./TrrntZipCMD/obj/
	rm -r ./out