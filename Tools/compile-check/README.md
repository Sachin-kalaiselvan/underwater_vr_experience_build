# compile check

Compiles ABYSS + the packages it uses with plain dotnet, no Unity editor or licence needed. Catches API mistakes early.

needs: dotnet 8 sdk, the Unity 6000.3.29f1 linux editor extracted somewhere (only `Editor/Data/Managed` and `Editor/Data/Resources/PackageManager` are used), and the package tarballs unpacked.

paths are hardcoded at the top of `gen.py`, change them for your machine.

```
python3 gen.py /tmp/rt runtime && (cd /tmp/rt/Abyss.Runtime && dotnet build)
python3 gen.py /tmp/ed editor  && (cd /tmp/ed/Abyss.Editor && dotnet build)
dotnet test LogicTests.csproj   # the pure c# unit tests
```

limits:
- URP isn't compiled (it drags in burst/collections codegen). `urpstub/Stub.cs` has the handful of URP members ABYSS and OpenXR touch, copied from URP 17.3.0 source. If you start using more URP API, add it there and check the real signature.
- shaders aren't checked at all, only Unity can compile those.
