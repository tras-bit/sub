// ============================================================================
//  SUBSISTENCE — AI/MonsterMotion.cs
//  Процедурная анимация монстров: костей в моделях нет, поэтому «жизнь» даётся
//  смещением визуального меша (ребёнка объекта) — покачивание при ходьбе,
//  дыхание в покое, крен при повороте, дрожь Искровика, заваливание при смерти.
//  Сам объект не двигается: физика, коллайдер и сеть остаются нетронутыми.
// ============================================================================
using UnityEngine;

namespace Subsistence.AI
{
    public class MonsterMotion : MonoBehaviour
    {
        MonsterRuntime _rt;
        Transform _vis;
        Vector3 _basePos;
        Quaternion _baseRot;
        float _phase;
        float _speed01;           // 0 — стоит, 1 — бежит
        Vector3 _last;
        float _deathT = -1f;
        float _flashT;

        void Start()
        {
            _rt = GetComponent<MonsterRuntime>();
            _last = transform.position;
            _vis = FindVisual(transform);
            if (_vis != null) { _basePos = _vis.localPosition; _baseRot = _vis.localRotation; }
        }

        /// <summary>Первый ребёнок с мешем — это модель из ModelLibrary (держатель + меш).</summary>
        static Transform FindVisual(Transform root)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.GetComponentInChildren<MeshRenderer>() != null) return c;
            }
            return null;
        }

        void Update()
        {
            if (_vis == null || _rt == null) return;

            float dt = Time.deltaTime;
            var pos = transform.position;
            float moved = Vector3.Distance(pos, _last);
            _last = pos;
            float targetSpeed = Mathf.Clamp01(moved / Mathf.Max(0.0001f, dt) / 4.5f);
            _speed01 = Mathf.MoveTowards(_speed01, targetSpeed, dt * 2.5f);

            // смерть: заваливается вперёд и оседает
            if (!_rt.IsAlive)
            {
                if (_deathT < 0f) _deathT = 0f;
                _deathT = Mathf.Min(1f, _deathT + dt * 1.6f);
                float k = Mathf.SmoothStep(0f, 1f, _deathT);
                _vis.localRotation = _baseRot * Quaternion.Euler(k * 82f, 0f, k * 12f);
                _vis.localPosition = _basePos + Vector3.down * (k * 0.12f);
                return;
            }

            // в погоне движения резче — видно, что тварь несётся
            float chase = _rt.State == MonsterState.Chase ? 1.4f : 1f;
            float stride = Mathf.Lerp(2.2f, 8.5f, _speed01) * chase;
            _phase += dt * stride;
            float bob = Mathf.Abs(Mathf.Sin(_phase)) * 0.07f * (0.4f + _speed01) * chase;
            float breath = Mathf.Sin(Time.time * 1.5f) * 0.012f * (1f - _speed01);

            // крен вбок на шаге + наклон вперёд на бегу
            float roll = Mathf.Sin(_phase) * Mathf.Lerp(1.2f, 7.0f, _speed01) * chase;
            float pitch = _speed01 * 8.0f * chase;

            switch (_rt.kind)
            {
                case MonsterKind.Whisperer:
                    // тянется вперёд, покачиваясь боком — «крадётся»
                    roll *= 1.6f; pitch += 4f;
                    bob *= 0.7f;
                    break;
                case MonsterKind.Drowned:
                    // тяжёлый: медленное качание вбок, почти без подпрыгивания
                    roll *= 2.2f; bob *= 0.45f; pitch *= 0.5f;
                    break;
                case MonsterKind.Spark:
                    // дрожь и рывки: высокочастотный jitter
                    float j = 0.014f + 0.02f * _speed01;
                    _vis.localPosition = _basePos + new Vector3(
                        Mathf.PerlinNoise(Time.time * 26f, 0f) * 2f - 1f,
                        bob + breath,
                        Mathf.PerlinNoise(0f, Time.time * 24f) * 2f - 1f) * j;
                    _vis.localRotation = _baseRot * Quaternion.Euler(pitch, Mathf.Sin(_phase * 1.3f) * 3f, roll);
                    // «искры»: короткие вспышки звука раз в ~1.5 с
                    _flashT -= dt;
                    if (_flashT <= 0f) { _flashT = Random.Range(1.1f, 2.4f); }
                    return;
                case MonsterKind.Hound:
                    pitch = _speed01 * 3f;      // четвероногая — почти не наклоняется
                    bob *= 0.6f;
                    break;
            }

            _vis.localPosition = _basePos + Vector3.up * (bob + breath);
            _vis.localRotation = _baseRot * Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
