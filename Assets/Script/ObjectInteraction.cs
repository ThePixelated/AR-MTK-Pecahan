using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class ObjectInteraction : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private Transform pivotRest;
    //[SerializeField] private GameObject gelasGabunganA;
    //[SerializeField] private GameObject gelasGabunganB;

    //private Transform _pourPivotGBA; // first child of gelasGabunganX
    //private Transform _pourPivotGBB;
    private GameObject _gObjGelas;
    private Animator _anim;

    private Vector3 _mOffset;
    private float _mZCoor;
    private bool _flagColliderHit = false;

    private Coroutine _coroutine;

    private void Awake()
    {
        //_gObjGelas = new GameObject();
        _anim = GetComponent<Animator>();

        _anim.enabled = false;
    }

    private void OnMouseDown()
    {
        _mZCoor = cam.WorldToScreenPoint(gameObject.transform.position).z;

        _mOffset = gameObject.transform.position - GetMouseWorldPos();
    }

    private void OnMouseDrag()
    {
        transform.position = GetMouseWorldPos() + _mOffset;
    }

    private void OnMouseUp()
    {
        if (_flagColliderHit && !ObjectManager.IsPouring)
        {
            _anim.enabled = true;
            ObjectManager.SetPourAction(true);
            Vector3 pivotNuang = _gObjGelas.transform.GetChild(0).GetComponent<Transform>().position;
            _coroutine = StartCoroutine(PlaysPourAnimation());

            _flagColliderHit = false;
            //_gObjGelas = new GameObject();
        }
        else
        {
            transform.position = pivotRest.position;
            transform.rotation = pivotRest.rotation;
        }
    }

    private IEnumerator PlaysPourAnimation()
    {
        _anim.SetTrigger("Nuang");

        //var m_CurrentClipInfo = this._anim.GetCurrentAnimatorClipInfo(0);
        ////Access the current length of the clip
        //var m_CurrentClipLength = m_CurrentClipInfo[0].clip.length;

        yield return new WaitForSeconds(4.7f);

        _anim.enabled = false;

        ObjectManager.SetPourAction(false);
        transform.position = pivotRest.position;
        transform.rotation = pivotRest.rotation;

        _coroutine = null;
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePos = Input.mousePosition;

        mousePos.z = _mZCoor;

        return cam.ScreenToWorldPoint(mousePos);
    }

    private void Update()
    {
        if (Input.GetMouseButton(0))
        {
            //RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, 100, 7);

            // urutin dari yang paling deket, penting kalau nanti ada G3a & G3b sepanjang ray yang sama
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.collider.gameObject.CompareTag("AR1G3"))
                {
                    print("Hit G3: " + hit.collider.gameObject.name);
                    _flagColliderHit = true;
                    GameObject currGObj = hit.collider.gameObject;

                    if (currGObj != _gObjGelas)
                    {
                        _gObjGelas = currGObj;
                    }



                    //ObjectManager.ColliderHit(hit.collider.gameObject);
                    break; // ambil yang paling deket duluan
                }
            }
        }
    }
}
