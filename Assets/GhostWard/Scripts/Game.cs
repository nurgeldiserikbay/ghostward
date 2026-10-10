using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GhostWard
{
    // Точка входа: лежит на единственном объекте сцены Main и строит всё остальное из кода.
    public class Game : MonoBehaviour
    {
        enum Phase { Loading, Menu, Playing, Over }

        const int MaxHp = 3;
        const float MinScore = 0.74f;
        const string BestKey = "best";

        Phase phase;
        GameUI ui;
        Camera cam;
        bool arMode;
        AudioSource sfx;

        readonly List<Ghost> ghosts = new();
        int hp, score, combo, wave;
        int toSpawn, maxAlive;
        float spawnTimer, spawnInterval, speedMul, waveBreak;

        readonly List<Vector2> stroke = new();
        bool drawing;

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;

            sfx = gameObject.AddComponent<AudioSource>();
            sfx.spatialBlend = 0f;

            ui = GameUI.Create();
            ui.StartPressed += StartRun;

            ArRig ar = null;
            yield return ArRig.TryCreate(r => ar = r);
            if (ar != null)
            {
                cam = ar.Cam;
                arMode = true;
            }
            else
            {
                var gyro = GyroRig.Create();
                cam = gyro.Cam;
                arMode = false;
                if (!gyro.HasGyro && !Application.isEditor)
                    ui.Message("Гироскоп не найден", 4f);
            }

            ui.ShowMenu(arMode
                ? "Режим AR: призраки живут в комнате. Можно ходить."
                : "Режим без AR: стой на месте и поворачивайся.");
            phase = Phase.Menu;
        }

        void StartRun()
        {
            foreach (var g in ghosts) if (g != null) g.Vanish();
            ghosts.Clear();
            hp = MaxHp;
            score = 0;
            combo = 0;
            wave = 0;
            drawing = false;
            ui.SetHp(hp);
            ui.SetScore(score, combo);
            ui.ShowHud();
            phase = Phase.Playing;
            NextWave();
        }

        void NextWave()
        {
            wave++;
            toSpawn = 3 + wave * 2;
            maxAlive = Mathf.Min(1 + wave, 5);
            spawnInterval = Mathf.Max(1.3f, 4.2f - wave * 0.4f);
            speedMul = 1f + (wave - 1) * 0.08f;
            spawnTimer = 1.5f;
            waveBreak = 0f;
            ui.SetWave(wave);

            // новые типы открываются по одному, чтобы игрок успел выучить знак
            for (int i = 0; i < GhostCatalog.All.Length; i++)
                ui.SetLegendVisible(GhostCatalog.All[i].Kind, i < UnlockedKinds());
            if (wave <= GhostCatalog.All.Length)
            {
                var def = GhostCatalog.All[wave - 1];
                ui.Message($"Волна {wave}\nНовый враг: {def.Name}\nзнак — {GhostCatalog.SigilName(def.Ward)}", 3f);
            }
            else ui.Message($"Волна {wave}", 2f);
        }

        int UnlockedKinds() => Mathf.Min(wave, GhostCatalog.All.Length);

        void Update()
        {
            if (phase != Phase.Playing) return;

            ghosts.RemoveAll(g => g == null);
            HandleStroke();
            UpdateSpawning();
            ui.UpdateIndicators(cam, ghosts);
        }

        void UpdateSpawning()
        {
            int alive = 0;
            foreach (var g in ghosts) if (g.Alive) alive++;

            if (toSpawn == 0)
            {
                if (alive > 0) return;
                waveBreak += Time.deltaTime;
                if (waveBreak > 2.5f) NextWave();
                return;
            }

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f || alive >= maxAlive) return;
            spawnTimer = spawnInterval * Random.Range(0.8f, 1.2f);
            toSpawn--;
            Spawn();
        }

        void Spawn()
        {
            var def = GhostCatalog.All[Random.Range(0, UnlockedKinds())];

            // на первых волнах призраки приходят спереди, дальше — отовсюду, в том числе со спины
            float spread = Mathf.Lerp(50f, 180f, Mathf.InverseLerp(1f, 4f, wave));
            float yaw = Random.Range(-spread, spread);
            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-3f) forward = Vector3.forward;
            var dir = Quaternion.Euler(0f, yaw, 0f) * forward.normalized;
            float dist = Random.Range(2.6f, 4.2f);
            var pos = cam.transform.position + dir * dist + Vector3.up * Random.Range(-0.3f, 0.4f);

            var ghost = Ghost.Create(def, pos, cam.transform, speedMul);
            ghost.Struck += OnStruck;
            ghosts.Add(ghost);
        }

        void OnStruck(Ghost g)
        {
            if (phase != Phase.Playing) return;
            hp--;
            combo = 0;
            ui.SetHp(hp);
            ui.SetScore(score, combo);
            ui.Damage();
            sfx.PlayOneShot(ProceduralAudio.Hit(), 1f);
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
            if (hp <= 0) GameOver();
        }

        void GameOver()
        {
            phase = Phase.Over;
            foreach (var g in ghosts) if (g != null && g.Alive) g.Banish();
            int best = Mathf.Max(score, PlayerPrefs.GetInt(BestKey, 0));
            PlayerPrefs.SetInt(BestKey, best);
            PlayerPrefs.Save();
            ui.ShowGameOver(score, best, wave);
        }

        // ---------- рисование знаков ----------

        void HandleStroke()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;
            bool pressed = pointer.press.isPressed;
            var pos = pointer.position.ReadValue();

            if (pressed && !drawing)
            {
                drawing = true;
                stroke.Clear();
                stroke.Add(pos);
            }
            else if (pressed && drawing)
            {
                if (Vector2.Distance(stroke[^1], pos) > Screen.height * 0.004f)
                {
                    stroke.Add(pos);
                    ui.SetStroke(stroke, new Color(1f, 1f, 1f, 0.9f));
                }
            }
            else if (!pressed && drawing)
            {
                drawing = false;
                FinishStroke();
            }
        }

        void FinishStroke()
        {
            if (SigilRecognizer.PathLength(stroke) < Screen.height * 0.12f)
            {
                ui.ReleaseStroke(Color.white);
                return;
            }

            var sigil = SigilRecognizer.Recognize(stroke, out float quality);
            if (quality < MinScore)
            {
                ui.ReleaseStroke(new Color(0.6f, 0.6f, 0.6f));
                ui.Message("Знак не распознан");
                sfx.PlayOneShot(ProceduralAudio.Fail(), 0.8f);
                return;
            }

            var centroid = Vector2.zero;
            foreach (var p in stroke) centroid += p;
            centroid /= stroke.Count;

            // цель — видимый призрак ближе всего к центру знака; подходящий по типу в приоритете
            Ghost match = null, nearest = null;
            float matchD = float.MaxValue, nearestD = float.MaxValue;
            foreach (var g in ghosts)
            {
                if (!g.Alive) continue;
                var sp = cam.WorldToScreenPoint(g.transform.position);
                if (sp.z <= 0f || sp.x < 0 || sp.x > Screen.width || sp.y < 0 || sp.y > Screen.height) continue;
                float d = Vector2.Distance(centroid, sp);
                if (d < nearestD) { nearestD = d; nearest = g; }
                if (g.Def.Ward == sigil && d < matchD) { matchD = d; match = g; }
            }

            if (match != null)
            {
                match.Banish();
                combo++;
                score += Mathf.RoundToInt(100 * (1f + (combo - 1) * 0.25f));
                ui.SetScore(score, combo);
                ui.ReleaseStroke(match.Def.Color);
            }
            else if (nearest != null)
            {
                nearest.Enrage();
                combo = 0;
                ui.SetScore(score, combo);
                ui.ReleaseStroke(new Color(0.5f, 0.5f, 0.5f));
                ui.Message($"{GhostCatalog.SigilName(sigil)} не действует!\n{nearest.Def.Name} — {GhostCatalog.SigilName(nearest.Def.Ward)}");
                sfx.PlayOneShot(ProceduralAudio.Fail(), 0.8f);
            }
            else
            {
                ui.ReleaseStroke(Color.white);
                ui.Message("Наведи камеру на призрака");
            }
        }
    }
}
