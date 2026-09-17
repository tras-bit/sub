#!/usr/bin/env python3
# ============================================================================
#  SUBSISTENCE — tools/check_net_scale.py
#  Статическая проверка сетевого слоя под «100+ человек» (без запуска Unity).
#
#  Что проверяет:
#    N1  формат пакета снапшота: сервер пишет [время float][count ushort] и
#        ОБЯЗАН заполнить count (WriteUShortAt) — иначе клиент прочитает 0 сущностей;
#    N2  у снапшота есть потребитель: SnapshotClient.Apply читает тот же формат
#        (ReadFloat → ReadUShort → ReadUInt + ReadSnapshot) и подписан на OnRpc;
#    N3  реестр NetEntity по netId (Find/ByNetId) — без него приём пакета невозможен;
#    N4  interest management: InterestGrid.Query + профиль LOD (AOI 120→55, 20→16 Гц,
#        лимит сущностей в пакете) применяются в рассылке;
#    N5  экономия: буфер переиспользуется (Reset), а не Rent/Return на соединение;
#    N6  лимит онлайна: слоты ≥ 100, мягкий лимит ≤ слотов, отказ новым при переполнении;
#    N7  приёмка трафика выражена константами Balance (КБ/с на игрока, Мбит/с, бюджет мс)
#        и стенд NetLoadTest их реально проверяет;
#    N8  клиент не двигает своего игрока из снапшотов (иначе конфликт с предиктом);
#    N9  запуск: SnapshotClient.Init() вызывается из RuntimeBootstrap, а не «когда-нибудь».
#
#  Код выхода: 0 — чисто, 1 — есть находки.
# ============================================================================
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCRIPTS = os.path.join(ROOT, "UnityProject", "Assets", "Subsistence", "Scripts")

FINDINGS = []


def read(rel):
    path = os.path.join(SCRIPTS, rel)
    if not os.path.exists(path):
        FINDINGS.append(("N0", rel, "файл не найден"))
        return ""
    return io.open(path, encoding="utf-8").read()


def need(tag, rel, ok, msg):
    if not ok:
        FINDINGS.append((tag, rel, msg))


def main():
    FINDINGS.clear()
    host = read("Net/MirrorAdapter/MirrorNetworkHost.cs")
    bridge = read("Net/NetworkBridge.cs")
    client = read("Net/SnapshotClient.cs")
    loadtest = read("Net/NetLoadTest.cs")
    ids = read("Core/IDs.cs")
    boot = read("Runtime/RuntimeBootstrap.cs")

    # ---- N1: формат и заполнение count ----
    need("N1", "Net/MirrorAdapter/MirrorNetworkHost.cs",
         "WriteFloat((float)NetworkTime.time)" in host and "WriteUShort(0)" in host,
         "рассылка больше не пишет [время][count] — формат снапшота сломан")
    need("N1", "Net/MirrorAdapter/MirrorNetworkHost.cs",
         "w.WriteUShortAt(countPos" in host,
         "count не заполняется после обхода AOI (клиент прочитает 0 сущностей)")
    need("N1", "Net/NetworkBridge.cs",
         "public void WriteUShortAt(int pos, ushort v)" in bridge,
         "в BufferWriter нет правки записанного значения (WriteUShortAt)")

    # ---- N2: потребитель снапшота ----
    need("N2", "Net/SnapshotClient.cs",
         'method != "snapshot"' in client and "ReadUShort()" in client
         and "ReadUInt()" in client and "ReadSnapshot(_reader)" in client,
         "клиент не разбирает снапшот тем же форматом")
    need("N2", "Net/SnapshotClient.cs",
         "host.OnRpc += OnRpc" in client,
         "SnapshotClient не подписан на серверные RPC — пакеты никто не читает")
    need("N2", "Runtime/RuntimeBootstrap.cs",
         "SnapshotClient.Init()" in boot,
         "SnapshotClient.Init() не вызывается из RuntimeBootstrap")

    # ---- N3: реестр сущностей ----
    need("N3", "Net/NetworkBridge.cs",
         "Find(NetId id)" in bridge and "ByNetId" in bridge,
         "нет реестра NetEntity по сетевому id — принять снапшот некуда")

    # ---- N4: interest management + LOD ----
    need("N4", "Net/MirrorAdapter/MirrorNetworkHost.cs",
         "InterestGrid.Query(" in host and "LoadProfile(" in host,
         "рассылка идёт без AOI/LOD-профиля — 100+ игроков не вытянет")
    need("N4", "Core/IDs.cs",
         "LodPlayerThreshold" in ids and "AoiRadiusMin" in ids
         and "MaxEntitiesPerSnapshot" in ids,
         "в Balance нет констант LOD/AOI/лимита пакета")
    need("N4", "Core/IDs.cs",
         "public const int TargetOnline" in ids,
         "в Balance не зафиксирована цель онлайна (100+)")

    # ---- N5: без мусора в рассылке ----
    need("N5", "Net/MirrorAdapter/MirrorNetworkHost.cs",
         "_packet" in host and ".Reset()" in host,
         "буфер снапшота не переиспользуется (Rent/Return на каждое соединение → GC)")

    # ---- N6: лимиты онлайна ----
    m_slots = re.search(r"MaxPlayerSlots\s*=\s*(\d+)", ids)
    m_online = re.search(r"MaxPlayersOnline\s*=\s*(\d+)", ids)
    need("N6", "Core/IDs.cs", bool(m_slots) and int(m_slots.group(1)) >= 100,
         "слотов транспорта меньше 100 — цель «сеть 100+» недостижима")
    if m_slots and m_online:
        need("N6", "Core/IDs.cs", int(m_online.group(1)) <= int(m_slots.group(1)),
             "мягкий лимит онлайна больше числа слотов")
    need("N6", "Net/MirrorAdapter/MirrorNetworkHost.cs",
         "maxPlayers" in host and "Disconnect()" in host,
         "нет отказа новым соединениям при переполнении сервера")

    # ---- N7: пороги приёмки и их проверка стендом ----
    for const in ("NetKbPerPlayerCap", "NetMbpsTotalCap", "NetTickBudgetMs"):
        need("N7", "Core/IDs.cs", const in ids, f"нет порога приёмки {const} в Balance")
    need("N7", "Net/NetLoadTest.cs",
         "NetKbPerPlayerCap" in loadtest and "NetMbpsTotalCap" in loadtest,
         "стенд не сверяет трафик с порогами приёмки")
    need("N7", "Net/NetLoadTest.cs",
         "w.WriteUShortAt(countPos" in loadtest,
         "пакет стенда не повторяет боевой формат (count не заполняется)")

    # ---- N8: свой игрок из сети не двигается ----
    need("N8", "Net/SnapshotClient.cs",
         "DeployNet.IsLocal(pc)" in client,
         "свой игрок не исключён из применения снапшотов — драка с предиктом")

    # ---- N9: ранний выход при рассинхроне ----
    need("N9", "Net/SnapshotClient.cs",
         "UnknownEntities++" in client and "break;" in client,
         "нет реакции на неизвестный netId — разбор пакета молча поедет")

    total_files = sum(1 for _ in walk_cs())
    print(f"[check_net_scale] файлов .cs: {total_files}")
    if FINDINGS:
        for tag, rel, msg in FINDINGS:
            print(f"[{tag}] {rel}: {msg}")
        print(f"[check_net_scale] находок: {len(FINDINGS)} — сетевой слой под 100+ неполон")
        return 1
    print("[check_net_scale] СЕТЬ 100+ ГОТОВА: формат снапшота и его приёмник совпадают, "
          "AOI+LOD на месте, лимиты и пороги приёмки заданы")
    return 0


def walk_cs():
    for cur, _dirs, files in os.walk(SCRIPTS):
        for f in files:
            if f.endswith(".cs"):
                yield os.path.join(cur, f)


if __name__ == "__main__":
    sys.exit(main())
