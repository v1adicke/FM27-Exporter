# Building

Requires an FM27 install: interop assemblies are generated locally from the game and are not part of the repo.

```
dotnet-install.sh --channel 8.0 --install-dir .toolchain/dotnet
# vendor/bepinex    <- BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.793+dbc82f0.zip
# vendor/unity-libs <- https://unity.bepinex.dev/libraries/6000.3.18.zip
.toolchain/dotnet/dotnet build tools/interopgen -c Release
.toolchain/dotnet/dotnet tools/interopgen/bin/Release/net8.0/interopgen.dll "<FM27 dir>" 6000.3.18f1 vendor/unity-libs vendor/interop vendor/dummy
.toolchain/dotnet/dotnet build src/FM27Exporter -c Release
.toolchain/dotnet/dotnet test tests/FM27Exporter.Tests
scripts/package.sh
```
