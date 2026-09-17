#!/usr/bin/env python3
"""
check_cs_api.py — «второй глаз» перед упаковкой: то, что ловит уже не синтаксис,
а связи между файлами (ровно те ошибки, что Unity показал на 39 файлах):

  A. Полностью квалифицированная ссылка ведёт в несуществующий тип
     (`Subsistence.Player.PlayerInventory`, тогда как PlayerInventory — в Core).
  B. Тип проекта использован в файле, но нужного `using` (или охватывающего
     namespace) нет  →  CS0246 «type or namespace not found».  (BuildTier в
     SpawnNet/ModelLibrary, `Building.BuildTier` вместо `Core.BuildTier`.)
  C. `override` метода, у которого в родителе проекта нет virtual/abstract →
     CS0506.  (TraderNpc/LevelElevator поверх BuildDeployable.ApplyDamage.)
  D. Инициализатор поля в СТРУКТУРЕ — это C# 10, а Unity 2022.3 = C# 9 →
     CS8773/CS8983.  (ArmorStats.durabilityLossPerHit = 0.5f.)
  E. Атрибуты, которые обязаны стоять на поле, но стоят на свойстве/методе
     ([Header], [Tooltip], [SerializeField], [Range], [Space], [Min], [TextArea]) → CS0592.
  F. Синтаксис C# 10+ (file-scoped namespace, record, global using, required,
     init-аксессор) — в Unity 2022.3 недоступен.
  M. Два члена с одним именем в одном типе → CS0102 (дубль const/поля, напр.
     Names.FilingCabinet при PR_/LV_).
  N. Тип из UnityEngine.UI (Text/Image/Button/…) без `using UnityEngine.UI;` → CS0246.
  O. Вложенный тип использован ВНЕ своего внешнего класса (как Slot внутри
     InventoryUI, на который ссылался HudRuntime) → CS0246.

Запуск:  python3 tools/check_cs_api.py [папка Scripts]
Выход:   0 — чисто; 1 — есть находки; 2 — нет парсера.
Требует: pip3 install tree_sitter tree_sitter_c_sharp
"""
import os
import re
import sys
from collections import defaultdict

# Парсер C# — рядом с инструментами (tools/_vendor): окружение песочницы обнуляется
# между запусками, а линтер обязан работать всегда. Если пакеты стоят в системе, берём их.
_VENDOR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_vendor")
if os.path.isdir(_VENDOR) and _VENDOR not in sys.path:
    sys.path.insert(0, _VENDOR)

try:
    import tree_sitter_c_sharp
    from tree_sitter import Language, Parser
    _PARSER = Parser(Language(tree_sitter_c_sharp.language()))
except Exception as exc:                                     # noqa: BLE001
    print(f"[check_cs_api] НЕТ ПАРСЕРА: {exc}")
    print("                поставь: pip3 install tree_sitter tree_sitter_c_sharp")
    sys.exit(2)

TYPE_DECL = {"class_declaration", "struct_declaration", "enum_declaration",
             "interface_declaration", "delegate_declaration", "record_declaration"}
FIELD_ATTRS = {"Header", "Tooltip", "SerializeField", "Range", "Space", "Min",
               "TextArea", "HideInInspector", "Multiline"}
EXTERNAL_HINT = ("MonoBehaviour", "ScriptableObject", "StateMachineBehaviour",
                 "Editor", "EditorWindow", "MonoBehaviourPun", "NetworkBehaviour")


# ----------------------------------------------------------------- совместимость
def find_scripts_dir(argv):
    if len(argv) > 1 and os.path.isdir(argv[1]):
        return argv[1]
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.dirname(here)
    for cand in (os.path.join(root, "Assets", "Subsistence", "Scripts"),
                 os.path.join(root, "UnityProject", "Assets", "Subsistence", "Scripts")):
        if os.path.isdir(cand):
            return cand
    return root


def collect(folder):
    out = []
    for base, _d, names in os.walk(folder):
        for n in names:
            if n.endswith(".cs"):
                out.append(os.path.join(base, n))
    return sorted(out)


def text_of(node, src):
    return src[node.start_byte:node.end_byte].decode("utf-8", "replace")


def children_of(node, kind=None):
    for ch in node.children:
        if kind is None or ch.type == kind:
            yield ch


def walk(node):
    yield node
    for ch in node.children:
        yield from walk(ch)


def first_identifier(node, src):
    """`BuildTier`, `Subsistence.Core.BuildTier`, `List<BuildTier>` → кортеж частей."""
    raw = text_of(node, src).strip()
    raw = re.sub(r"<.*", "", raw).strip()            # снять дженерики
    return [p.strip() for p in raw.split(".") if p.strip()]


def modifiers(node):
    return {text_of(ch, src_of[0]) for ch in node.children if ch.type == "modifier"} \
        if False else None


# --------------------------------------------------------------------- индексация
class Index:
    def __init__(self, files, srcs):
        self.files = files
        self.srcs = srcs
        self.types = defaultdict(set)        # namespace → {type}
        self.type_ns = {}                    # type → namespace (если имя уникально)
        self.type_file = {}                  # type → файл
        self.decl_file = {}                  # type → node (декларация класса)
        self.ns_of_file = {}
        self.dupes = set()
        self._index()

    def _index(self):
        seen = defaultdict(list)
        for path in self.files:
            src = self.srcs[path]
            tree = _PARSER.parse(src)
            fns = {}
            self._walk(tree.root_node, src, path, "", fns)
            for t, ns in fns.items():
                seen[t].append(ns)
                self.types[ns].add(t)
                self.type_file.setdefault(t, path)
        for t, nss in seen.items():
            if len(set(nss)) == 1:
                self.type_ns[t] = nss[0]
            else:
                self.dupes.add(t)

    def _walk(self, node, src, path, ns, fns):
        """Обход с НАКОПЛЕНИЕМ namespace (вложенные namespace склеиваются)."""
        for ch in node.children:
            if ch.type == "namespace_declaration":
                name = ch.child_by_field_name("name")
                sub = text_of(name, src) if name is not None else ""
                full = (ns + "." + sub) if (ns and sub) else (sub or ns)
                self.ns_of_file[path] = full
                body = ch.child_by_field_name("body") or ch
                self._walk(body, src, path, full, fns)
            elif ch.type in TYPE_DECL:
                nm = ch.child_by_field_name("name")
                if nm is not None:
                    t = text_of(nm, src)
                    fns.setdefault(t, ns)
                    self.decl_file.setdefault((t, ns), (path, ch, src))
                body = ch.child_by_field_name("body")
                if body is not None:
                    self._walk(body, src, path, ns, fns)
            else:
                self._walk(ch, src, path, ns, fns)

src_of = [""]


# ------------------------------------------------------------------- проверки
def check_A_bad_qualified(idx, path, src, tree, out):
    """Subsistence.X.Y — есть ли такой тип/namespace."""
    ns_all = set(idx.types.keys())
    for node in walk(tree.root_node):
        if node.type not in ("qualified_name", "identifier"):
            continue
        # интересуют только ссылки, начинающиеся с Subsistence
        parts = text_of(node, src).split(".")
        if len(parts) < 3 or parts[0] != "Subsistence":
            continue
        # длиннейший известный namespace
        ns = None
        for cut in range(len(parts) - 1, 0, -1):
            cand = ".".join(parts[:cut])
            if cand in ns_all or any(n.startswith(cand + ".") for n in ns_all):
                ns = cand
                break
        if ns is None:
            continue
        rest = parts[len(ns.split(".")):]
        head = rest[0].split("<")[0]
        if head and head not in idx.types.get(ns, set()) and head not in idx.dupes \
                and not any(n.split(".")[-1] == head for n in ns_all):
            out.append((path, node.start_point[0] + 1, "A",
                        f"ссылка `{'.'.join(parts[:len(ns.split('.'))+1])}` — такого типа нет "
                        f"(в namespace `{ns}`: {', '.join(sorted(idx.types.get(ns, []))[:6]) or '—'})"))


def in_scope_namespaces(tree, src, path, idx):
    scopes = set()
    for node in walk(tree.root_node):
        if node.type == "namespace_declaration":
            name = node.child_by_field_name("name")
            if name:
                ns = text_of(name, src)
                parts = ns.split(".")
                for i in range(1, len(parts) + 1):
                    scopes.add(".".join(parts[:i]))
        elif node.type == "using_directive":
            raw = text_of(node, src)
            m = re.match(r"using\s+(static\s+)?([\w\.]+)\s*(=\s*[\w\.]+)?;", raw)
            if m and not m.group(3):
                scopes.add(m.group(2))
            elif m and m.group(3):                      # using Alias = Ns.Type;
                alias = raw.split("=")[0].replace("using", "").strip()
                scopes.add(alias)
    own = idx.ns_of_file.get(path)
    if own:
        parts = own.split(".")
        for i in range(1, len(parts) + 1):
            scopes.add(".".join(parts[:i]))
    return scopes


def type_field_of(node, src):
    f = node.child_by_field_name("type")
    if f is None:
        return None
    return f


def type_refs(tree):
    """Все места файла, где упомянут ТИП: объявления, дженерик-аргументы (GetComponent<T>),
    typeof(T), список баз. check_B раньше смотрел только на поле `type` — и пропускал
    `go.GetComponent<MonsterAudio>()`, из-за чего Unity ругался CS0246."""
    out = []
    for node in walk(tree.root_node):
        if node.type == "type_argument_list":
            for ch in node.children:
                if ch.type not in (",", "<", ">"):
                    out.append(ch)
        elif node.type == "typeof_expression":
            for ch in node.children:
                if ch.type not in ("typeof", "(", ")"):
                    out.append(ch)
        elif node.type == "base_list":
            for ch in node.children:
                if ch.type not in (",", ":"):
                    out.append(ch)
        else:
            f = node.child_by_field_name("type")
            if f is not None:
                out.append(f)
    return out


def check_B_missing_using(idx, path, src, tree, out):
    """Тип проекта в области видимости без using / с битым префиксом → CS0246."""
    scopes = in_scope_namespaces(tree, src, path, idx)
    own_ns = idx.ns_of_file.get(path, "")
    local_types = set()
    for node in walk(tree.root_node):
        if node.type in TYPE_DECL and node.child_by_field_name("name"):
            local_types.add(text_of(node.child_by_field_name("name"), src))

    all_ns = list(idx.types.keys())

    def looks_like_namespace(part):
        return any(n == part or n.endswith("." + part) for n in all_ns)

    def prefix_resolves(prefix):
        """`World.LootContainer` → префикс `World` резолвится от `Subsistence.UI`."""
        last = prefix.split(".")[-1]
        if looks_like_namespace(prefix) or looks_like_namespace(last):
            return True
        if last in idx.type_ns or last in idx.dupes:       # InventoryNet.Handle — вложенный тип
            return True
        for sc in scopes:
            cand = sc + "." + prefix
            if looks_like_namespace(cand) or cand.split(".")[-1] in idx.type_ns:
                return True
        return False

    reported = set()
    for t in type_refs(tree):
        if t is None:
            continue
        node = t
        parts = first_identifier(t, src)
        if not parts:
            continue
        name = parts[-1]
        if name in local_types:
            continue
        if len(parts) > 1:
            prefix = ".".join(parts[:-1])
            ns_cand = None
            for sc in sorted(scopes, key=len, reverse=True):
                if sc + "." + prefix in idx.types:
                    ns_cand = sc + "." + prefix
                    break
            if ns_cand is None and prefix in idx.types:
                ns_cand = prefix
            if ns_cand is not None:
                if name in idx.types.get(ns_cand, set()):
                    continue
                if name in idx.type_ns and idx.type_ns[name] != ns_cand:
                    out.append((path, node.start_point[0] + 1, "B",
                                f"`{prefix}.{name}` — в namespace `{ns_cand}` такого типа нет; "
                                f"`{name}` объявлен в `{idx.type_ns[name]}` (CS0246)"))
                continue
            if prefix.split(".")[-1] in idx.type_ns or prefix.split(".")[-1] in idx.dupes:
                continue                      # вложенный тип: InventoryNet.Handle
            if name not in idx.type_ns:
                continue
        else:
            if name not in idx.type_ns:
                continue
            if idx.type_ns[name] in scopes or idx.type_ns[name] == own_ns:
                continue
        decl_ns = idx.type_ns.get(name, "?")
        key = (name, node.start_point[0] + 1)
        if key in reported:
            continue
        reported.add(key)
        out.append((path, node.start_point[0] + 1, "B",
                    f"тип `{name}` (namespace `{decl_ns}`) не виден отсюда — нужен "
                    f"`using {decl_ns};` либо полное/верное имя"))


def class_map(idx):
    """type → (node, src, path, [базы])"""
    out = {}
    for (t, ns), (path, node, src) in idx.decl_file.items():
        bases = []
        for ch in node.children:
            if ch.type == "base_list":
                for b in ch.children:
                    if b.type in ("identifier", "qualified_name", "generic_name"):
                        parts = first_identifier(b, src)
                        if parts:
                            bases.append(parts[-1])
        out[t] = (node, src, path, bases)
    return out


def declared_members(node, src):
    """{(name): {modifier}} для методов/свойств/полей класса."""
    res = {}
    for ch in walk(node):
        if ch.type in ("method_declaration", "property_declaration", "constructor_declaration"):
            nm = ch.child_by_field_name("name")
            if nm is None:
                continue
            mods = {text_of(m, src) for m in children_of(ch, "modifier")}
            res.setdefault(text_of(nm, src), set()).update(mods)
    return res


def check_C_override(idx, path, src, tree, out, cmap):
    for node in walk(tree.root_node):
        if node.type != "method_declaration":
            continue
        mods = {text_of(m, src) for m in children_of(node, "modifier")}
        if "override" not in mods:
            continue
        nm = node.child_by_field_name("name")
        if nm is None:
            continue
        name = text_of(nm, src)
        # найти охватывающий класс
        parent = node.parent
        cls = None
        while parent is not None:
            if parent.type in TYPE_DECL:
                cls = parent
                break
            parent = parent.parent
        if cls is None:
            continue
        cname = text_of(cls.child_by_field_name("name"), src)
        chain = cmap.get(cname)
        if chain is None:
            continue
        cn, csrc, cpath, bases = chain
        # идём вверх по проектной цепочке наследования
        cur_bases = list(bases)
        seen = set()
        found = False
        project_base = False
        while cur_bases:
            b = cur_bases.pop(0)
            if b in seen:
                continue
            seen.add(b)
            if b not in cmap:
                if any(h.lower() in b.lower() for h in EXTERNAL_HINT):
                    continue
                continue
            project_base = True
            bnode, bsrc, bpath, bbases = cmap[b]
            mem = declared_members(bnode, bsrc)
            if name in mem and ({"virtual", "abstract", "override"} & mem[name]):
                found = True
                break
            cur_bases.extend(bbases)
        if project_base and not found:
            out.append((path, node.start_point[0] + 1, "C",
                        f"`override {name}` в `{cname}`, но ни в одном проектном родителе нет "
                        f"virtual/abstract `{name}` — CS0506 (сделай базовый метод virtual)"))


def check_D_struct_fields(idx, path, src, tree, out):
    for node in walk(tree.root_node):
        if node.type != "struct_declaration":
            continue
        nm = node.child_by_field_name("name")
        sname = text_of(nm, src) if nm is not None else "?"
        body = node.child_by_field_name("body")
        if body is None:
            continue
        for fld in walk(body):
            if fld.type != "field_declaration":
                continue
            if any(x.type == "struct_declaration" for x in walk(fld)):
                continue
            mods = {text_of(m, src) for m in children_of(fld, "modifier")}
            if {"static", "const"} & mods:
                continue
            has_init = False
            for d in walk(fld):
                if d.type == "variable_declarator" and "=" in text_of(d, src):
                    has_init = True
                    break
            if has_init:
                out.append((path, fld.start_point[0] + 1, "D",
                            f"поле структуры `{sname}` с инициализатором — в C# 9 (Unity 2022.3) это "
                            f"запрещено (CS8773/CS8983): убери `= …` и выставь умолчание в коде"))


def check_E_attrs(idx, path, src, tree, out):
    FIELD_KINDS = ("field_declaration", "event_field_declaration")
    for node in walk(tree.root_node):
        if node.type != "attribute_list":
            continue
        names = set()
        for a in walk(node):
            if a.type == "attribute":
                nm = a.child_by_field_name("name")
                if nm is not None:
                    names.add(text_of(nm, src).split(".")[-1])
        hit = names & FIELD_ATTRS
        if not hit:
            continue
        par = node.parent
        if par is None or par.type in FIELD_KINDS:
            continue                      # норма: атрибут на поле
        out.append((path, node.start_point[0] + 1, "E",
                    f"[{', '.join(sorted(hit))}] висит на `{par.type}` — такой атрибут допустим "
                    f"только на поле (CS0592)"))


def check_F_langversion(idx, path, src, tree, out):
    kinds = {
        "file_scoped_namespace_declaration": "file-scoped namespace (C# 10)",
        "record_declaration": "record (C# 9 — только с явным полем/compat; в Unity 2022 лучше класс)",
        "global_statement": "global using (C# 10)",
    }
    for node in walk(tree.root_node):
        if node.type in kinds:
            out.append((path, node.start_point[0] + 1, "F", kinds[node.type]))
        if node.type == "modifier" and text_of(node, src) == "required":
            out.append((path, node.start_point[0] + 1, "F", "`required` — это C# 11, в Unity 2022.3 нельзя"))
        if node.type == "accessor_declaration":
            mods = {text_of(m, src) for m in children_of(node, "modifier")}
            if "init" in text_of(node, src).split():
                out.append((path, node.start_point[0] + 1, "F", "`init`-аксессор — C# 9 нет в Unity 2022.3"))



def receiver_type_name(node, src, idx):
    """Имя проектного типа из приёмника: `LootSpawner`, `World.LootSpawner`,
    `Subsistence.World.LootSpawner` → `LootSpawner`."""
    if node.type == "identifier":
        t = text_of(node, src)
        return t if t in idx.type_ns else None
    if node.type in ("qualified_name", "member_access_expression"):
        parts = []
        cur = node
        while cur is not None and cur.type in ("qualified_name", "member_access_expression", "member_binding_expression"):
            nm = cur.child_by_field_name("name")
            if nm is not None:
                parts.append(text_of(nm, src))
            cur = cur.child_by_field_name("expression") or cur.child_by_field_name("qualifier")
        if cur is not None and cur.type == "identifier":
            parts.append(text_of(cur, src))
        parts.reverse()
        if parts and parts[-1] in idx.type_ns:
            return parts[-1]
    return None


UNITY_INSTANCE_OK = {
    # члены UnityEngine.Object / Component / MonoBehaviour, которые законно звать у проектных
    # классов с внешней базой — чтобы не ловить ложные CS1061
    "transform", "gameObject", "name", "tag", "enabled", "isActiveAndEnabled", "activeSelf",
    "GetComponent", "GetComponents", "GetComponentInChildren", "GetComponentInParent",
    "GetComponentsInChildren", "GetComponentsInParent", "TryGetComponent", "CompareTag",
    "StartCoroutine", "StopCoroutine", "StopAllCoroutines", "Invoke", "InvokeRepeating",
    "CancelInvoke", "IsInvoking", "SendMessage", "BroadcastMessage", "SetActive",
    "Instantiate", "Destroy", "DontDestroyOnLoad", "FindObjectOfType", "FindAnyObjectByType",
    "position", "rotation", "localPosition", "localRotation", "localScale", "parent",
    "up", "right", "forward", "localToWorldMatrix", "worldToLocalMatrix",
}


UNITY_STATIC_OK = {
    # статические/унаследованные от UnityEngine.Object имена, которые могут встретиться
    # в вызове вида `Type.M(...)` у класса, унаследованного от MonoBehaviour
    "GetComponent", "GetComponents", "GetComponentInChildren", "GetComponentInParent",
    "FindObjectOfType", "FindObjectsOfType", "FindAnyObjectByType", "Instantiate",
    "Destroy", "DestroyImmediate", "DontDestroyOnLoad", "StartCoroutine", "StopCoroutine",
    "StopAllCoroutines", "Invoke", "InvokeRepeating", "CancelInvoke", "CompareTag",
    "SendMessage", "BroadcastMessage", "IsInvoking", "FindObjectsByType",
}


def check_G_static_members(idx, path, src, tree, out, cmap):
    """`ПроектныйТип.Метод(...)` — есть ли такой метод в типе (или в проектных родителях)."""
    if not cmap:
        return
    for node in walk(tree.root_node):
        if node.type != "invocation_expression":
            continue
        fn = node.child_by_field_name("function")
        if fn is None or fn.type != "member_access_expression":
            continue
        expr = fn.child_by_field_name("expression")
        nm = fn.child_by_field_name("name")
        if expr is None or nm is None:
            continue
        tname = receiver_type_name(expr, src, idx)
        if tname is None:
            continue
        mname = text_of(nm, src)
        if mname in UNITY_STATIC_OK:
            continue
        if tname not in idx.type_ns:                 # не тип проекта
            continue
        chain = cmap.get(tname)
        if chain is None:
            continue
        # собираем члены по проектной цепочке; если где-то упираемся во внешнюю базу — пропускаем проверку
        queue = [tname]
        seen = set()
        members = set()
        touch_external = False
        while queue:
            t = queue.pop(0)
            if t in seen:
                continue
            seen.add(t)
            info = cmap.get(t)
            if info is None:
                touch_external = True
                continue
            tnode, tsrc, tpath, tbases = info
            members |= set(declared_members(tnode, tsrc).keys())
            for b in tbases:
                if b in cmap:
                    queue.append(b)
                elif b not in ("object",):
                    touch_external = True
        if mname in members:
            continue
        if mname not in GLOBAL_MEMBER_NAMES and touch_external:
            continue                                  # похоже на Unity-член у типа с внешней базой
        hint = ""
        if mname in GLOBAL_MEMBER_NAMES:
            owners = ", ".join(sorted(GLOBAL_MEMBER_OWNERS.get(mname, set()))[:4])
            hint = f" — он объявлен в: {owners}"
        out.append((path, node.start_point[0] + 1, "G",
                    f"`{tname}.{mname}(…)` — в типе `{tname}` и его проектных родителях такого "
                    f"члена нет (CS0117/CS1061){hint}"))



STRUCTISH_UNITY = {"Vector2", "Vector3", "Vector4", "Vector2Int", "Vector3Int", "Quaternion",
                   "Color", "Color32", "Rect", "RectInt", "Bounds", "LayerMask", "Matrix4x4",
                   "Plane", "Ray"}


# ------------------------------------------------- новые проверки (M/N/O)
def own_body(node):
    """Тело типа без тел вложенных типов — только «свои» объявления."""
    body = node.child_by_field_name("body")
    if body is None:
        return None
    return body


def check_M_duplicate_members(idx, path, src, tree, out):
    """CS0102: один и тот же член объявлен в типе дважды (консты/поля/свойства/события)."""
    for node in walk(tree.root_node):
        if node.type not in TYPE_DECL:
            continue
        body = own_body(node)
        if body is None:
            continue
        tname = text_of(node.child_by_field_name("name"), src) if node.child_by_field_name("name") else "?"
        seen = {}
        for ch in body.children:
            if ch.type in TYPE_DECL:            # вложенные типы живут в своей области
                continue
            if ch.type == "field_declaration":
                for d in walk(ch):
                    if d.type != "variable_declarator":
                        continue
                    nm = d.child_by_field_name("name")
                    if nm is not None:
                        seen.setdefault(text_of(nm, src), []).append((nm.start_point[0] + 1, "поле/const"))
            elif ch.type == "property_declaration":
                nm = ch.child_by_field_name("name")
                if nm is not None:
                    seen.setdefault(text_of(nm, src), []).append((nm.start_point[0] + 1, "свойство"))
            elif ch.type == "event_field_declaration":
                for d in walk(ch):
                    if d.type == "variable_declarator":
                        nm = d.child_by_field_name("name")
                        if nm is not None:
                            seen.setdefault(text_of(nm, src), []).append((nm.start_point[0] + 1, "событие"))
        for nm, places in seen.items():
            if len(places) > 1:
                lines = ", ".join(str(l) for l, _ in places)
                out.append((path, places[0][0], "M",
                            f"`{tname}.{nm}` объявлен {len(places)} раз (строки {lines}) → CS0102. "
                            f"Переименуй (например PR_/LV_-варианты: FilingCabinet / LvFilingCabinet)."))


# типы, которые в Unity живут ТОЛЬКО в UnityEngine.UI
UGUI_TYPES = {
    "Text", "Image", "RawImage", "Button", "Toggle", "Slider", "Scrollbar", "Dropdown",
    "InputField", "ScrollRect", "Mask", "RectMask2D", "Outline", "Shadow", "Selectable",
    "Graphic", "MaskableGraphic", "CanvasScaler", "GraphicRaycaster", "LayoutElement",
    "ContentSizeFitter", "HorizontalLayoutGroup", "VerticalLayoutGroup", "GridLayoutGroup",
    "AspectRatioFitter", "LayoutRebuilder", "ToggleGroup", "TextMeshProUGUI",
}


def _strip_comments_strings(text):
    text = re.sub(r"/\*.*?\*/", " ", text, flags=re.S)
    text = re.sub(r"//[^\n]*", " ", text)
    text = re.sub(r'"([^"\\]|\\.)*"', '""', text)
    return text


def check_N_ugui_using(idx, path, src, tree, out):
    """CS0246: Text/Image/Button и т.п. без `using UnityEngine.UI;`."""
    srctext = src.decode("utf-8", "replace") if isinstance(src, (bytes, bytearray)) else src
    if "using UnityEngine.UI;" in srctext:
        return
    code = _strip_comments_strings(srctext)
    for t in sorted(UGUI_TYPES):
        if t in idx.type_ns or t in idx.dupes:      # свой тип с таким именем — не наша забота
            continue
        pats = (r"\b" + t + r"\b\s+[A-Za-z_]",  # объявление: Text foo (не GraphicsSettings!)
                r"\b" + t + r"\b\s*\[\s*\]",  # массив: Text[] x
                r"\bnew\s+" + t + r"\b",         # new Text(...)
                r":\s*" + t + r"\b",              # базовый тип: class X : Button
                r"\(\s*" + t + r"\s*\)",        # приведение (Text)
                r"<\s*" + t + r"\s*[>,]")         # List<Text>
        hit = None
        for pat in pats:
            hit = re.search(pat, code)
            if hit:
                break
        if not hit:
            continue
        line = code[:hit.start()].count("\n") + 1
        out.append((path, line, "N",
                    f"`{t}` — тип из UnityEngine.UI, а в файле нет `using UnityEngine.UI;` → CS0246. "
                    f"Добавь using (или пиши полное имя `UnityEngine.UI.{t}`)."))
        return


def check_O_nested_visibility(idx, path, src, tree, out):
    """CS0246: вложенный класс использован вне своего внешнего класса."""
    srctext = src.decode("utf-8", "replace") if isinstance(src, (bytes, bytearray)) else src
    tree_types = {}
    for node in walk(tree.root_node):
        if node.type in TYPE_DECL:
            nm = node.child_by_field_name("name")
            if nm is not None:
                tree_types[node.id] = (text_of(nm, src), node)

    # для каждого вложенного типа найти ближайшего внешнего «родителя-тип»
    for node in walk(tree.root_node):
        if node.type not in TYPE_DECL or node.id not in tree_types:
            continue
        nm, _ = tree_types[node.id]
        parent = node.parent
        outer = None
        while parent is not None:
            if parent.type in TYPE_DECL and parent.id in tree_types:
                outer = parent
                break
            parent = parent.parent
        if outer is None:
            continue
        if sum(1 for v in tree_types.values() if v[0] == nm) > 1:
            continue           # в файле несколько одноимённых типов — области видимости разные, не судим
        outer_name = tree_types[outer.id][0]
        # ВАЖНО: смещения узлов — БАЙТОВЫЕ, а srctext — строка: на кириллице они расходятся.
        outside = _strip_comments_strings(
            (src[:outer.start_byte] + src[outer.end_byte:]).decode("utf-8", "replace"))
        # Ошибка только при НЕквалифицированном использовании: `Slot slot`.
        # `ModelLibrary.Names.X` / `SnapshotInterpolator.Snap` — законно (тип через внешний класс).
        bare = r"(?<![\w.])" + nm + r"\b"
        pats = (bare + r"\s+[A-Za-z_]",          # объявление/параметр/поле
                bare + r"\s*\[\s*\]",          # массив
                r"\bnew\s+" + bare,              # new Snap(...)
                r":\s*" + bare,                   # базовый тип
                r"\(\s*" + bare + r"\s*\)",    # приведение
                r"<\s*" + bare + r"\s*[>,]")     # дженерик-аргумент
        m = None
        for pat in pats:
            m = re.search(pat, outside)
            if m:
                break
        if m:
            line = src[:outer.start_byte].count(b"\n") + 1
            out.append((path, line, "O",
                        f"вложенный тип `{nm}` (внутри `{outer_name}`) используется вне своего внешнего класса → CS0246. "
                        f"Вынеси `{nm}` на уровень namespace."))


def _bare_type_pats(nm):
    """Шаблоны «тип в позиции типа»: `Slot slot`, `new Slot()`, `(Slot)`, `List<Slot>`, `Slot[]`."""
    bare = r"(?<![\w.])" + nm + r"\b"
    return (bare + r"\s+[A-Za-z_]", bare + r"\s*\[\s*\]",
            r"\bnew\s+" + bare, r":\s*" + bare,
            r"\(\s*" + bare + r"\s*\)", r"<\s*" + bare + r"\s*[>,]")


def check_O2_nested_cross_file(idx, files, srcs, trees, out):
    """Вложенный тип, использованный без квалификации в ДРУГОМ файле → CS0246
    (ровно так было со `Slot` внутри InventoryUI и HudRuntime)."""
    nested = []          # (простое имя, внешний тип, файл-владелец)
    toplevel = set()     # имена обычных (не вложенных) типов проекта
    counts = defaultdict(int)
    for path in files:
        src, tree = srcs[path], trees[path]
        types = {}
        for node in walk(tree.root_node):
            if node.type in TYPE_DECL:
                nm = node.child_by_field_name("name")
                if nm is not None:
                    types[node.id] = (text_of(nm, src), node)
        for node_id, (nm, node) in types.items():
            par = node.parent
            is_nested = False
            while par is not None:
                if par.type in TYPE_DECL and par.id in types:
                    nested.append((nm, types[par.id][0], path))
                    counts[nm] += 1
                    is_nested = True
                    break
                par = par.parent
            if not is_nested:
                toplevel.add(nm)

    for nm, outer, owner in nested:
        if counts[nm] > 1 or nm in toplevel or nm in idx.dupes:
            continue                     # имя неоднозначно (или это обычный тип проекта)
        for path in files:
            if path == owner:
                continue
            text = _strip_comments_strings(srcs[path].decode("utf-8", "replace"))
            if re.search(r":\s*[\w\.]*\b" + outer + r"\b", text):
                continue                 # наследник внешнего класса: вложенный тип виден — законно
            for pat in _bare_type_pats(nm):
                m = re.search(pat, text)
                if m:
                    line = text[:m.start()].count("\n") + 1
                    out.append((path, line, "O",
                                f"вложенный тип `{nm}` объявлен внутри `{outer}` ({os.path.basename(owner)}), "
                                f"а здесь используется как самостоятельный → CS0246. "
                                f"Вынеси `{nm}` на уровень namespace."))
                    break


# ------------------------------------------- P/Q/R: сегодняшние ошибки Unity (CS1061/CS0103)
def _enclosing_class(node):
    par = node.parent
    while par is not None:
        if par.type in TYPE_DECL:
            return par
        par = par.parent
    return None


def _members_of_type(tname, cmap):
    if tname not in cmap:
        return set(), set()
    members, external = _chain_members_full(tname, cmap)
    return members, external


def check_P_member_shadows_namespace(idx, path, src, tree, out, cmap):
    """`Player.TransportSpawner.Populate(...)`, где `Player` — свойство класса, а `TransportSpawner` —
    тип из namespace `Subsistence.Player` → Unity: CS1061 «нет члена TransportSpawner».
    Подсказываем писать полный путь к namespace."""
    for node in walk(tree.root_node):
        if node.type != "member_access_expression":
            continue
        expr = node.child_by_field_name("expression")
        nm = node.child_by_field_name("name")
        if expr is None or nm is None or expr.type != "identifier" or nm.type != "identifier":
            continue
        qual, member = text_of(expr, src), text_of(nm, src)
        cls = _enclosing_class(node)
        if cls is None:
            continue
        cm = class_members(cls, src)
        own = set(cm["fields"]) | set(cm["props"]) | set(cm["methods"])
        if qual not in own:
            continue                       # `Player` не член этого класса — не наш случай
        # тип свойства/поля
        ftype = cm["fields"].get(qual) or cm["props"].get(qual, "")
        ftype = base_type_name(re.sub(r"<.*", "", ftype).strip())
        if ftype in idx.type_ns or ftype in idx.dupes:
            members, _ext = _members_of_type(ftype, cmap)
            if member in members:
                continue                   # это настоящий член типа — всё хорошо
            if member in idx.type_ns:
                out.append((path, node.start_point[0] + 1, "P",
                            f"`{qual}.{member}`: `{qual}` — это член класса (тип `{ftype}`), а `{member}` — "
                            f"тип из namespace `{idx.type_ns[member]}`. Похоже, нужен полный путь "
                            f"`{idx.type_ns[member]}.{member}` (иначе CS1061)."))
            continue
        # тип свойства неизвестен, но имя совпадает с namespace проекта — тоже ловушка
        if member in idx.decl_file or f"{member}" in {ns.split(".")[-1] for ns in idx.types}:
            if member in idx.type_ns:
                out.append((path, node.start_point[0] + 1, "P",
                            f"`{qual}.{member}`: `{qual}` — член класса, `{member}` — тип из "
                            f"namespace `{idx.type_ns[member]}`. Если нужен тип — пиши полный путь (CS1061)."))


def check_Q_bare_call_other_class(idx, path, src, tree, out, cmap, files_locals):
    """`AttachIcon(slot)` без префикса, когда метод объявлен в ДРУГОМ классе → CS0103."""
    if "using static" in src.decode("utf-8", "replace"):
        return
    for node in walk(tree.root_node):
        if node.type != "invocation_expression":
            continue
        fn = node.child_by_field_name("function")
        if fn is None or fn.type != "identifier":
            continue
        name = text_of(fn, src)
        cls = _enclosing_class(node)
        if cls is None:
            continue
        cname = text_of(cls.child_by_field_name("name"), src) if cls.child_by_field_name("name") else ""
        if cname not in cmap:
            continue
        members, _ext = _members_of_type(cname, cmap)
        if name in members or name in ("ToString", "GetType", "Equals", "GetHashCode"):
            continue
        # локальная функция или делегат с таким именем внутри охватывающих методов — не ошибка
        up = node.parent
        local_ok = False
        while up is not None and up.type not in TYPE_DECL:
            if up.type in ("method_declaration", "constructor_declaration", "local_function_statement",
                           "lambda_expression", "accessor_declaration"):
                for d in walk(up):
                    if d.type == "local_function_statement":
                        nmm = d.child_by_field_name("name")
                        if nmm is not None and text_of(nmm, src) == name:
                            local_ok = True
                    elif d.type == "variable_declarator":
                        nmm = d.child_by_field_name("name")
                        if nmm is not None and nmm.type == "identifier" and text_of(nmm, src) == name:
                            local_ok = True
            up = up.parent
        if local_ok:
            continue
        owners = GLOBAL_MEMBER_OWNERS.get(name)
        if not owners:
            continue
        if cname in owners:
            continue
        out.append((path, node.start_point[0] + 1, "Q",
                    f"`{name}(...)` вызывается без префикса, но объявлен в: {', '.join(sorted(owners))} — "
                    f"в классе `{cname}` такого метода нет → CS0103. Вынеси метод в общий тип или "
                    f"вызывай через владельца."))


def _scope_locals(node, src):
    """Все имена, объявленные внутри узла: локалки, параметры, переменные foreach/catch/lambda/out."""
    names = set()
    for d in walk(node):
        if d.type == "parameter":
            nm = d.child_by_field_name("name")
            if nm is not None:
                names.add(text_of(nm, src))
        elif d.type == "variable_declarator":
            nm = d.child_by_field_name("name") or (d.children[0] if d.children else None)
            if nm is not None:
                if nm.type == "identifier":
                    names.add(text_of(nm, src))
                else:                                     # `var (fx, ends) = ...`
                    for t in walk(nm):
                        if t.type == "identifier":
                            names.add(text_of(t, src))
        elif d.type in ("for_each_statement", "foreach_statement"):
            for ch in d.children:                     # `foreach (var x in ...)` — до ключевого слова in
                if ch.type == "in":
                    break
                if ch.type == "identifier":
                    names.add(text_of(ch, src))
                elif ch.type in ("tuple_pattern", "parenthesized_variable_designation"):
                    for t in walk(ch):
                        if t.type == "identifier":
                            names.add(text_of(t, src))
        elif d.type == "parameter_list":
            # параметры с составным типом (кортеж/массив) грамматика кладёт иначе:
            # `params (string item, float w)[] items` — имя лежит прямо в списке
            for ch in d.children:
                if ch.type == "identifier":
                    names.add(text_of(ch, src))
                elif ch.type == "parameter":
                    nm = ch.child_by_field_name("name")
                    if nm is not None:
                        names.add(text_of(nm, src))
                    else:
                        ids = [t for t in walk(ch) if t.type == "identifier"]
                        if ids:
                            names.add(text_of(ids[-1], src))
        elif d.type == "catch_declaration":
            nm = d.child_by_field_name("name")
            if nm is not None:
                names.add(text_of(nm, src))
        elif d.type == "lambda_expression":
            got = False
            for pnode in walk(d):
                if pnode.type == "parameter":
                    nm = pnode.child_by_field_name("name")
                    if nm is not None:
                        names.add(text_of(nm, src)); got = True
            if not got:                                   # `conn => ...` — имя в узле implicit_parameter
                for ch in d.children:
                    if ch.type == "=>":
                        break
                    if ch.type in ("identifier", "implicit_parameter"):
                        names.add(text_of(ch, src))
        elif d.type == "declaration_expression":
            nm = d.child_by_field_name("name")
            if nm is not None:
                names.add(text_of(nm, src))
        elif d.type == "local_function_statement":      # локальная функция видна в своём методе
            nm = d.child_by_field_name("name")
            if nm is not None:
                names.add(text_of(nm, src))
        elif d.type in ("declaration_pattern", "recursive_pattern"):
            nm = d.child_by_field_name("name")                # `d is MonoBehaviour mb`
            if nm is not None:
                names.add(text_of(nm, src))
        elif d.type == "parenthesized_variable_designation":  # `var (fx, ends) = ...`
            for t in walk(d):
                if t.type == "identifier":
                    names.add(text_of(t, src))
    return names


def check_R_wrong_scope_name(idx, path, src, tree, out, cmap):
    """Имя, объявленное в проекте (локалка другого метода / поле другого типа), но не в этой
    области видимости → CS0103 (как `netId` в Monsters.cs и `rng` в LevelGenerator.cs)."""
    file_locals = _scope_locals(tree.root_node, src)   # имена-локалки/параметры где-либо в файле

    for node in walk(tree.root_node):
        if node.type != "identifier":
            continue
        parent = node.parent
        if parent is None:
            continue
        # пропускаем: имена объявлений, члены доступа, типы, вызовы (это к Q), using, атрибуты
        if parent.type == "variable_declarator":
            if parent.children and parent.children[0].id == node.id:
                continue                       # это имя объявления; значение проверяем
        elif parent.type in ("parameter", "method_declaration", "class_declaration",
                           "struct_declaration", "property_declaration", "enum_member_declaration",
                           "using_directive", "namespace_declaration", "attribute", "type_argument_list",
                           "qualified_name", "object_creation_expression",
                           "invocation_expression", "enum_declaration", "interface_declaration",
                           "constructor_declaration", "field_declaration", "type_parameter",
                           "generic_name", "cast_expression", "base_list", "tuple_element",
                           "tuple_pattern", "parenthesized_variable_designation", "declaration_pattern",
                           "local_declaration_statement"):
            continue
        if parent.type == "assignment_expression":
            left = parent.child_by_field_name("left")
            if left is not None and left.id == node.id:
                continue
        if parent.type in ("member_access_expression", "member_binding_expression",
                           "conditional_access_expression"):
            nmfield = parent.child_by_field_name("name")   # в `x.y` имя — y; получатель x нас интересует
            if nmfield is None or nmfield.id == node.id:
                continue
        if parent.type == "argument":                  # именованный аргумент: Apply(x, instant: true)
            lbl = parent.child_by_field_name("name")
            if lbl is not None and lbl.id == node.id:
                continue
        # имя внутри ТИПА (не переменная): смотрим только прямой родитель и один уровень вложенности
        if parent.type in ("tuple_type", "array_type", "nullable_type", "generic_name", "qualified_name",
                           "predefined_type", "type_argument_list", "cast_expression", "base_list",
                           "type_parameter_list", "using_directive", "attribute", "attribute_list",
                           "object_creation_expression", "tuple_element"):
            continue
        gp = parent.parent
        if gp is not None and gp.type in ("tuple_type", "array_type", "nullable_type", "type_argument_list",
                                          "generic_name", "attribute"):
            continue
        name = text_of(node, src)
        if len(name) < 2 or not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", name):
            continue
        cls = _enclosing_class(node)
        if cls is None:
            continue
        cname = text_of(cls.child_by_field_name("name"), src) if cls.child_by_field_name("name") else ""
        members = set()
        if cname in cmap:
            members, _ext = _members_of_type(cname, cmap)
        # область видимости = все охватывающие методы/лямбды (захваченные переменные видны)
        scope_names = set()
        up = node.parent
        found_scope = False
        while up is not None and up.type not in TYPE_DECL:
            if up.type in ("method_declaration", "constructor_declaration", "accessor_declaration",
                           "local_function_statement", "lambda_expression", "anonymous_method_expression"):
                scope_names |= _scope_locals(up, src)
                found_scope = True
            up = up.parent
        if not found_scope:
            scope_names = _scope_locals(cls, src)
        if name in scope_names or name in members:
            continue
        if name in idx.type_ns or name in idx.dupes:
            continue
        # квалификатор namespace: `Net.NetworkBridge.Host` — имя `Net` это namespace, не переменная
        if parent.type in ("member_access_expression", "qualified_name"):
            nxt = parent.child_by_field_name("name")
            ns_segs = {ns.split(".")[-1] for ns in idx.types}
            if name in ns_segs and nxt is not None:
                nxt_name = text_of(nxt, src)
                if nxt_name in idx.type_ns or nxt_name in ns_segs or nxt_name in idx.dupes:
                    continue
        if name in file_locals or name in GLOBAL_MEMBER_NAMES:
            where = []
            if name in GLOBAL_MEMBER_OWNERS:
                where.append("член " + ", ".join(sorted(GLOBAL_MEMBER_OWNERS[name])[:3]))
            if name in file_locals:
                where.append("локалка/параметр другого метода этого файла")
            out.append((path, node.start_point[0] + 1, "R",
                        f"`{name}` не объявлено в этой области видимости — в проекте это " +
                        "; ".join(where) + " → CS0103."))


def check_S_dictionary_value(idx, path, src, tree, out, cmap):
    """`foreach (var d in ItemDatabase.All) … d.id …`, где All — словарь: у KeyValuePair нет `id` →
    CS1061, нужен `d.Value.id`. Ловим по типу перебираемого выражения."""
    for node in walk(tree.root_node):
        if node.type not in ("for_each_statement", "foreach_statement"):
            continue
        var_name = None
        iterable = None
        seen_in = False
        for ch in node.children:
            if ch.type == "in":
                seen_in = True
                continue
            if not seen_in:
                if ch.type == "identifier" and var_name is None:
                    var_name = text_of(ch, src)          # `foreach (var d in ...)` → d
            elif iterable is None and ch.type in ("member_access_expression", "identifier",
                                                  "invocation_expression", "element_access_expression"):
                iterable = ch                            # перебираемое выражение — ПОСЛЕ `in`
        if var_name is None:
            continue
        # тип перебираемого: поле/свойство класса или тип проекта
        vtype = None
        if iterable is not None and iterable.type == "member_access_expression":
            nm = iterable.child_by_field_name("name")
            expr = iterable.child_by_field_name("expression")
            if nm is not None and expr is not None and expr.type == "identifier":
                recv = text_of(expr, src)
                cls = _enclosing_class(node)
                cname = text_of(cls.child_by_field_name("name"), src) if cls is not None and cls.child_by_field_name("name") else ""
                if cname in cmap:
                    cnode, csrc, _p, _b = cmap[cname]
                    cm = class_members(cnode, csrc)
                    vtype = cm["fields"].get(recv) or cm["props"].get(recv)
                if not vtype and (recv in cmap or recv in idx.type_ns):
                    # статический доступ: ItemDatabase.All — берём тип свойства у самого типа
                    owner = recv if recv in cmap else idx.type_ns.get(recv)
                    if owner in cmap:
                        onode, osrc, _p2, _b2 = cmap[owner]
                        om = class_members(onode, osrc)
                        vtype = om["fields"].get(text_of(nm, src)) or om["props"].get(text_of(nm, src))
        if not vtype:
            continue
        m = re.search(r"(?:I?ReadOnly)?Dictionary<\s*[^,]+,\s*([^>]+)>", vtype)
        if not m:
            continue
        value_type = base_type_name(m.group(1).strip())
        if value_type not in cmap:
            continue
        members, _ext = _members_of_type(value_type, cmap)
        for use in walk(node):
            if use.type == "member_access_expression":
                nm = use.child_by_field_name("name")
                ex = use.child_by_field_name("expression")
                if nm is None or ex is None or ex.type != "identifier" or text_of(ex, src) != var_name:
                    continue
                member = text_of(nm, src)
                if member in ("Key", "Value", "ToString", "GetType", "Equals", "GetHashCode", "Deconstruct"):
                    continue
                if member in members:
                    out.append((path, use.start_point[0] + 1, "S",
                                f"`{var_name}.{member}`: `{var_name}` — это KeyValuePair из словаря, "
                                f"а `{member}` — член типа `{value_type}` → CS1061. Нужен `.Value.{member}`."))


def class_members(node, src):
    """Методы/свойства/поля/события типа (рекурсивно, вместе с вложенными)."""
    out = {"methods": {}, "props": {}, "fields": {}, "events": set(), "readonly": set()}
    for ch in walk(node):
        if ch.type == "method_declaration":
            nm = ch.child_by_field_name("name")
            if nm is not None:
                out["methods"][text_of(nm, src)] = {text_of(m, src) for m in children_of(ch, "modifier")}
        elif ch.type == "property_declaration":
            nm = ch.child_by_field_name("name")
            tp = ch.child_by_field_name("type")
            if nm is not None:
                out["props"][text_of(nm, src)] = text_of(tp, src) if tp is not None else "?"
        elif ch.type in ("field_declaration", "event_field_declaration"):
            tp, mods = "?", set()
            for c in ch.children:
                if c.type == "variable_declaration":
                    vt = c.child_by_field_name("type")
                    tp = text_of(vt, src) if vt is not None else "?"
                elif c.type == "modifier":
                    mods.add(text_of(c, src))
            for d in walk(ch):
                if d.type == "variable_declarator":
                    nmn = d.child_by_field_name("name") if d.child_by_field_name("name") else (d.children[0] if d.children else None)
                    if nmn is None:
                        continue
                    n = text_of(nmn, src)
                    if ch.type == "event_field_declaration" or "event" in mods:
                        out["events"].add(n)
                    else:
                        out["fields"][n] = tp
                        if "readonly" in mods:
                            out["readonly"].add(n)
    return out


def check_T_unused_local(idx, path, src, tree, out, cmap):
    """CS0219: локальная переменная с инициализатором, которую больше нигде не читают.
    Именно этот warning Unity дал на `int at = 0;` в ProcAudio — теперь ловится заранее."""
    for node in walk(tree.root_node):
        if node.type != "local_declaration_statement":
            continue
        par = node.parent
        if par is not None and par.type in ("using_statement", "for_statement",
                                            "for_each_statement", "fixed_statement"):
            continue                                  # `using var x = ...` живёт ради Dispose
        decls = [c for c in node.children if c.type == "variable_declaration"]
        if len(decls) != 1:
            continue                                  # `int a = 0, b = 1;` — не трогаем ради простоты
        vars_ = [c for c in decls[0].children if c.type == "variable_declarator"]
        if len(vars_) != 1:
            continue
        v = vars_[0]
        name_node = v.child_by_field_name("name")
        if name_node is None and v.children and v.children[0].type == "identifier":
            name_node = v.children[0]                 # у declarator поле name заполняется не всегда
        if name_node is None:
            continue
        # у tree-sitter «=» — отдельный токен, а не узел equals_value_clause
        if not any(c.type in ("=", "equals_value_clause") for c in v.children):
            continue                                  # без инициализатора это CS0168, а не CS0219
        name = text_of(name_node, src)
        if not name or name == "_":
            continue
        owner = node.parent
        while owner is not None and owner.type not in (
                "method_declaration", "constructor_declaration", "local_function_statement",
                "lambda_expression", "anonymous_method_expression", "accessor_declaration",
                "operator_declaration", "block"):
            owner = owner.parent
        if owner is None:
            continue
        uses = 0
        for n2 in walk(owner):
            if n2.type != "identifier" or text_of(n2, src) != name:
                continue
            if (n2.start_point == name_node.start_point and n2.end_point == name_node.end_point):
                continue                              # само объявление
            uses += 1
        if uses == 0:
            out.append((path, node.start_point[0] + 1, "T",
                        f"`{name}` проинициализирована, но нигде не читается — warning CS0219 "
                        f"(мертвая переменная, в проде такое лучше убрать)."))


def file_variable_names(path, src, tree):
    """Имена параметров/локальных/полей файла — чтобы не путать их с типами."""
    names = set()
    for node in walk(tree.root_node):
        if node.type == "parameter":
            nm = node.child_by_field_name("name")
            if nm is not None:
                names.add(text_of(nm, src))
        elif node.type in ("variable_declarator", "declaration_expression"):
            nm = node.child_by_field_name("name") or node.child_by_field_name("identifier")
            if nm is None and node.children:
                nm = node.children[0]
            if nm is not None and nm.type == "identifier":
                names.add(text_of(nm, src))
        elif node.type == "assignment_expression":
            l = node.child_by_field_name("left")
            if l is not None and l.type == "declaration_expression":
                nm = l.child_by_field_name("name")
                if nm is not None:
                    names.add(text_of(nm, src))
    return names


INFER_METHODS = {"GetComponent", "GetComponentInParent", "GetComponentInChildren", "GetComponents",
                 "AddComponent", "GetOrAddComponent", "FindAnyObjectByType", "FindFirstObjectByType"}


def infer_var_type(value, src):
    """`var x = <выражение>` → текст типа (или None). Нужно, чтобы ловить `surv.health`-ошибки."""
    if value is None:
        return None
    if value.type == "object_creation_expression":
        t = value.child_by_field_name("type")
        return text_of(t, src) if t is not None else None
    if value.type == "cast_expression":
        t = value.child_by_field_name("type")
        return text_of(t, src) if t is not None else None
    if value.type == "as_expression":
        t = value.child_by_field_name("right") or (value.children[-1] if value.children else None)
        return text_of(t, src) if t is not None else None
    if value.type == "invocation_expression":
        fn = value.child_by_field_name("function")
        if fn is not None:
            nm = fn if fn.type == "generic_name" else (fn.child_by_field_name("name") if fn.type == "member_access_expression" else None)
            if nm is not None and nm.type == "generic_name":
                txt = text_of(nm, src)
                parts = re.sub(r"<.*", "", txt).strip()
                if parts in INFER_METHODS:
                    inner = re.search(r"<([^<>]+)>", txt)
                    return inner.group(1).strip() if inner else None
    return None


def var_types(path, src, tree, unique_only=True):
    """{имя: текст типа} по явным объявлениям. unique_only — брать только имена,
    объявленные в файле РОВНО одним типом (иначе `c`, `s`, `r` в разных методах путаются)."""
    res = {}
    all_t = {}
    decl_cnt = {}
    for node in walk(tree.root_node):
        if node.type == "variable_declarator":
            nm = node.child_by_field_name("name") or (node.children[0] if node.children else None)
            if nm is not None:
                decl_cnt[text_of(nm, src)] = decl_cnt.get(text_of(nm, src), 0) + 1
        elif node.type == "parameter":
            nm = node.child_by_field_name("name")
            if nm is not None:
                decl_cnt[text_of(nm, src)] = decl_cnt.get(text_of(nm, src), 0) + 1
    for node in walk(tree.root_node):
        if node.type in ("variable_declaration", "field_declaration"):
            tp = None
            for c in node.children:
                if c.type == "variable_declaration":
                    v = c.child_by_field_name("type")
                    tp = text_of(v, src) if v is not None else None
                elif c.type == "implicit_type":
                    tp = "var"                      # `var x = ...` — тип выведем из инициализатора
                elif c.type in ("predefined_type", "identifier", "qualified_name", "generic_name"):
                    tp = text_of(c, src)
            if tp is None:
                continue
            for d in walk(node):
                if d.type == "variable_declarator":
                    nm = d.child_by_field_name("name") or (d.children[0] if d.children else None)
                    if nm is None:
                        continue
                    eff = tp
                    if tp.strip() == "var":
                        val = d.child_by_field_name("value")
                        if val is None:                       # дерево: identifier '=' expression
                            eq = [c for c in d.children if c.type not in ("identifier", "=")]
                            val = eq[-1] if eq else None
                        eff = infer_var_type(val, src) or tp
                    res[text_of(nm, src)] = eff
                    all_t.setdefault(text_of(nm, src), set()).add(eff)
        elif node.type == "parameter":
            nm = node.child_by_field_name("name")
            tp = node.child_by_field_name("type")
            if nm is not None and tp is not None:
                res[text_of(nm, src)] = text_of(tp, src)
                all_t.setdefault(text_of(nm, src), set()).add(text_of(tp, src))
    if unique_only:
        # берём только имена, объявленные в файле РОВНО ОДИН раз и с одним явным типом
        return {k: v for k, v in res.items()
                if decl_cnt.get(k, 0) == 1 and len(all_t.get(k, ())) == 1}
    return res


def enclosing_class_node(node):
    par = node.parent
    while par is not None:
        if par.type in TYPE_DECL:
            return par
        par = par.parent
    return None


def base_type_name(tp):
    return re.sub(r"<.*", "", tp).split(".")[-1].strip()


def check_H_type_receiver(idx, path, src, tree, out):
    """`RuntimeBootstrap.Instance`, `UIStyle.Panel(…)` — тип без using → CS0103."""
    scopes = in_scope_namespaces(tree, src, path, idx)
    own_ns = idx.ns_of_file.get(path, "")
    vars_here = file_variable_names(path, src, tree)
    seen = set()
    for node in walk(tree.root_node):
        if node.type not in ("member_access_expression", "member_binding_expression"):
            continue
        expr = node.child_by_field_name("expression")
        if expr is None or expr.type != "identifier":
            continue
        name = text_of(expr, src)
        if name in vars_here or name not in idx.type_ns:
            continue
        if idx.type_ns[name] in scopes or idx.type_ns[name] == own_ns:
            continue
        key = (name, node.start_point[0] + 1)
        if key in seen:
            continue
        seen.add(key)
        out.append((path, node.start_point[0] + 1, "H",
                    f"`{name}` — это тип из namespace `{idx.type_ns[name]}`, отсюда он не виден: "
                    f"нужен `using {idx.type_ns[name]};` (CS0103)"))



def check_H2_namespace_member(idx, path, src, tree, out, cmap=None):
    """`World.TransportSpawnerPopulate(…)` — у namespace такого типа нет (CS0234)."""
    ns_last = {}
    for ns in idx.types:
        ns_last.setdefault(ns.split(".")[-1], set()).add(ns)
    # имена, которые в этом файле заняты переменными или членами типов — их не считаем namespace
    shadowed = file_variable_names(path, src, tree)
    # члены класса и его баз (в т.ч. из других файлов): `Net.Value` у NetEntity — это свойство, не namespace
    if cmap:
        for cls in walk(tree.root_node):
            if cls.type not in TYPE_DECL:
                continue
            cname = text_of(cls.child_by_field_name("name"), src) if cls.child_by_field_name("name") else ""
            if cname in cmap:
                members, _ext = _chain_members_full(cname, cmap)
                shadowed |= set(members)
    for node in walk(tree.root_node):
        if node.type in ("method_declaration", "property_declaration", "field_declaration"):
            nmn = node.child_by_field_name("name")
            if nmn is not None:
                shadowed.add(text_of(nmn, src))
            for ch in walk(node):
                if ch.type == "variable_declarator":
                    n2 = ch.child_by_field_name("name")
                    if n2 is not None:
                        shadowed.add(text_of(n2, src))
    seen = set()
    for node in walk(tree.root_node):
        if node.type not in ("member_access_expression", "member_binding_expression"):
            continue
        expr = node.child_by_field_name("expression")
        nm = node.child_by_field_name("name")
        if expr is None or nm is None or expr.type != "identifier":
            continue
        head = text_of(expr, src)
        if head not in ns_last or head in shadowed:
            continue
        member = text_of(nm, src)
        ok = False
        for full in ns_last[head]:
            if member in idx.types.get(full, set()) or any(n.startswith(full + "." + member) for n in idx.types):
                ok = True
                break
        if ok:
            continue
        key = (head, member, node.start_point[0] + 1)
        if key in seen:
            continue
        seen.add(key)
        where = f"; `{member}` объявлен в `{idx.type_ns[member]}`" if member in idx.type_ns else ""
        out.append((path, node.start_point[0] + 1, "H",
                    f"`{head}.{member}` — в namespace `{head}` такого типа нет{where} "
                    f"(CS0234/CS0116: проверь имя или namespace)"))


def cond_access_names(node, src):
    """`a?.b` → (a, b). Для обычного доступа вернёт None."""
    cond = node.child_by_field_name("condition")
    if cond is None or cond.type != "identifier":
        return None
    for ch in node.children:
        if ch.type == "member_binding_expression":
            nm = ch.child_by_field_name("name")
            if nm is not None:
                return text_of(cond, src), text_of(nm, src)
    return None


def _chain_members_full(tname, cmap):
    queue, seen, members, external = [tname], set(), set(), False
    while queue:
        t = queue.pop(0)
        if t in seen:
            continue
        seen.add(t)
        info = cmap.get(t)
        if info is None:
            external = True
            continue
        tnode, tsrc, tpath, tbases = info
        m = class_members(tnode, tsrc)
        members |= set(m["methods"]) | set(m["props"]) | set(m["fields"]) | m["events"]
        for b in tbases:
            if b in cmap:
                queue.append(b)
            elif b != "object":
                external = True
    return members, external


def check_I_member_of_var(idx, path, src, tree, out, cmap):
    """`somePlayer.ResetAfterDeath()` — такого члена у проектного типа нет → CS1061."""
    vtypes = var_types(path, src, tree)
    for node in walk(tree.root_node):
        expr = nm = None
        if node.type in ("member_access_expression", "member_binding_expression"):
            expr = node.child_by_field_name("expression")
            nm = node.child_by_field_name("name")
        elif node.type == "conditional_access_expression":
            got = cond_access_names(node, src)
            if got is None:
                continue
            vname, member = got
            expr = node.child_by_field_name("condition")
            nm = None
            vname = vname  # уже разобрано
            # дальше используется пара (vname, member): подставляем напрямую
            tname = base_type_name(vtypes.get(vname, ""))
            if tname not in idx.type_ns and tname not in idx.dupes:
                continue
            chain = cmap.get(tname)
            if chain is None:
                continue
            members, external = _chain_members_full(tname, cmap)
            member = re.sub(r"<.*", "", member).strip()
            if member in members or member in ("ToString", "GetType", "Equals", "GetHashCode"):
                continue
            if external and member in UNITY_INSTANCE_OK:
                continue
            out.append((path, node.start_point[0] + 1, "I",
                        f"`{vname}?.{member}` — у типа `{tname}` такого члена нет (CS1061)"))
            continue
        else:
            continue
        if expr is None or nm is None or expr.type != "identifier":
            continue
        vname = text_of(expr, src)
        if vname not in vtypes:
            continue
        tname = base_type_name(vtypes[vname])
        if tname not in idx.type_ns and tname not in idx.dupes:
            continue
        chain = cmap.get(tname)
        if chain is None:
            continue
        members, external = _chain_members_full(tname, cmap)
        member = re.sub(r"<.*", "", text_of(nm, src)).strip()   # GetComponent<T> → GetComponent
        if member in members or member in ("ToString", "GetType", "Equals", "GetHashCode"):
            continue
        if external and member in UNITY_INSTANCE_OK:
            continue                      # законный член Unity у класса с внешней базой
        if external:
            hint = ""
            if member in GLOBAL_MEMBER_NAMES:
                owners = ", ".join(sorted(GLOBAL_MEMBER_OWNERS.get(member, set()))[:4])
                hint = f" — он объявлен в: {owners}"
            out.append((path, node.start_point[0] + 1, "I",
                        f"`{vname}.{member}` — у типа `{tname}` такого члена нет (CS1061){hint}"))
            continue
        out.append((path, node.start_point[0] + 1, "I",
                    f"`{vname}.{member}` — у типа `{tname}` такого члена нет (CS1061)"))


def check_J_struct_property(idx, path, src, tree, out, cmap):
    """Правка поля структуры через свойство/геттер → CS1612."""
    structs = {t for (t, _ns), (_p, n, _s) in idx.decl_file.items() if n.type == "struct_declaration"}
    def is_struct_type(tp):
        b = base_type_name(tp)
        return b in structs or b in STRUCTISH_UNITY
    vtypes = var_types(path, src, tree)
    for node in walk(tree.root_node):
        if node.type not in ("assignment_expression", "postfix_unary_expression", "prefix_unary_expression"):
            continue
        target = node.child_by_field_name("left")
        if target is None and node.type != "assignment_expression":
            target = node.children[0] if node.children else None
        if target is None or target.type != "member_access_expression":
            continue
        parts, cur = [], target
        while cur is not None and cur.type in ("member_access_expression", "member_binding_expression"):
            nm = cur.child_by_field_name("name")
            if nm is not None:
                parts.append(text_of(nm, src))
            cur = cur.child_by_field_name("expression")
        if cur is None or cur.type != "identifier":
            continue
        parts.append(text_of(cur, src))
        parts.reverse()
        if len(parts) < 2:
            continue
        bad = False
        cls = enclosing_class_node(node)
        if cls is not None:
            cname = text_of(cls.child_by_field_name("name"), src)
            info = cmap.get(cname)
            if info is not None:
                tp = class_members(info[0], info[1])["props"].get(parts[0])
                if tp is not None and is_struct_type(tp):
                    bad = True
        if not bad:
            tname = base_type_name(vtypes.get(parts[0], ""))
            info = cmap.get(tname)
            if info is not None:
                tp = class_members(info[0], info[1])["props"].get(parts[1])
                if tp is not None and is_struct_type(tp):
                    bad = True
        if bad:
            out.append((path, node.start_point[0] + 1, "J",
                        f"правка поля структуры через свойство `{'.'.join(parts[:2])}` — CS1612. "
                        f"Сделай контейнер ПОЛЕМ (public SurvivalState State;), а не свойством"))


def check_K_readonly(idx, path, src, tree, out, cmap):
    """Присваивание readonly-полю вне конструктора → CS0191."""
    for node in walk(tree.root_node):
        if node.type != "assignment_expression":
            continue
        left = node.child_by_field_name("left")
        if left is None:
            continue
        name = None
        if left.type == "identifier":
            name = text_of(left, src)
        elif left.type == "member_access_expression":
            nm = left.child_by_field_name("name")
            ex = left.child_by_field_name("expression")
            if nm is not None and ex is not None and ex.type == "this_expression":
                name = text_of(nm, src)
        if not name:
            continue
        cls = enclosing_class_node(node)
        if cls is None:
            continue
        cname = text_of(cls.child_by_field_name("name"), src)
        info = cmap.get(cname)
        if info is None:
            continue
        members = class_members(info[0], info[1])
        if name not in members["readonly"]:
            continue
        # внутри конструктора класса — можно
        m = node.parent
        in_ctor = False
        while m is not None and m is not cls:
            if m.type == "constructor_declaration":
                in_ctor = True
                break
            m = m.parent
        if in_ctor:
            continue
        out.append((path, node.start_point[0] + 1, "K",
                    f"присваивание readonly-полю `{name}` вне конструктора → CS0191 "
                    f"(убери readonly или присваивай в конструкторе)"))


def project_chain(name, cmap):
    """Все проектные предки класса (включая его самого)."""
    queue, seen = [name], set()
    while queue:
        t = queue.pop(0)
        if t in seen:
            continue
        seen.add(t)
        info = cmap.get(t)
        if info is None:
            continue
        for b in info[3]:
            if b in cmap:
                queue.append(b)
    return seen


def check_L_event_outside(idx, path, src, tree, out, events_by_type, interfaces, cmap):
    """Вызов события из чужого типа → CS0070."""
    for node in walk(tree.root_node):
        if node.type != "invocation_expression":
            continue
        fn = node.child_by_field_name("function")
        if fn is None:
            continue
        name = None
        if fn.type == "identifier":
            name = text_of(fn, src)
        elif fn.type in ("member_access_expression", "member_binding_expression"):
            nm = fn.child_by_field_name("name")
            ex = fn.child_by_field_name("expression")
            if nm is not None and ex is not None and ex.type == "identifier" and text_of(nm, src) == "Invoke":
                name = text_of(ex, src)
        elif fn.type == "conditional_access_expression":
            got = cond_access_names(fn, src)
            if got is not None and got[1] == "Invoke":
                name = got[0]
        if not name or name not in events_by_type:
            continue
        owners = events_by_type[name]
        cls = enclosing_class_node(node)
        cname = text_of(cls.child_by_field_name("name"), src) if cls is not None else None
        if cname is not None and cname in owners:
            continue                      # вызываем событие в типе, который его же и объявил — можно
        class_owners = owners - interfaces
        if not class_owners:
            continue                      # событие только в интерфейсе — не наш случай
        owner = ", ".join(sorted(class_owners))
        out.append((path, node.start_point[0] + 1, "L",
                    f"событие `{name}` объявлено в `{owner}`, а вызывается отсюда → CS0070: "
                    f"сделай в `{owner}` метод-обёртку (RaiseChanged)"))


# ------------------------------------------------------------------------- main
def main():
    folder = find_scripts_dir(sys.argv)
    files = collect(folder)
    if not files:
        print(f"[check_cs_api] не нашёл .cs в {folder}")
        return 1
    srcs = {p: open(p, "rb").read() for p in files}
    trees = {p: _PARSER.parse(srcs[p]) for p in files}
    idx = Index(files, srcs)
    cmap = class_map(idx)

    global GLOBAL_MEMBER_NAMES, GLOBAL_MEMBER_OWNERS
    GLOBAL_MEMBER_NAMES, GLOBAL_MEMBER_OWNERS = set(), {}
    events_by_type = {}
    interfaces = set()
    for t, (tnode, tsrc, tpath, _b) in cmap.items():
        m = class_members(tnode, tsrc)
        for bucket in ("methods", "props", "fields"):
            for nm in m[bucket]:
                GLOBAL_MEMBER_NAMES.add(nm)
                GLOBAL_MEMBER_OWNERS.setdefault(nm, set()).add(t)
        for nm in m["events"]:
            GLOBAL_MEMBER_NAMES.add(nm)
            GLOBAL_MEMBER_OWNERS.setdefault(nm, set()).add(t)
            events_by_type.setdefault(nm, set()).add(t)
        if tnode.type == "interface_declaration":
            interfaces.add(t)

    findings = []
    for p in files:
        tree = trees[p]
        src = srcs[p]
        check_A_bad_qualified(idx, p, src, tree, findings)
        check_B_missing_using(idx, p, src, tree, findings)
        check_C_override(idx, p, src, tree, findings, cmap)
        check_D_struct_fields(idx, p, src, tree, findings)
        check_E_attrs(idx, p, src, tree, findings)
        check_F_langversion(idx, p, src, tree, findings)
        check_G_static_members(idx, p, src, tree, findings, cmap)
        check_H_type_receiver(idx, p, src, tree, findings)
        check_H2_namespace_member(idx, p, src, tree, findings, cmap)
        check_I_member_of_var(idx, p, src, tree, findings, cmap)
        check_J_struct_property(idx, p, src, tree, findings, cmap)
        check_K_readonly(idx, p, src, tree, findings, cmap)
        check_L_event_outside(idx, p, src, tree, findings, events_by_type, interfaces, cmap)
        check_M_duplicate_members(idx, p, src, tree, findings)
        check_N_ugui_using(idx, p, src, tree, findings)
        check_O_nested_visibility(idx, p, src, tree, findings)
        check_P_member_shadows_namespace(idx, p, src, tree, findings, cmap)
        check_Q_bare_call_other_class(idx, p, src, tree, findings, cmap, None)
        check_R_wrong_scope_name(idx, p, src, tree, findings, cmap)
        check_S_dictionary_value(idx, p, src, tree, findings, cmap)
        check_T_unused_local(idx, p, src, tree, findings, cmap)
    check_O2_nested_cross_file(idx, files, srcs, trees, findings)

    seen = set()
    uniq = []
    for f in findings:
        k = (os.path.relpath(f[0], folder), f[1], f[2], f[3])
        if k in seen:
            continue
        seen.add(k)
        uniq.append(f)

    names = {"A": "ссылка в несуществующий тип", "B": "нет using для типа проекта",
             "G": "нет такого члена у проектного типа", "H": "тип без using (CS0103)",
             "I": "нет члена у типа переменной (CS1061)", "J": "правка структуры через свойство (CS1612)",
             "K": "readonly вне конструктора (CS0191)", "L": "событие из чужого типа (CS0070)",
             "C": "override без virtual в родителе", "D": "инициализатор поля структуры (C#9)",
             "E": "атрибут не на поле", "F": "синтаксис C#10+",
             "M": "дубль члена типа (CS0102)", "N": "нет using UnityEngine.UI (CS0246)",
             "O": "вложенный тип снаружи (CS0246)", "P": "член затеняет namespace (CS1061)",
             "Q": "метод другого класса без префикса (CS0103)", "R": "имя не в этой области (CS0103)",
             "S": "словарь без .Value (CS1061)",
             "T": "мертвая переменная (CS0219)"}
    for path, line, kind, msg in sorted(uniq, key=lambda x: (x[2], os.path.relpath(x[0], folder), x[1])):
        print(f"[{kind}] {os.path.relpath(path, folder)}:{line}  ({names[kind]})")
        print(f"      {msg}")

    print(f"\n[check_cs_api] файлов: {len(files)}, типов проиндексировано: {len(idx.type_ns)}, находок: {len(uniq)}")
    if not uniq:
        print("[check_cs_api] СВЯЗИ ЧИСТЫ — CS0246/CS0102/CS0506/CS8773/CS0592 исключены.")
        return 0
    print("[check_cs_api] проверь находки выше (ложные срабатывания возможны на B/C).")
    return 1


if __name__ == "__main__":
    sys.exit(main())
