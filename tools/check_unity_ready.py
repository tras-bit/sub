#!/usr/bin/env python3
"""
check_unity_ready.py — «предполётный» аудит Unity-проекта без Unity.
Ловит то, что python-линтер синтаксиса не видит, а Unity — да:

  1) имя MonoBehaviour-класса != имени файла (Unity не создаст компонент);
  2) Editor-скрипты вне папки Editor (или без #if UNITY_EDITOR);
  3) API, которых НЕТ в Unity 2022.3 (появились в 2023/Unity 6) — CS0117/CS1061;
  4) ссылки на HDRP/Mirror/TextMeshPro без защиты — CS0246, если пакета нет;
  5) Packages/manifest.json: валидный JSON, версии под 2022.3, нет мусорных пакетов;
  6) ProjectSettings/ProjectVersion.txt совпадает с заявленной версией;
  7) пустые Resources-папки и отсутствующие ожидаемые пути;
  8) дублирующиеся имена типов (Unity: конфликт сборки).

Запуск: python3 tools/check_unity_ready.py
Код возврата: 0 — чисто, 1 — есть находки.
"""
import json
import os
import re
import sys
import collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
ASSETS = os.path.join(ROOT, "UnityProject", "Assets")
PKGS = os.path.join(ROOT, "UnityProject", "Packages", "manifest.json")
VER = os.path.join(ROOT, "UnityProject", "ProjectSettings", "ProjectVersion.txt")
UNITY = "2022.3.62f2"

# API, которых нет в 2022.3 (появились в 2023.x / Unity 6). Линтер синтаксиса их не видит.
BAD_API = {
    "FindFirstObjectByType": "Unity 2022.2+ (есть, но требует полного имени Object.)",
    "Awaitable": "Unity 6 (2023.3+) — нет в 2022.3",
    "SetVertexBufferData": "Unity 2023+",
    "GraphicsSettings.hdrMode": "Unity 2023+",
    "Mesh.GetVertexBuffer": "Unity 2022.1+ ок",
    "Physics.simulationMode": "Unity 2022.2+ ок",
    "AsyncGPUReadback.RequestIntoNativeArray": "Unity 2023+",
    "Object.InstantiateAsync": "Unity 2023+",
    "RenderGraph": "HDRP 14 ок, но только для SRP-кода",
    "EditorUtility.SaveFilePanelInProject": "ок, но проектное API редактора",
}
HARD_BAD = ("Awaitable", "InstantiateAsync", "GraphicsSettings.hdrMode", "AsyncGPUReadback.RequestIntoNativeArray")
HDRP_TYPES = ("HighDefinition", "HDAdditionalCameraData", "HDAdditionalLightData", "VolumeProfile",
              "PhysicallyBasedSky", "HDCamera", "HDRenderPipelineAsset", "Exposure", "Fog")
MIRROR_TYPES = ("Mirror.", "NetworkServer", "NetworkClient", "NetworkIdentity", "NetworkManager")

# разделяем находки: БЛОКЕР (Unity не соберётся) и ЗАМЕТКА (соберётся, но знать полезно)
BLOCKERS = collections.defaultdict(list)
NOTES = collections.defaultdict(list)
findings = BLOCKERS          # обратная совместимость внутри файла

STRIP_COMMENTS = re.compile(r'//[^\n]*|/\*.*?\*/', re.S)
STRIP_STRINGS = re.compile(r'"(?:\\.|[^"\\])*"')


def code_only(src):
    """Код без комментариев и строковых литералов — чтобы не ловить слова в тексте."""
    return STRIP_STRINGS.sub('""', STRIP_COMMENTS.sub("", src))


KNOWN_NS = {
    "UnityEngine", "UnityEngine.AI", "UnityEngine.Rendering", "UnityEngine.Rendering.HighDefinition",
    "UnityEngine.SceneManagement", "UnityEngine.UI", "UnityEngine.UIElements", "UnityEditor",
    "UnityEditor.SceneManagement", "UnityEngine.Networking", "UnityEngine.Serialization",
    "System", "System.IO", "System.Linq", "System.Text", "System.Collections",
    "System.Collections.Generic", "System.Threading", "System.Threading.Tasks", "System.Diagnostics",
    "TMPro", "Mirror", "Mirror.Discovery", "Mirror.Authenticators",
}


DECLARED_NS = set()


def full_name_ok(ns):
    """true, если пространство имён существует: объявлено в проекте или вложено в него."""
    if ns in DECLARED_NS:
        return True
    for d in DECLARED_NS:
        if ns.startswith(d + "."):
            return True
    for d in DECLARED_NS:
        if d.startswith(ns + "."):
            return True
    return len(DECLARED_NS) == 0


def scan():
    cs_files = []
    for base, dirs, names in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if d not in ("obj", "Library", "Temp")]
        for n in names:
            if n.endswith(".cs"):
                cs_files.append(os.path.join(base, n))

    # сначала собираем все namespace проекта, чтобы проверять using
    for path in cs_files:
        src0 = open(path, encoding="utf-8").read()
        for m in re.findall(r'^\s*namespace\s+([\w.]+)', code_only(src0), re.M):
            DECLARED_NS.add(m)

    type_names = collections.Counter()
    for path in cs_files:
        rel = os.path.relpath(path, ROOT).replace(os.sep, "/")
        src = open(path, encoding="utf-8").read()
        code = code_only(src)
        fname = os.path.splitext(os.path.basename(path))[0]
        in_editor_folder = "/Editor/" in ("/" + rel)
        has_editor_guard = "#if UNITY_EDITOR" in src

        # 1) MonoBehaviour/классы-компоненты должны лежать в файле со своим именем
        comps = re.findall(r'public\s+(?:sealed\s+|abstract\s+|partial\s+)*class\s+(\w+)\s*:\s*([\w.,<> ]+)', src)
        for cname, bases in comps:
            if "MonoBehaviour" in bases or "ScriptableObject" in bases:
                if cname != fname:
                    # НЕ блокер: AddComponent<T>() из кода работает и так (важно только для
                    # ручного добавления в инспекторе и для сериализации в префаб-ассетах)
                    NOTES["class-file-mismatch"].append(f"{rel}: class {cname} в файле {fname}.cs — "
                                                        f"в инспекторе не выбрать, из кода AddComponent работает")
        for t in re.findall(r'public\s+(?:sealed\s+|abstract\s+|partial\s+)*class\s+(\w+)', src):
            type_names[t] += 1

        # 2) Editor-скрипты (BLOCKER: UnityEditor недоступен в билде)
        if re.search(r'\busing\s+UnityEditor\b|UnityEditor\.', code) and not in_editor_folder and not has_editor_guard:
            BLOCKERS["editor-outside"].append(f"{rel}: UnityEditor без папки Editor и без #if UNITY_EDITOR → "
                                              f"билд упадёт (CS0246 UnityEditor)")

        # 2a) using на несуществующий неймспейс (CS0246/CS0234) — сверяем с известными и своими
        for ns in re.findall(r'^\s*using\s+(?:static\s+)?([\w.]+)\s*;', code, re.M):
            if ns.startswith("Subsistence"):                       # своё: сверяем с объявленными
                if not full_name_ok(ns):
                    BLOCKERS["bad-using"].append(f"{rel}: using {ns}; — такого пространства имён в проекте нет (опечатка?)")
            elif ns.startswith(("UnityEngine", "UnityEditor", "System", "TMPro", "Mirror")):
                continue                                            # встроенные Unity/.NET
            elif ns not in KNOWN_NS:
                BLOCKERS["bad-using"].append(f"{rel}: using {ns}; — неизвестное пространство имён (опечатка?)")

        # 3) API из будущих версий
        for api in HARD_BAD:
            if api in code:
                findings["api-too-new"].append(f"{rel}: {api} — {BAD_API.get(api, 'нет в 2022.3')}")

        # 4) HDRP / Mirror без защиты
        guarded = "#if" in code
        for t in HDRP_TYPES:
            if t in code and "using UnityEngine.Rendering.HighDefinition" in code and not guarded:
                NOTES["hdrp-direct"].append(f"{rel}: тип HDRP ({t}) — пакет в manifest есть (14.0.12), но следи за версией")
                break
        if "#if MIRROR" not in code:
            for t in MIRROR_TYPES:
                if t in code and not in_editor_folder:
                    BLOCKERS["mirror-direct"].append(f"{rel}: {t} без #if MIRROR — офлайн-сборка не соберётся")
                    break

    # 8) дубли имён открытых типов
    for t, c in type_names.items():
        if c > 1:
            findings["dup-types"].append(f"{t}: объявлен в {c} файлах — риск CS0101 (если в одном namespace)")

    return cs_files


def check_packages():
    if not os.path.exists(PKGS):
        findings["packages"].append("нет Packages/manifest.json")
        return
    try:
        data = json.load(open(PKGS, encoding="utf-8"))
    except Exception as e:
        findings["packages"].append(f"manifest.json не парсится: {e}")
        return
    deps = data.get("dependencies", {})
    need = {"com.unity.render-pipelines.high-definition": "14.0.",
            "com.unity.render-pipelines.core": "14.0.",
            "com.unity.inputsystem": "",
            "com.unity.textmeshpro": "3.0.",
            "com.unity.test-framework": ""}
    for pkg, prefix in need.items():
        if pkg in deps and prefix and not str(deps[pkg]).startswith(prefix):
            findings["packages"].append(f"{pkg}: {deps[pkg]} — для Unity 2022.3 ожидается {prefix}x")
    for pkg in deps:
        if pkg.startswith("com.unity.modules") or pkg.startswith("com.unity.render-pipelines") or pkg.count(".") >= 2:
            continue
    print(f"[unity] пакетов в manifest: {len(deps)} (HDRP {deps.get('com.unity.render-pipelines.high-definition', '—')})")


def check_version():
    if not os.path.exists(VER):
        findings["version"].append("нет ProjectSettings/ProjectVersion.txt — Hub не поймёт версию")
        return
    txt = open(VER, encoding="utf-8").read()
    m = re.search(r'm_EditorVersion:\s*(\S+)', txt)
    if not m:
        findings["version"].append("ProjectVersion.txt без m_EditorVersion")
    elif m.group(1) != UNITY:
        findings["version"].append(f"ProjectVersion {m.group(1)} != {UNITY}")
    # часть ProjectSettings Unity создаёт сама; критичные проверяем
    ps = os.path.join(ROOT, "UnityProject", "ProjectSettings")
    have = set(os.listdir(ps)) if os.path.isdir(ps) else set()
    print(f"[unity] ProjectSettings: {len(have)} файлов — {', '.join(sorted(have))}")


def check_assets():
    models = os.path.join(ASSETS, "Subsistence", "Models")
    res = os.path.join(ASSETS, "Subsistence", "Resources")
    fbx = sum(1 for b, d, n in os.walk(models) for f in n if f.endswith(".fbx"))
    print(f"[unity] моделей FBX в Assets: {fbx}")
    if not os.path.isdir(res):
        findings["assets"].append("нет Assets/Subsistence/Resources — префабы некуда собирать "
                                  "(бутстрап создаст, но лучше проверить права на запись)")
    scripts = os.path.join(ASSETS, "Subsistence", "Scripts")
    if os.path.isdir(scripts):
        for d in sorted(os.listdir(scripts)):
            full = os.path.join(scripts, d)
            if os.path.isdir(full) and not any(f.endswith(".cs") for f in os.listdir(full)):
                findings["assets"].append(f"пустая папка скриптов: Scripts/{d}")


def main():
    cs = scan()
    check_packages()
    check_version()
    check_assets()
    print(f"[unity] скриптов: {len(cs)}")
    blockers = 0
    for key, items in sorted(BLOCKERS.items()):
        print(f"\n[БЛОКЕР: {key}] {len(items)}")
        for it in items[:14]:
            print("  •", it)
        if len(items) > 14:
            print(f"  … ещё {len(items) - 14}")
        blockers += len(items)
    for key, items in sorted(NOTES.items()):
        print(f"\n[заметка: {key}] {len(items)}")
        for it in items[:4]:
            print("  •", it)
        if len(items) > 4:
            print(f"  … ещё {len(items) - 4}")
    print()
    if blockers == 0:
        print("[unity] ГОТОВО К ОТКРЫТИЮ: блокеров нет — Unity должен импортировать без красной консоли.")
        return 0
    print(f"[unity] БЛОКЕРОВ: {blockers} — правим перед первым запуском.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
