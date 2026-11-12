"""Downloads the CC0 textures/HDRI used by the shooting range from Poly Haven (https://polyhaven.com, CC0)."""
import json, os, sys, urllib.request
OUT = sys.argv[1]
TEXTURES = ["concrete_floor_02", "concrete_wall_006", "concrete_wall_004", "wood_table_worn", "metal_plate_02", "painted_concrete"]
HDRI = "derelict_airfield_01"
UA = {"User-Agent": "texture-fetch"}
def get(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60).read()
def save(url, path):
    if os.path.exists(path): return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = get(url); open(path, "wb").write(data); print(f"  {os.path.basename(path)}  {len(data)//1024} KB")
for t in TEXTURES:
    files = json.loads(get(f"https://api.polyhaven.com/files/{t}"))
    print(t)
    for key, suffix in (("Diffuse", "diff"), ("nor_gl", "nor"), ("AO", "ao")):
        if key in files and "2k" in files[key] and "jpg" in files[key]["2k"]:
            save(files[key]["2k"]["jpg"]["url"], f"{OUT}/{t}/{t}_{suffix}.jpg")
files = json.loads(get(f"https://api.polyhaven.com/files/{HDRI}"))
print(HDRI)
save(files["hdri"]["2k"]["hdr"]["url"], f"{OUT}/HDRI/{HDRI}_2k.hdr")
open(f"{OUT}/CREDITS.txt", "w").write("Textures and HDRI from Poly Haven (https://polyhaven.com), licensed CC0.\n" + "\n".join(TEXTURES + [HDRI]) + "\n")
