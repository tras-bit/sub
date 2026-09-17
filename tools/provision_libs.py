#!/usr/bin/env python3
"""
Subsistence — provision_libs.py
Скачивает недостающие системные библиотеки (Debian) и распаковывает их в локальный
sysroot, чтобы запускать Blender/bpy без root-прав.

Использование:
    python3 tools/provision_libs.py --target /home/user/.cache/blender/blender
    python3 tools/provision_libs.py --target $(python3 -c "import bpy,os;print(os.path.dirname(bpy.__file__)+'/bpy.so' 2>/dev/null" || echo /usr/local/lib/python3.13/site-packages/bpy/_bpy.so)
"""
import argparse, io, lzma, os, re, subprocess, sys, tarfile, urllib.request, shutil

DEB_MIRROR = "https://deb.debian.org/debian"
DIST = "trixie"
COMPONENT = "main"
CACHE = os.path.expanduser("~/.cache/dl")
SYSROOT = os.path.expanduser("~/.cache/sysroot")

# Стартовый набор: то, что чаще всего просят Blender/bpy в минимальных контейнерах.
SEED = [
    "libxkbcommon0", "libx11-6", "libx11-xcb1", "libxcb1", "libxau6", "libxdmcp6",
    "libxext6", "libxi6", "libxfixes3", "libxxf86vm1", "libxrender1", "libxrandr2",
    "libxinerama1", "libxcursor1", "libxcomposite1", "libxdamage1", "libsm6", "libice6",
    "libgl1", "libglx0", "libglvnd0", "libegl1", "libgomp1", "libdecor-0-0",
    "libwayland-client0", "libwayland-cursor0", "libwayland-egl1", "libxkbcommon-x11-0",
    "libdbus-1-3", "libusb-1.0-0", "libspnav0", "libepoxy0",
    "libfreetype6", "libfontconfig1", "libharfbuzz0b", "libgraphite2-3", "libbrotli1",
    "libpng16-16", "libjpeg62-turbo", "libtiff6", "libwebp7", "libwebpdemux2",
    "libopenjp2-7", "libopenexr-3-1-30", "libimath-3-1-29", "libdeflate0", "libjbig0", "liblerc4",
    "libxml2", "libnuma1", "libyaml-0-2", "liborc-0-4-0",
    "libogg0", "libvorbis0a", "libvorbisenc2", "libflac12", "libopus0", "libvpx7",
    "libmp3lame0", "libmpg123-0", "libsamplerate0", "libsndfile1", "libspeex1",
    "libtheora0", "libtwolame0", "libwavpack1", "libsdl2-2-0", "libpulse0",
    "libasound2t64", "libpipewire-0.3-0", "libudev1", "libbsd0", "libmd0",
    "libicu76", "libzstd1", "liblzma5", "libbz2-1.0", "libexpat1", "libffi8", "libtinfo6",
    "libdrm2", "libglapi-mesa",
    "libgfortran5", "libquadmath0", "libatomic1",
]

# Пакеты, которые НЕЛЬЗЯ подкладывать (ядро системы, сломает загрузку процессов).
BLACKLIST = {"libc6", "libc-bin", "libgcc-s1", "libstdc++6", "libcrypt1", "libpcre2-8-0",
             "libselinux1", "libmount1", "libblkid1", "libuuid1", "zlib1g", "libcap2",
             "libacl1", "libattr1", "libsystemd0", "liblz4-1", "libgcrypt20", "libgpg-error0"}

def log(*a): print("[provision]", *a, flush=True)

def fetch(url, path, force=False):
    if os.path.exists(path) and os.path.getsize(path) > 0 and not force:
        return path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    log("download", url)
    tmp = path + ".part"
    with urllib.request.urlopen(url, timeout=120) as r, open(tmp, "wb") as f:
        shutil.copyfileobj(r, f, 1 << 20)
    os.replace(tmp, path)
    return path

def load_index():
    idx_path = os.path.join(CACHE, f"Packages_{DIST}_{COMPONENT}_amd64.xz")
    fetch(f"{DEB_MIRROR}/dists/{DIST}/{COMPONENT}/binary-amd64/Packages.xz", idx_path)
    log("parse index")
    data = lzma.open(idx_path, "rt", encoding="utf-8", errors="replace").read()
    pkgs = {}
    for block in data.split("\n\n"):
        name = filename = None
        deps = ""
        for line in block.splitlines():
            if line.startswith("Package: "): name = line[9:].strip()
            elif line.startswith("Filename: "): filename = line[10:].strip()
            elif line.startswith("Depends: "): deps = line[9:].strip()
        if name and filename:
            pkgs[name] = {"file": filename, "deps": deps}
    log(f"index: {len(pkgs)} packages")
    return pkgs

def deb_to_tar(deb_path, dest):
    """Распаковка .deb (ar-контейнер). data.tar.* может быть .xz/.gz/.zst."""
    with open(deb_path, "rb") as f:
        raw = f.read()
    if raw[:8] != b"!<arch>\n":
        raise RuntimeError("not an ar archive: " + deb_path)
    off, members = 8, []
    while off + 60 <= len(raw):
        hdr = raw[off:off + 60]
        name = hdr[0:16].decode("utf-8", "replace").strip()
        size = int(hdr[48:58].decode().strip() or 0)
        data = raw[off + 60: off + 60 + size]
        members.append((name, data))
        off += 60 + size + (size & 1)
    for name, data in members:
        base = name.rstrip("/")
        if not base.startswith("data.tar"):
            continue
        if base.endswith(".xz"): payload = lzma.decompress(data)
        elif base.endswith(".gz"):
            import gzip; payload = gzip.decompress(data)
        elif base.endswith(".zst"):
            import zstandard; payload = zstandard.ZstdDecompressor().decompress(data, max_output_size=1 << 31)
        elif base.endswith(".tar"): payload = data
        else: continue
        with tarfile.open(fileobj=io.BytesIO(payload)) as tf:
            for m in tf.getmembers():
                if not (m.isfile() or m.issym()): continue
                target = os.path.join(dest, m.name.lstrip("./"))
                if m.issym():
                    os.makedirs(os.path.dirname(target), exist_ok=True)
                    if not os.path.lexists(target): os.symlink(m.linkname, target)
                    continue
                if not m.name.startswith(("./usr/lib", "./usr/share", "./lib")): continue
                os.makedirs(os.path.dirname(target), exist_ok=True)
                f = tf.extractfile(m)
                if f is None: continue
                with open(target, "wb") as out: shutil.copyfileobj(f, out, 1 << 20)
                try: os.chmod(target, m.mode)
                except OSError: pass
        return True
    return False

def already_present(pkgs):
    """Система уже умеет резолвить всё, что даёт пакет?"""
    pass  # упрощено: решаем по факту ldd-проверки

def resolve_and_extract(seed, index, dest):
    done, queue = set(), list(seed)
    have_libs = set()
    for d in ("/usr/lib/x86_64-linux-gnu", "/usr/lib", "/lib/x86_64-linux-gnu"):
        if os.path.isdir(d):
            for f in os.listdir(d): have_libs.add(f)
    sysroot_lib = os.path.join(dest, "usr/lib/x86_64-linux-gnu")
    if os.path.isdir(sysroot_lib): have_libs |= set(os.listdir(sysroot_lib))

    while queue:
        name = queue.pop(0)
        if name in done or name in BLACKLIST: continue
        pkg = index.get(name)
        if not pkg:
            log("skip (нет в индексе):", name); done.add(name); continue
        done.add(name)
        deb = os.path.join(CACHE, os.path.basename(pkg["file"]))
        try:
            fetch(f"{DEB_MIRROR}/{pkg['file']}", deb)
            deb_to_tar(deb, dest)
            log("extracted", name)
        except Exception as e:
            log("FAIL", name, e); continue
        # зависимости
        for dep in parse_deps(pkg["deps"]):
            if dep not in done: queue.append(dep)
    return done

def parse_deps(s):
    out = []
    for part in s.split(","):
        alt = part.split("|")[0].strip()
        alt = re.sub(r"\s*\(.*?\)", "", alt).strip()
        alt = re.sub(r":(any|native)$", "", alt).strip()
        if alt: out.append(alt)
    return out

def missing_sonames(ldd_target):
    try:
        out = subprocess.run(["ldd", ldd_target], capture_output=True, text=True, timeout=120).stdout
    except Exception as e:
        log("ldd error", e); return set()
    return set(re.findall(r"(\S+\.so[\.\d]*)\s*=>\s*not found", out)) | set(re.findall(r"^\s+(\S+\.so[\.\d]*)\s*=>\s*not found", out, re.M))

def soname_to_packages(soname, index):
    """libxkbcommon.so.0 -> пакеты, чьё имя похоже (эвристика)."""
    stem = re.sub(r"\.so.*$", "", soname).lower()
    cands = []
    for name in index:
        if name.startswith(stem) or name.startswith(stem.replace("lib", "lib", 1)):
            cands.append(name)
    # точное совпадение по «libfoo0/1/…»
    cands.sort(key=lambda n: (not re.fullmatch(re.escape(stem) + r"\d*[a-z]{0,4}\d*", n), len(n)))
    return cands[:3]

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--target", action="append", required=True, help="бинарник/модуль для ldd-проверки")
    ap.add_argument("--seed-only", action="store_true")
    args = ap.parse_args()

    os.makedirs(SYSROOT, exist_ok=True)
    index = load_index()
    todo = list(SEED)
    resolve_and_extract(todo, index, SYSROOT)

    # итеративная докачка по фактическим ошибкам линковщика
    for round_no in range(4):
        missing = set()
        for t in args.target:
            if os.path.exists(t): missing |= missing_sonames(t)
        if not missing:
            log(f"round {round_no}: все зависимости разрешены")
            break
        log(f"round {round_no}: не хватает {sorted(missing)}")
        new_pkgs = []
        for so in missing:
            for p in soname_to_packages(so, index):
                if p not in new_pkgs: new_pkgs.append(p)
        if not new_pkgs: break
        resolve_and_extract(new_pkgs, index, SYSROOT)

    env = os.path.join(os.path.expanduser("~/.cache"), "blender_env.sh")
    with open(env, "w") as f:
        f.write(f'export SUBSISTENCE_SYSROOT="{SYSROOT}"\n')
        f.write(f'export LD_LIBRARY_PATH="{SYSROOT}/usr/lib/x86_64-linux-gnu:{SYSROOT}/usr/lib:$LD_LIBRARY_PATH"\n')
        f.write(f'export PATH="{SYSROOT}/usr/bin:$PATH"\n')
    log("env written:", env)

if __name__ == "__main__":
    main()
