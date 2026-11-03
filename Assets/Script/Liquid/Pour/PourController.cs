using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Satu tuangan yang dijalankan kode (bukan lewat drag): Gabungkan di Media 1 dan 3
public struct PourJob
{
    public VesselActor source;
    public VesselActor target;
    public Func<PourError> commit;      // perubahan data saat aliran mulai; null = session.TryPour 1 bagian
    public float durationScale;         // 1 = pourDuration; lebih besar = lebih lama (mis. banyaknya bagian); 0 dianggap 1
    public float seconds;               // > 0: durasi tuang dalam detik (menimpa durationScale)
    public bool disposeSource;          // true: sumber dimatikan setelah selesai (botol dari gambar), tidak kembali ke pivot
    public Action onDone;               // dijalankan setelah tuangan selesai
}

// Interaksi tuang (dipakai Media 1 dan Media 3): grab gelas, ray ke penerima, tuang dengan animasi,
// lalu kembali ke pivot asal. Letakkan di bawah World Root halaman yang bersangkutan.
public class PourController : MonoBehaviour
{
    [SerializeField] LiquidBinderBase binder;
    [SerializeField] VesselActor[] movableActors;      // boleh digrab dan dituang (Media 1: A, B, W; Media 3: gelas sirup, gelas air)
    [SerializeField] VesselActor[] fixedActors;        // hanya jadi penerima (Media 1: gelas akhir; Media 3: wadah)
    [SerializeField] WaterStream stream;
    [SerializeField] WaterStream secondStream;         // untuk dua tuangan bersamaan (Gabungkan); kosong = berbagi aliran pertama
    [SerializeField] Camera worldCamera;               // kosong = Camera.main
    [SerializeField] LayerMask pourMask;               // SATU layer khusus (mis. "Pour")
    [SerializeField] string notMovableMessage;         // kosong = pakai Media1Messages.NotMovable

    [Header("Gerak")]
    [SerializeField] float dragLift = 0.02f;           // meter: gelas terangkat saat di-drag
    [SerializeField] float dragFollow = 20f;           // makin besar makin cepat mengikuti jari
    [SerializeField] float approachTime = 0.3f;
    [SerializeField] float untiltTime = 0.25f;
    [SerializeField] float returnTime = 0.45f;

    [Header("Tuang")]
    [SerializeField] float tiltRate = 70f;             // derajat per detik
    [SerializeField] float maxTilt = 105f;
    [SerializeField] float pourDuration = 1.4f;        // detik untuk memindahkan 1 bagian
    [SerializeField] float lipMargin = 0.0005f;        // bibir sedikit di bawah permukaan supaya aliran terjaga (meter dunia)
    [SerializeField, Range(0.01f, 0.2f)] float streamWidthFraction = 0.05f;   // tebal aliran relatif terhadap tinggi gelas pemberi

    enum Mode { Idle, Dragging, Busy }
    static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    readonly RaycastHit[] hits = new RaycastHit[16];
    Mode mode;
    int activeRoutines;
    VesselActor dragged, hoverActor;
    PourError hoverError;
    Plane dragPlane;
    Vector3 grabOffset;

    Camera Cam => worldCamera ? worldCamera : Camera.main;
    int Layer => pourMask.value == 0 ? 0 : Mathf.RoundToInt(Mathf.Log(pourMask.value & -pourMask.value, 2f));
    public bool IsBusy => mode != Mode.Idle;     // sedang di-drag atau tuangan masih berjalan

    IEnumerable<VesselActor> AllActors()
    {
        if (movableActors != null) foreach (var a in movableActors) yield return a;
        if (fixedActors != null) foreach (var a in fixedActors) yield return a;
    }

    bool IsMovable(VesselActor a) => movableActors != null && Array.IndexOf(movableActors, a) >= 0;
    bool IsFixed(VesselActor a) => fixedActors != null && Array.IndexOf(fixedActors, a) >= 0;

    void OnEnable()
    {
        if (binder == null || stream == null || movableActors == null || movableActors.Length == 0)
        {
            Debug.LogError($"[{name}] PourController: Binder, Stream, dan minimal satu Movable Actor harus diisi.", this);
            return;
        }
        if (pourMask.value == 0) Debug.LogWarning($"[{name}] PourController: 'Pour Mask' kosong, ray tidak akan menemukan gelas.", this);

        foreach (VesselActor a in AllActors()) if (a) a.SetupCollider(Layer);
        mode = Mode.Idle;
        activeRoutines = 0;
    }

    void Update()
    {
        IPourSession session = binder ? binder.PourSession : null;
        Pointer ptr = Pointer.current;
        if (ptr == null || session == null || Cam == null) return;

        Vector2 pos = ptr.position.ReadValue();
        if (mode == Mode.Idle)
        {
            if (ptr.press.wasPressedThisFrame) TryGrab(pos);
        }
        else if (mode == Mode.Dragging)
        {
            UpdateDrag(pos, session);
            if (!ptr.press.isPressed) Release(session);
        }
    }

    // ---------------- job (Gabungkan) ----------------
    // Menjalankan beberapa tuangan lewat kode; bersamaan (aliran pertama dan kedua) atau bergiliran. false = sedang sibuk atau tidak ada job.
    public bool StartJobs(IList<PourJob> jobs, bool sequential)
    {
        IPourSession session = binder ? binder.PourSession : null;
        if (mode != Mode.Idle || session == null || jobs == null || jobs.Count == 0) return false;

        mode = Mode.Busy;
        if (sequential)
        {
            Run(SequenceRoutine(new List<PourJob>(jobs), session));
            return true;
        }

        if (jobs.Count > 1 && secondStream == null)
            Debug.LogWarning($"[{name}] PourController: 'Second Stream' kosong, dua tuangan bersamaan berbagi satu aliran.", this);

        for (int i = 0; i < jobs.Count; i++)
            Run(PourRoutine(jobs[i], session, i > 0 && secondStream != null ? secondStream : stream));
        return true;
    }

    IEnumerator SequenceRoutine(List<PourJob> jobs, IPourSession session)
    {
        foreach (PourJob job in jobs) yield return PourRoutine(job, session, stream);
    }

    // Semua gerak berjalan lewat Run; mode kembali Idle setelah routine terakhir selesai
    void Run(IEnumerator routine)
    {
        activeRoutines++;
        StartCoroutine(Tracked(routine));
    }

    IEnumerator Tracked(IEnumerator routine)
    {
        yield return routine;
        activeRoutines = Mathf.Max(0, activeRoutines - 1);
        if (activeRoutines == 0) mode = Mode.Idle;
    }

    // ---------------- grab dan drag ----------------
    void TryGrab(Vector2 pos)
    {
        if (IsOverUi(pos)) return;

        Ray ray = Cam.ScreenPointToRay(pos);
        int n = Physics.RaycastNonAlloc(ray, hits, 10f, pourMask, QueryTriggerInteraction.Collide);
        VesselActor best = null;
        float bestD = float.MaxValue;
        Vector3 bestPoint = default;
        for (int i = 0; i < n; i++)
        {
            var actor = hits[i].collider.GetComponentInParent<VesselActor>();
            if (actor == null || hits[i].distance >= bestD) continue;
            best = actor; bestD = hits[i].distance; bestPoint = hits[i].point;
        }
        if (best == null) return;

        if (IsFixed(best))
        {
            string msg = string.IsNullOrEmpty(notMovableMessage) ? Media1Messages.NotMovable : notMovableMessage;
            Feedback.Show(msg, FeedbackKind.Info);
            return;
        }
        if (!IsMovable(best)) return;      // bukan milik controller ini (halaman lain)

        dragged = best;
        dragPlane = new Plane(Vector3.up, bestPoint);
        grabOffset = best.transform.position - bestPoint;
        mode = Mode.Dragging;
    }

    void UpdateDrag(Vector2 pos, IPourSession session)
    {
        Ray ray = Cam.ScreenPointToRay(pos);
        if (dragPlane.Raycast(ray, out float dist))
        {
            Vector3 desired = ray.GetPoint(dist) + grabOffset + Vector3.up * dragLift;
            dragged.transform.position = Vector3.Lerp(dragged.transform.position, desired, 1f - Mathf.Exp(-dragFollow * Time.deltaTime));
        }

        // penerima terdekat di bawah jari (gelas yang di-drag dan aktor halaman lain diabaikan)
        VesselActor found = null;
        float bestD = float.MaxValue;
        int n = Physics.RaycastNonAlloc(ray, hits, 10f, pourMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            var actor = hits[i].collider.GetComponentInParent<VesselActor>();
            if (actor == null || actor == dragged || hits[i].distance >= bestD) continue;
            if (!IsMovable(actor) && !IsFixed(actor)) continue;
            found = actor; bestD = hits[i].distance;
        }

        PourError err = found != null ? CheckPour(dragged, found, session) : PourError.None;
        if (found != hoverActor && hoverActor) hoverActor.SetTargeted(false);
        hoverActor = found;
        hoverError = err;
        if (hoverActor) hoverActor.SetTargeted(err == PourError.None);
    }

    PourError CheckPour(VesselActor from, VesselActor to, IPourSession session)
    {
        if (!binder.TryGetContainer(from.Vessel, out LiquidContainer a) || !binder.TryGetContainer(to.Vessel, out LiquidContainer b))
            return PourError.NotConfigured;
        return session.CheckPour(a, b);
    }

    void Release(IPourSession session)
    {
        VesselActor src = dragged, tgt = hoverActor;
        PourError err = hoverError;
        if (tgt) tgt.SetTargeted(false);
        dragged = null;
        hoverActor = null;
        mode = Mode.Busy;

        if (tgt != null && err == PourError.None)
        {
            Run(PourRoutine(new PourJob { source = src, target = tgt }, session, stream));
            return;
        }

        if (tgt != null)
        {
            string msg = Media1Messages.For(err);
            if (!string.IsNullOrEmpty(msg)) Feedback.Show(msg, FeedbackKind.Warning);
        }
        Run(ReturnHome(src, 0f));
    }

    // ---------------- tuang ----------------
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    IEnumerator PourRoutine(PourJob job, IPourSession session, WaterStream waterStream)
    {
        VesselActor src = job.source, tgt = job.target;
        float duration = job.seconds > 0f ? job.seconds : pourDuration * (job.durationScale > 0f ? job.durationScale : 1f);

        Transform root = src.transform;
        LiquidVessel sv = src.Vessel, tv = tgt.Vessel;
        binder.TryGetContainer(sv, out LiquidContainer sc);
        binder.TryGetContainer(tv, out LiquidContainer tc);

        Vector3 up = Vector3.up;
        Vector3 d = Flat(tv.RimCenterWorld - sv.RimCenterWorld);
        d = d.sqrMagnitude > 1e-8f ? d.normalized : Flat(root.right).normalized;

        Quaternion startRot = root.rotation;
        Vector3 startPos = root.position;

        // wadah bercerat: putar (yaw) supaya cerat menghadap penerima
        Quaternion yawRot = startRot;
        if (sv.HasSpout)
        {
            Vector3 spout = Flat(sv.SpoutDirectionWorld);
            if (spout.sqrMagnitude > 1e-8f) yawRot = Quaternion.AngleAxis(Vector3.SignedAngle(spout, d, up), up) * startRot;
        }

        Vector3 lipLocal = sv.LipLocal(d, true);
        Vector3 offset0 = sv.Glass.TransformPoint(lipLocal) - startPos;      // posisi bibir relatif akar pada rotasi startRot
        Vector3 lipTarget = tgt.PourPointWorld(d);

        // pose akar supaya bibir tepat di lipTarget untuk rotasi rot
        Vector3 PosFor(Quaternion rot) => lipTarget - (rot * Quaternion.Inverse(startRot)) * offset0;

        // 1) pindah ke atas penerima
        Vector3 p1 = PosFor(yawRot);
        for (float t = 0f; t < 1f; t += Time.deltaTime / approachTime)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            root.SetPositionAndRotation(Vector3.Lerp(startPos, p1, e), Quaternion.Slerp(startRot, yawRot, e));
            yield return null;
        }
        root.SetPositionAndRotation(p1, yawRot);

        // 2) miring: aliran mulai saat cairan menyentuh bibir; makin miring seiring cairan berkurang
        Vector3 tiltAxis = Vector3.Cross(up, d).normalized;
        float angle = 0f, holdAtMax = 0f, flowT = 0f;
        bool flowing = false;
        float width = sv.DepthWorld * streamWidthFraction;

        while (true)
        {
            float dt = Time.deltaTime;
            Vector3 lipNow = sv.Glass.TransformPoint(lipLocal);
            float sd = Vector3.Dot(sv.LiquidPlaneNormalWorld, lipNow - sv.LiquidPlanePointWorld);   // <= 0: bibir di bawah permukaan
            bool reached = sv.LiquidVisible && sd <= 0f;

            if (!flowing)
            {
                if (angle >= maxTilt) holdAtMax += dt;
                if (reached || holdAtMax > 0.15f)
                {
                    flowing = true;
                    binder.BeginPourWindow(duration);                      // level dan warna bergerak sepanjang durasi tuang
                    PourError e = job.commit != null ? job.commit() : session.TryPour(sc, tc);    // data berubah sekali, saat aliran mulai
                    if (e != PourError.None)
                    {
                        string msg = Media1Messages.For(e);
                        if (!string.IsNullOrEmpty(msg)) Feedback.Show(msg, FeedbackKind.Warning);
                        break;
                    }
                }
            }

            if (flowing)
            {
                flowT += dt;
                waterStream.SetPath(lipNow, tv.LiquidSurfaceCenterWorld(), width, sv.LiquidColor);
                if (flowT >= duration) break;
                if (sd > -lipMargin) angle = Mathf.Min(angle + tiltRate * dt, maxTilt);
            }
            else
            {
                angle = Mathf.Min(angle + tiltRate * dt, maxTilt);
            }

            Quaternion rot = Quaternion.AngleAxis(angle, tiltAxis) * yawRot;
            root.SetPositionAndRotation(PosFor(rot), rot);
            yield return null;
        }
        waterStream.Hide();

        // 3) tegak lagi (di sekitar bibir), lalu kembali ke pivot asal
        float a0 = angle;
        for (float t = 0f; t < 1f; t += Time.deltaTime / untiltTime)
        {
            Quaternion rot = Quaternion.AngleAxis(Mathf.Lerp(a0, 0f, Mathf.SmoothStep(0f, 1f, t)), tiltAxis) * yawRot;
            root.SetPositionAndRotation(PosFor(rot), rot);
            yield return null;
        }

        if (job.disposeSource) src.gameObject.SetActive(false);        // botol: hilang setelah menuang
        else yield return ReturnHome(src, returnTime);
        job.onDone?.Invoke();
    }

    // Kembali ke pose asal dalam ruang lokal, supaya tetap benar walau Image Target bergerak
    IEnumerator ReturnHome(VesselActor actor, float duration)
    {
        Transform root = actor.transform;
        Vector3 p0 = root.localPosition, p1 = actor.HomeLocalPosition;
        Quaternion r0 = root.localRotation, r1 = actor.HomeLocalRotation;
        if (duration <= 0f) duration = returnTime;

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            root.localPosition = Vector3.Lerp(p0, p1, e);
            root.localRotation = Quaternion.Slerp(r0, r1, e);
            yield return null;
        }
        root.localPosition = p1;
        root.localRotation = r1;
    }

    // UI HUD (overlay) selalu menghalangi; UI world-space hanya kalau interaktif
    static bool IsOverUi(Vector2 pos)
    {
        if (EventSystem.current == null) return false;
        var data = new PointerEventData(EventSystem.current) { position = pos };
        uiHits.Clear();
        EventSystem.current.RaycastAll(data, uiHits);

        for (int i = 0; i < uiHits.Count; i++)
        {
            GameObject go = uiHits[i].gameObject;
            Canvas canvas = go.GetComponentInParent<Canvas>();
            bool worldSpace = canvas != null && canvas.rootCanvas.renderMode == RenderMode.WorldSpace;
            if (!worldSpace) return true;

            if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(go) != null
                || ExecuteEvents.GetEventHandler<IPointerDownHandler>(go) != null
                || ExecuteEvents.GetEventHandler<IDragHandler>(go) != null)
                return true;
        }
        return false;
    }
}