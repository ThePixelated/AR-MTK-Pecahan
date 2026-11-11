using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class MultiPionTexts
{
    public const string Incomplete = "Taruh semua hewan di garis bilangan dulu ya.";
}

// Tampilan soal MultiPion pada layout baru: tombol tokoh di kiri (di-instantiate otomatis), drag ke kanan jadi hewan yang
// snap ke garis, banyak pion pada satu garis, highlight, dan outline benar/salah. Semua keputusan ada di MultiPionBoard.
//
// Aturan partisi: garis hanya berganti partisi pada TAP atau AWAL DRAG (tombol tokoh atau pion). Pointer yang sekadar
// lewat di atas pion tidak mengubah apa pun. Posisi pion disimpan sebagai nilai, jadi tidak bergeser saat garis berganti.
public class MultiPionPresenter : MonoBehaviour
{
    [Header("Garis (layout baru)")]
    [SerializeField] NumberLineView lineView;               // NumberLineView di GarisBilanganSection layout baru
    [SerializeField] RectTransform pionLayer;               // induk visual pion; sibling DI ATAS garis; titik pivotnya = tinggi pion berdiri
    [SerializeField] RectTransform dropZone;                // area "sisi kanan": pointer di dalamnya = hewan menempel ke garis
    [SerializeField] RectTransform dragRoot;                // induk ghost drag; kosong = Canvas root

    [Header("Tombol tokoh (sisi kiri)")]
    [SerializeField] RectTransform tokenParent;             // ButtonIconsGrid (GridLayoutGroup)
    [SerializeField] PionTokenButton tokenPrefab;

    [Header("Hewan")]
    [SerializeField] AnimalChooserPanel animalSource;       // sprite pion ke-N = hewan ke-N di panel ini (panel boleh nonaktif)
    [SerializeField] Sprite[] fallbackSprites;              // dipakai kalau animalSource kosong
    [SerializeField] RectTransform pionPrefab;              // opsional: prefab pion (Image); kosong = dibuat lewat kode
    [SerializeField] Vector2 pionSize = new Vector2(110f, 110f);
    [SerializeField] float restY = 0f;                      // koreksi tinggi pion terhadap pivot Pion Layer
    [SerializeField] float stackOffset = 30f;               // pion bernilai sama ditumpuk naik sebesar ini (0 = bertumpuk persis)
    [SerializeField] bool useDragThreshold = true;          // true: tap kecil tidak dianggap drag (disarankan)
    [SerializeField, Range(0.2f, 1f)] float ghostAlpha = 0.85f;
    [SerializeField] PionFeedbackStyle style = new PionFeedbackStyle();

    MultiPionBoard board;
    PionTokenButton[] tokens = new PionTokenButton[0];
    PionView[] pions = new PionView[0];
    int[] lastTick = new int[0];
    Sprite[] animals = new Sprite[0];

    // drag tombol tokoh
    PionTokenButton dragToken;
    PionTokenButton ghostToken;
    Image ghostAnimal;

    // drag pion di garis
    PionView dragPion;
    bool pionOutside;
    float grabOffset;

    public bool IsShowing => board != null;

    // ---------------- siklus ----------------
    void OnDisable() => CancelDrags();

    void OnDestroy() => Clear();

    public void Show(MultiPionBoard newBoard)
    {
        Clear();
        if (newBoard == null) return;

        if (lineView == null || pionLayer == null || tokenParent == null || tokenPrefab == null)
        {
            Debug.LogError($"[{name}] MultiPionPresenter: Line View, Pion Layer, Token Parent, dan Token Prefab harus diisi.", this);
            return;
        }

        board = newBoard;
        animals = CollectSprites();
        if (animals.Length == 0)
            Debug.LogWarning($"[{name}] MultiPionPresenter: tidak ada sprite hewan (isi Animal Source atau Fallback Sprites).", this);
        else if (animals.Length < board.Count)
            Debug.LogWarning($"[{name}] MultiPionPresenter: {board.Count} pion tapi hanya {animals.Length} hewan; hewan dipakai ulang berurutan.", this);

        tokens = new PionTokenButton[board.Count];
        pions = new PionView[board.Count];
        lastTick = new int[board.Count];

        for (int i = 0; i < board.Count; i++)
        {
            Sprite sprite = SpriteFor(i);
            tokens[i] = CreateToken(i, sprite);
            pions[i] = CreatePion(i, sprite);
            lastTick[i] = -1;
        }

        board.Changed += Refresh;
        Refresh();
    }

    public void Clear()
    {
        CancelDrags();
        if (board != null) board.Changed -= Refresh;
        board = null;

        foreach (PionTokenButton t in tokens) if (t) Destroy(t.gameObject);
        foreach (PionView p in pions) if (p) Destroy(p.gameObject);
        tokens = new PionTokenButton[0];
        pions = new PionView[0];
        lastTick = new int[0];
    }

    // Hasil CEK: satu indikator per pion (outline benar atau salah)
    public void ShowIndicators(NumberLineIndicator[] indicators)
    {
        if (indicators == null) return;
        for (int i = 0; i < pions.Length && i < indicators.Length; i++)
        {
            PionMark mark = indicators[i] == NumberLineIndicator.Correct ? PionMark.Correct
                          : indicators[i] == NumberLineIndicator.Wrong ? PionMark.Wrong
                          : PionMark.None;
            if (pions[i]) pions[i].SetMark(mark);
        }
    }

    public void ClearIndicators()
    {
        foreach (PionView p in pions) if (p) p.SetMark(PionMark.None);
    }

    // ---------------- pembuatan ----------------
    Sprite[] CollectSprites()
    {
        var list = new List<Sprite>();
        if (animalSource != null)
        {
            foreach (AnimalButton b in animalSource.GetComponentsInChildren<AnimalButton>(true))
                if (b.PionSprite != null) list.Add(b.PionSprite);
        }
        if (list.Count == 0 && fallbackSprites != null)
            foreach (Sprite s in fallbackSprites) if (s != null) list.Add(s);
        return list.ToArray();
    }

    Sprite SpriteFor(int index) => animals.Length == 0 ? null : animals[index % animals.Length];

    PionTokenButton CreateToken(int index, Sprite sprite)
    {
        PionTokenButton t = Instantiate(tokenPrefab, tokenParent);
        t.gameObject.SetActive(true);
        t.name = $"Token {index + 1}";
        t.Setup(index, board.Label(index), sprite);
        t.Tapped += OnTokenTapped;
        t.DragBegan += OnTokenDragBegan;
        t.Dragged += OnTokenDragged;
        t.DragEnded += OnTokenDragEnded;
        return t;
    }

    PionView CreatePion(int index, Sprite sprite)
    {
        GameObject go;
        if (pionPrefab != null)
        {
            go = Instantiate(pionPrefab, pionLayer).gameObject;
        }
        else
        {
            go = new GameObject($"Pion {index + 1}", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(pionLayer, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = pionSize;
        }
        go.name = $"Pion {index + 1}";

        PionView v = go.GetComponent<PionView>();
        if (v == null) v = go.AddComponent<PionView>();
        v.Setup(index, sprite, style, useDragThreshold);
        v.Tapped += OnPionTapped;
        v.DragBegan += OnPionDragBegan;
        v.Dragged += OnPionDragged;
        v.DragEnded += OnPionDragEnded;
        go.SetActive(false);
        return v;
    }

    // ---------------- tampilan mengikuti papan ----------------
    void Refresh()
    {
        if (board == null) return;

        int selected = board.SelectedIndex;
        for (int i = 0; i < pions.Length; i++)
        {
            bool placed = board.IsPlaced(i);
            PionView p = pions[i];

            if (p.gameObject.activeSelf != placed) p.gameObject.SetActive(placed);
            if (!placed)
            {
                p.ResetFeedback();
                lastTick[i] = -1;
            }
            else
            {
                int tick = board.TickOf(i);
                if (lastTick[i] != -1 && lastTick[i] != tick) p.SetMark(PionMark.None);     // digeser setelah CEK: tanda lama tidak berlaku
                lastTick[i] = tick;
                p.SetSelected(i == selected);
            }

            tokens[i].SetSelected(i == selected);
            tokens[i].SetPlaced(placed);
        }

        if (selected >= 0 && board.IsPlaced(selected)) pions[selected].transform.SetAsLastSibling();      // yang dipilih tergambar di atas
    }

    void LateUpdate()
    {
        if (board == null || lineView == null || pionLayer == null || !lineView.IsLayoutReady) return;

        for (int i = 0; i < pions.Length; i++)
        {
            if (!board.IsPlaced(i)) continue;

            float x = lineView.NormalizedToLocalX(board.NormalizedOf(i), pionLayer);
            float y = restY + StackIndex(i) * stackOffset + pions[i].JumpOffset;
            pions[i].Rect.localPosition = new Vector3(x, y, 0f);
        }
    }

    // Pion bernilai sama ditumpuk naik, berurutan menurut nomor pion
    int StackIndex(int i)
    {
        int n = 0;
        float v = board.NormalizedOf(i);
        for (int j = 0; j < i; j++)
            if (board.IsPlaced(j) && Mathf.Abs(board.NormalizedOf(j) - v) < 0.0001f) n++;
        return n;
    }

    bool InZone(PointerEventData e)
    {
        RectTransform zone = dropZone != null ? dropZone : (lineView != null ? lineView.Line : null);
        return zone != null && RectTransformUtility.RectangleContainsScreenPoint(zone, e.position, e.pressEventCamera);
    }

    // ---------------- tombol tokoh ----------------
    void OnTokenTapped(PionTokenButton t)
    {
        if (board == null) return;
        board.Select(t.Index);
        if (board.IsPlaced(t.Index)) pions[t.Index].Highlight();
    }

    void OnTokenDragBegan(PionTokenButton t, PointerEventData e)
    {
        if (board == null) return;
        board.Select(t.Index);

        // pionnya sudah di garis: tidak spawn lagi, cukup highlight
        if (board.IsPlaced(t.Index))
        {
            pions[t.Index].Highlight();
            return;
        }

        dragToken = t;
        CreateGhost(t);
        OnTokenDragged(t, e);
    }

    void OnTokenDragged(PionTokenButton t, PointerEventData e)
    {
        if (dragToken != t || ghostToken == null) return;

        bool inZone = InZone(e);
        ghostToken.gameObject.SetActive(!inZone);
        ghostAnimal.gameObject.SetActive(inZone);

        if (!inZone)
        {
            RectTransform root = GhostRoot();
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(root, e.position, e.pressEventCamera, out Vector3 world))
                ghostToken.transform.position = world;
            return;
        }

        // di sisi kanan: berubah jadi hewan dan snap ke tick terdekat (partisi milik pion ini)
        if (!lineView.TryPointerToNormalized(e.position, e.pressEventCamera, out float n)) return;

        var config = new NumberLineConfig(board.Length, board.Partisi(t.Index));
        float snapped = config.TickToNormalized(config.NearestTick(n));
        float x = lineView.NormalizedToLocalX(snapped, pionLayer);
        ghostAnimal.transform.position = pionLayer.TransformPoint(new Vector3(x, restY, 0f));
    }

    void OnTokenDragEnded(PionTokenButton t, PointerEventData e)
    {
        if (dragToken != t) return;

        float n = 0f;
        bool ok = InZone(e) && lineView.TryPointerToNormalized(e.position, e.pressEventCamera, out n);

        DestroyGhost();
        dragToken = null;
        if (ok && board != null) board.PlaceAtNormalized(t.Index, n);       // Changed -> Refresh memunculkan pion
    }

    // ---------------- pion di garis ----------------
    void OnPionTapped(PionView p)
    {
        if (board != null) board.Select(p.Index);        // tap hanya mengganti partisi garis
    }

    void OnPionDragBegan(PionView p, PointerEventData e)
    {
        if (board == null) return;
        board.Select(p.Index);

        dragPion = p;
        pionOutside = false;
        grabOffset = 0f;
        if (lineView.TryPointerToNormalized(e.position, e.pressEventCamera, out float n))
            grabOffset = board.NormalizedOf(p.Index) - n;      // pion tidak melompat ke pointer saat mulai di-drag
    }

    void OnPionDragged(PionView p, PointerEventData e)
    {
        if (dragPion != p || board == null) return;

        bool inZone = InZone(e);
        pionOutside = !inZone;
        p.SetFaded(pionOutside);
        if (!inZone) return;

        if (lineView.TryPointerToNormalized(e.position, e.pressEventCamera, out float n))
            board.PlaceAtNormalized(p.Index, Mathf.Clamp01(n + grabOffset));
    }

    void OnPionDragEnded(PionView p, PointerEventData e)
    {
        if (dragPion != p) return;

        dragPion = null;
        p.SetFaded(false);
        bool remove = pionOutside;
        pionOutside = false;
        if (remove && board != null) board.Remove(p.Index);      // dilepas di luar area: kembali ke tombolnya
    }

    // ---------------- ghost ----------------
    RectTransform GhostRoot()
    {
        if (dragRoot != null) return dragRoot;
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? (RectTransform)canvas.rootCanvas.transform : (RectTransform)transform;
    }

    void CreateGhost(PionTokenButton source)
    {
        DestroyGhost();
        RectTransform root = GhostRoot();

        // tampilan tombol: salinan prefab tombol tokoh
        ghostToken = Instantiate(tokenPrefab, root);
        ghostToken.gameObject.SetActive(true);
        ghostToken.Setup(source.Index, board.Label(source.Index), SpriteFor(source.Index));
        ghostToken.MakeInert();
        foreach (Graphic g in ghostToken.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        ghostToken.transform.SetAsLastSibling();

        // tampilan hewan: dipakai saat pointer sudah di sisi kanan
        var go = new GameObject("GhostAnimal", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root, false);
        ((RectTransform)go.transform).sizeDelta = pionSize;
        ghostAnimal = go.GetComponent<Image>();
        ghostAnimal.sprite = SpriteFor(source.Index);
        ghostAnimal.preserveAspect = true;
        ghostAnimal.raycastTarget = false;
        ghostAnimal.color = new Color(1f, 1f, 1f, ghostAlpha);
        go.SetActive(false);
        go.transform.SetAsLastSibling();
    }

    void DestroyGhost()
    {
        if (ghostToken) Destroy(ghostToken.gameObject);
        if (ghostAnimal) Destroy(ghostAnimal.gameObject);
        ghostToken = null;
        ghostAnimal = null;
    }

    void CancelDrags()
    {
        DestroyGhost();
        dragToken = null;
        if (dragPion != null)
        {
            dragPion.SetFaded(false);
            dragPion = null;
        }
        pionOutside = false;
    }
}
