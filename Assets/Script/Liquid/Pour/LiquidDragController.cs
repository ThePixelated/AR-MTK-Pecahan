using System;
using UnityEngine;
using UnityEngine.UI;

public struct LiquidDrop
{
    public LiquidKind kind;
    public VesselActor target;       // gelas di bawah jari saat dilepas; null = bukan di gelas mana pun
    public VesselActor bottle;       // botol 3D (aktif) di titik lepas: dipakai tuangan atau dimatikan penerima event
    public Vector3 releaseWorld;
}

// Tarik gambar air atau botol sirup (UI) ke dunia 3D: di dalam HUD ghost 2D, di luar HUD botol 3D mengikuti jari.
public class LiquidDragController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] RectTransform hudRect;            // area HUD: di dalamnya ghost 2D, di luarnya botol 3D
    [SerializeField] RectTransform ghostRoot;          // induk ghost 2D (Canvas atau HUD)
    [SerializeField] Vector2 fingerOffset = new Vector2(0f, 90f);
    [SerializeField, Range(0.1f, 1f)] float ghostAlpha = 0.85f;

    [Header("Dunia 3D")]
    [SerializeField] Camera worldCamera;               // kosong = Camera.main
    [SerializeField] LayerMask pourMask;               // layer yang sama dengan PourController
    [SerializeField] VesselActor bottleWater;          // model botol air (VesselActor dan LiquidVessel), NONAKTIF di scene
    [SerializeField] VesselActor bottleSyrup;          // model botol sirup, NONAKTIF di scene
    [SerializeField] VesselActor heightReference;      // gelas yang bibirnya jadi acuan tinggi botol saat melayang
    [SerializeField] float hoverHeight = 0.06f;        // meter di atas bibir gelas acuan

    // Diisi controller halaman
    public Func<LiquidKind, VesselActor, bool> IsValidTarget;
    public Func<LiquidKind, Color> BottleColor;

    public event Action<LiquidDrop> Dropped;          // dilepas di dunia (target boleh null)
    public event Action Cancelled;                     // dilepas di HUD atau dibatalkan

    public bool IsDragging { get; private set; }

    static readonly RaycastHit[] hits = new RaycastHit[16];

    LiquidKind kind;
    RectTransform ghost;
    VesselActor bottle, hover;
    Camera uiCam;
    bool overHud;

    Camera WorldCam => worldCamera ? worldCamera : Camera.main;

    void OnDisable() => Cancel();

    public bool Begin(LiquidKind liquidKind, Sprite sprite, Vector2 size, Vector2 screenPos, Camera uiCamera)
    {
        if (IsDragging) return false;
        if (ghostRoot == null)
        {
            Debug.LogError($"[{name}] LiquidDragController: 'Ghost Root' harus diisi.", this);
            return false;
        }

        kind = liquidKind;
        uiCam = uiCamera;
        IsDragging = true;

        var go = new GameObject("LiquidGhost", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(ghostRoot, false);
        ghost = (RectTransform)go.transform;
        ghost.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, ghostAlpha);

        bottle = kind == LiquidKind.Water ? bottleWater : bottleSyrup;
        Move(screenPos);
        return true;
    }

    public void Move(Vector2 screenPos)
    {
        if (!IsDragging) return;
        Vector2 aim = screenPos + fingerOffset;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(ghostRoot, aim, uiCam, out Vector2 local))
            ghost.localPosition = local;

        overHud = hudRect != null && RectTransformUtility.RectangleContainsScreenPoint(hudRect, aim, uiCam);
        ghost.gameObject.SetActive(overHud);

        if (overHud)
        {
            HideBottle();
            SetHover(null);
            return;
        }
        UpdateBottle(aim);
    }

    public void End(Vector2 screenPos)
    {
        if (!IsDragging) return;
        Move(screenPos);

        bool inWorld = !overHud;
        VesselActor target = hover;
        VesselActor usedBottle = bottle != null && bottle.gameObject.activeSelf ? bottle : null;
        LiquidKind k = kind;

        Cleanup();       // ghost 2D dan indikator target; botol 3D tetap sampai dipakai atau dimatikan

        if (!inWorld || usedBottle == null)
        {
            if (usedBottle != null) DismissBottle(usedBottle);
            Cancelled?.Invoke();
            return;
        }

        var drop = new LiquidDrop { kind = k, target = target, bottle = usedBottle, releaseWorld = usedBottle.transform.position };
        if (Dropped == null) DismissBottle(usedBottle);
        else Dropped.Invoke(drop);
    }

    public void Cancel()
    {
        if (!IsDragging) return;
        HideBottle();
        Cleanup();
        Cancelled?.Invoke();
    }

    public void DismissBottle(VesselActor b)
    {
        if (b) b.gameObject.SetActive(false);
    }

    void Cleanup()
    {
        SetHover(null);
        if (ghost) Destroy(ghost.gameObject);
        ghost = null;
        IsDragging = false;
        bottle = null;
    }

    void HideBottle()
    {
        if (bottle && bottle.gameObject.activeSelf) bottle.gameObject.SetActive(false);
    }

    void UpdateBottle(Vector2 aim)
    {
        Camera cam = WorldCam;
        if (cam == null || bottle == null) return;

        Ray ray = cam.ScreenPointToRay(aim);
        float y = heightReference != null && heightReference.Vessel != null
            ? heightReference.Vessel.RimCenterWorld.y + hoverHeight
            : 0f;
        var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));

        if (plane.Raycast(ray, out float dist))
        {
            if (!bottle.gameObject.activeSelf)
            {
                bottle.gameObject.SetActive(true);                 // Awake dan OnEnable membangun cairan
                LiquidVessel v = bottle.Vessel;
                if (v != null)
                {
                    v.ScaleVisible = false;                        // botol tanpa garis penyebut
                    v.Fill = 1f;
                    v.LiquidColor = BottleColor != null ? BottleColor(kind) : Color.white;
                }
            }
            bottle.transform.SetPositionAndRotation(ray.GetPoint(dist), Quaternion.identity);
        }

        SetHover(FindActor(ray));
    }

    VesselActor FindActor(Ray ray)
    {
        int n = Physics.RaycastNonAlloc(ray, hits, 10f, pourMask, QueryTriggerInteraction.Collide);
        VesselActor best = null;
        float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var actor = hits[i].collider.GetComponentInParent<VesselActor>();
            if (actor == null || actor == bottle || hits[i].distance >= bestD) continue;
            best = actor;
            bestD = hits[i].distance;
        }
        return best;
    }

    void SetHover(VesselActor actor)
    {
        if (actor == hover) return;
        if (hover) hover.SetTargeted(false);
        hover = actor;
        if (hover) hover.SetTargeted(IsValidTarget == null || IsValidTarget(kind, hover));
    }
}