using UnityEngine;

public enum DpzColliderKind { Outer, Inner, Block, UpperBlock }

public struct DpzHit
{
    public DpzColliderKind kind;
    public int blockIndex;       // hanya untuk Block
    public Vector3 point;
}

public class DpzCollider : MonoBehaviour
{
    public DpzView owner;
    public DpzColliderKind kind;
    public int blockIndex = -1;
}