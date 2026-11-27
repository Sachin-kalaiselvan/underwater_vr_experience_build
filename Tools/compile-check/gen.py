# builds one csproj per unity asmdef so we can compile package + project code with plain dotnet
import json, os, sys, glob, re
OUT = sys.argv[1]; MODE = sys.argv[2]  # runtime | editor
U = "/home/claude/unityref/Editor/Data"
P = "/home/claude/pkgs"
BI = U + "/Resources/PackageManager/BuiltInPackages"
roots = {
  "com.unity.xr.core-utils": P+"/xr.core-utils-2.6.0/package",
  "com.unity.inputsystem": P+"/inputsystem-1.20.1/package",
  "com.unity.xr.management": P+"/xr.management-4.6.1/package",
  "com.unity.xr.legacyinputhelpers": P+"/xr.legacyinputhelpers-2.1.13/package",
  "com.unity.xr.openxr": P+"/xr.openxr-1.16.1/package",
  "com.unity.xr.interaction.toolkit": P+"/xr.interaction.toolkit-3.3.2/package",
  "com.unity.ugui": BI+"/com.unity.ugui",
  "com.unity.mathematics": P+"/mathematics-1.3.3/package",
  "com.unity.burst": P+"/burst-1.8.31/package",
  "abyss": "/home/claude/abyss/Assets",
}
pkgver = {"com.unity.xr.core-utils":"2.6.0","com.unity.inputsystem":"1.20.1","com.unity.xr.management":"4.6.1","com.unity.xr.legacyinputhelpers":"2.1.13","com.unity.xr.openxr":"1.16.1","com.unity.xr.interaction.toolkit":"3.3.2","com.unity.ugui":"2.0.0","com.unity.render-pipelines.universal":"17.3.0","com.unity.render-pipelines.core":"17.3.0","Unity":"6000.3.29","com.unity.mathematics":"1.3.3","com.unity.burst":"1.8.31"}
def vt(v): return tuple(int(x) for x in re.findall(r"\d+", v)[:3])
def match(expr, ver):
    # unity version define expressions, simple subset: [a,b) (a,b] a etc
    ver = vt(ver); expr = expr.strip()
    if not expr: return True
    if expr[0] in "[(":
        lo, hi = expr[1:-1].split(",")
        ok = True
        if lo.strip(): ok &= ver >= vt(lo) if expr[0]=="[" else ver > vt(lo)
        if hi.strip(): ok &= ver <= vt(hi) if expr[-1]=="]" else ver < vt(hi)
        return ok
    return ver >= vt(expr)
base = ["UNITY_6000_3_29","UNITY_6000_3","UNITY_6000","ENABLE_INPUT_SYSTEM","ENABLE_VR","ENABLE_XR_MODULE","ENABLE_MONO","CSHARP_7_3_OR_NEWER","UNITY_ANDROID","PLATFORM_ANDROID","UNITY_PHYSICS","UNITY_UGUI","UNITY_INPUT_SYSTEM_ENABLE_UI","UNITY_INPUT_SYSTEM_ENABLE_XR","UNITY_INPUT_SYSTEM_ENABLE_VR","UNITY_INPUT_SYSTEM_ENABLE_PHYSICS","ENABLE_VR_MODULE","DEVELOPMENT_BUILD","UNITY_ASSERTIONS","ENABLE_PROFILER","TEXTCORE_FONT_ENGINE_1_5_OR_NEWER","TEXTCORE_1_0_OR_NEWER"]
for y in range(2017, 2024):
    for m in range(1,5): base.append(f"UNITY_{y}_{m}_OR_NEWER")
for m in range(0,4): base.append(f"UNITY_6000_{m}_OR_NEWER")
base += ["UNITY_5_3_OR_NEWER","UNITY_5_4_OR_NEWER","UNITY_5_5_OR_NEWER","UNITY_5_6_OR_NEWER","UNITY_6000_OR_NEWER"]
if MODE == "editor": base += ["UNITY_EDITOR","UNITY_EDITOR_LINUX","UNITY_EDITOR_64"]

asm = {}
excluded_dirs = []
for key, root in roots.items():
    for a in glob.glob(root + "/**/*.asmdef", recursive=True):
        rel = a[len(root):]
        if "/Tests" in rel or "Samples~" in rel or "/Documentation" in rel: continue
        d = json.load(open(a))
        plats = d.get("includePlatforms", [])
        is_editor = plats == ["Editor"]
        if MODE == "runtime" and is_editor:
            excluded_dirs.append(os.path.dirname(a)); continue
        if d.get("defineConstraints") and any(c in ("UNITY_INCLUDE_TESTS",) for c in d["defineConstraints"]): continue
        asm[d["name"]] = (os.path.dirname(a), d, key)
dirs = {n: v[0] for n, v in asm.items()}
guid = {}
for key, root in roots.items():
    for a in glob.glob(root + "/**/*.asmdef", recursive=True):
        m = a + ".meta"
        if os.path.exists(m):
            g = re.search(r"guid: (\w+)", open(m).read())
            if g: guid[g.group(1)] = json.load(open(a))["name"]
def files_for(n):
    d = dirs[n]; other = [x for m, x in dirs.items() if m != n and x.startswith(d + "/")] + [x for x in excluded_dirs if x.startswith(d + "/")]
    fs = []
    for f in glob.glob(d + "/**/*.cs", recursive=True):
        if any(f.startswith(o + "/") for o in other): continue
        fs.append(f)
    return fs
unity_refs = sorted(glob.glob(U + "/Managed/UnityEngine/*.dll"))
if MODE != "editor": unity_refs = [r for r in unity_refs if "/UnityEditor." not in r]
os.makedirs(OUT, exist_ok=True)
skipped = []
for n, (d, j, key) in asm.items():
    defs = list(base)
    for vd in j.get("versionDefines", []):
        pname = vd["name"]; 
        have = pkgver.get(pname) or ("1.0.0" if pname.startswith("com.unity.modules.") else None)
        if have and match(vd.get("expression",""), have): defs.append(vd["define"])
    if n == "Unity.Burst": defs.append("BURST_UNITY_MOCK")  # only to type-check burst's own source here
    cons = j.get("defineConstraints", [])
    if any((c.lstrip("!") in defs) == c.startswith("!") for c in cons): skipped.append(n); continue
    refs = []
    for r in j.get("references", []):
        if r.startswith("GUID:") and r[5:] in guid: r = guid[r[5:]]
        r = {"Unity.ugui": "UnityEngine.UI", "GUID:43b111c4a445f446abd2c02e77750ff5": "UnityEngine.UI"}.get(r, r)
        if r in asm: refs.append(r)
    if MODE == "editor" and n == "Unity.InputSystem" and "UnityEditor.UI" in asm: refs.append("UnityEditor.UI")
    if n == "Unity.TextMeshPro" and "UnityEngine.UI" not in refs: refs.append("UnityEngine.UI")
    pre = []
    for pr in j.get("precompiledReferences", []):
        hits = glob.glob(root + "/**/" + pr, recursive=True) if False else []
    proj = f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>netstandard2.1</TargetFramework><AssemblyName>{n}</AssemblyName><LangVersion>9.0</LangVersion><EnableDefaultCompileItems>false</EnableDefaultCompileItems><AllowUnsafeBlocks>true</AllowUnsafeBlocks><NoWarn>CS0618;CS0649;CS0414;CS0169;CS0067;CS1591;CS0612;CS0672;CS8632;CS0436</NoWarn><DefineConstants>{";".join(defs)}</DefineConstants><GenerateAssemblyInfo>false</GenerateAssemblyInfo><Nullable>disable</Nullable><OutputPath>{OUT}/bin/{n}</OutputPath><AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath><ProduceReferenceAssembly>false</ProduceReferenceAssembly></PropertyGroup>
<ItemGroup>{"".join(f'<Compile Include="{f}" />' for f in files_for(n))}</ItemGroup>
<ItemGroup>{"".join(f'<Reference Include="{os.path.basename(r)[:-4]}"><HintPath>{r}</HintPath><Private>false</Private></Reference>' for r in unity_refs)}
<Reference Include="Unity.RenderPipelines.Universal.Runtime"><HintPath>{OUT}/../urpstub/bin/Unity.RenderPipelines.Universal.Runtime.dll</HintPath></Reference>
</ItemGroup>
<ItemGroup>{'<PackageReference Include="System.Runtime.CompilerServices.Unsafe" Version="6.0.0" />' if n == "Unity.Burst" else ""}</ItemGroup>
<ItemGroup>{"".join(f'<ProjectReference Include="{OUT}/{r}/{r}.csproj" />' for r in refs)}</ItemGroup>
</Project>'''
    os.makedirs(f"{OUT}/{n}", exist_ok=True)
    open(f"{OUT}/{n}/{n}.csproj", "w").write(proj)
print("projects:", len(asm) - len(skipped), "skipped by constraints:", skipped)
