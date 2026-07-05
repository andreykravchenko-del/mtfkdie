using UnityEngine;

/// <summary>
/// Хаотичное «тараканье» движение в пределах радиуса вокруг опорной точки.
/// Таракан короткими рывками бежит к случайным целям внутри круга, по пути
/// вихляет, иногда резко меняет цель и замирает — как настоящий таракан.
/// Двигается по горизонтали на своей стартовой высоте (без физики), только в режиме Explore.
/// Вешается на каждого таракана отдельно; несколько экземпляров десинхронизированы автоматически.
/// </summary>
public class CockroachWanderer : MonoBehaviour
{
    [Header("Область блуждания")]
    [Tooltip("Центр зоны. Если пусто — берётся стартовая позиция таракана.")]
    [SerializeField] private Transform anchor;
    [Tooltip("Радиус круга (по горизонтали), в котором таракан бегает.")]
    [SerializeField] private float radius = 2f;

    [Header("Скорость и рывки")]
    [Tooltip("Минимальная скорость рывка, м/с.")]
    [SerializeField] private float minSpeed = 1.5f;
    [Tooltip("Максимальная скорость рывка, м/с.")]
    [SerializeField] private float maxSpeed = 4f;
    [Tooltip("Как быстро таракан разворачивается к направлению бега, град/с.")]
    [SerializeField] private float turnSpeed = 720f;

    [Header("Хаотичность")]
    [Tooltip("Дистанция до цели, при которой считаем, что добежал, м.")]
    [SerializeField] private float arriveDistance = 0.15f;
    [Tooltip("Макс. длительность одного рывка до принудительной смены цели, сек (мин/макс).")]
    [SerializeField] private Vector2 dartDuration = new Vector2(0.4f, 1.5f);
    [Tooltip("Шанс замереть по достижении цели, 0..1.")]
    [Range(0f, 1f)]
    [SerializeField] private float pauseChance = 0.5f;
    [Tooltip("Длительность паузы-замирания между рывками, сек (мин/макс).")]
    [SerializeField] private Vector2 pauseDuration = new Vector2(0.05f, 0.6f);
    [Tooltip("Сила вихляния при беге: 0 — строго к цели, больше — сильнее петляет.")]
    [Range(0f, 1f)]
    [SerializeField] private float wiggle = 0.35f;

    private Vector3 startPos;     // стартовая позиция — центр по умолчанию
    private Vector3 target;       // текущая цель внутри радиуса
    private float dartSpeed;      // скорость текущего рывка
    private float dartTimer;      // сколько ещё бежать до принудительной смены цели
    private float pauseTimer;     // сколько ещё стоять на месте
    private float wigglePhase;    // фазовый сдвиг вихляния — свой у каждого таракана

    // Живой центр зоны: следует за anchor, если он задан.
    private Vector3 Center => anchor != null ? anchor.position : startPos;

    void Awake()
    {
        startPos = transform.position;
        wigglePhase = Random.Range(0f, 100f); // десинхронизируем нескольких тараканов
        PickNewTarget();
    }

    void Update()
    {
        // Тараканы бегают только в режиме исследования.
        if (GameManager.Instance == null || GameManager.Instance.Mode != GameMode.Explore) return;

        // Замерли — пережидаем паузу.
        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;
            return;
        }

        Scurry();
    }

    /// Рывок к текущей цели: поворот + движение вперёд с вихлянием.
    void Scurry()
    {
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f; // держимся горизонтальной плоскости

        // Добежал или бежит слишком долго — иногда замираем и берём новую цель.
        dartTimer -= Time.deltaTime;
        if (toTarget.magnitude <= arriveDistance || dartTimer <= 0f)
        {
            if (Random.value < pauseChance)
                pauseTimer = Random.Range(pauseDuration.x, pauseDuration.y);
            PickNewTarget();
            return;
        }

        // Направление к цели + боковое вихляние (перпендикуляр в горизонтали).
        Vector3 dir = toTarget.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        dir = (dir + side * Mathf.Sin(Time.time * 18f + wigglePhase) * wiggle).normalized;

        // Снапово доворачиваемся мордой по ходу движения.
        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);

        // Едем вперёд.
        transform.position += dir * dartSpeed * Time.deltaTime;
    }

    /// Новая случайная цель внутри круга + новая скорость/таймаут рывка.
    void PickNewTarget()
    {
        Vector2 rand = Random.insideUnitCircle * Mathf.Max(0f, radius);
        target = Center + new Vector3(rand.x, 0f, rand.y);
        dartSpeed = Random.Range(minSpeed, maxSpeed);
        dartTimer = Random.Range(dartDuration.x, dartDuration.y);
    }

    // Визуализация зоны блуждания в редакторе (при выделении объекта).
    void OnDrawGizmosSelected()
    {
        Vector3 c = anchor != null ? anchor.position
                                   : (Application.isPlaying ? startPos : transform.position);
        Gizmos.color = new Color(0.85f, 0.9f, 0.2f, 0.6f);

        const int seg = 40;
        Vector3 prev = c + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            Vector3 next = c + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
