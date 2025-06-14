using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PuzzlePiece : MonoBehaviour
{
    public PuzzleGroup currentGroup;
    public Vector3 offsetFromMouse;
    public bool isDragging = false;

    private void OnMouseDown()
    {
        isDragging = true;
        offsetFromMouse = transform.position - GetMouseWorldPos();

        // 如果属于组合，先移除
        if (currentGroup != null)
        {
            currentGroup.RemovePiece(this);
            currentGroup = null;

            // ✅ 拼图块被拖出组合后，刷新路径段
            var mover = FindObjectOfType<PathMover>();
            if (mover != null)
            {
                mover.RefreshPaths();
            }
        }
    }

    private void OnMouseDrag()
    {
        if (isDragging)
        {
            transform.position = GetMouseWorldPos() + offsetFromMouse;
        }
    }

    private void OnMouseUp()
    {
        isDragging = false;

        PuzzlePiece[] allPieces = FindObjectsOfType<PuzzlePiece>();
        foreach (var other in allPieces)
        {
            if (other == this) continue;

            if (TrySnapTo(other))
            {
                CombineWith(other);
                return;
            }
        }

        // ✅ 即使没有吸附成功，也刷新路径（位置可能发生变化）
        var mover = FindObjectOfType<PathMover>();
        if (mover != null)
        {
            mover.RefreshPaths();
        }
    }

    bool TrySnapTo(PuzzlePiece other)
    {
        float snapTolerance = 0.2f;

        Vector3 mySize = GetComponent<Renderer>().bounds.size;
        Vector3 otherSize = other.GetComponent<Renderer>().bounds.size;

        Vector3 myPos = transform.position;
        Vector3 otherPos = other.transform.position;

        Vector3 offset = myPos - otherPos;

        float expectedX = (mySize.x + otherSize.x) / 2f;
        float expectedY = (mySize.y + otherSize.y) / 2f;

        // 左右吸附
        if (Mathf.Abs(Mathf.Abs(offset.x) - expectedX) < snapTolerance && Mathf.Abs(offset.y) < snapTolerance)
        {
            float newX = otherPos.x + Mathf.Sign(offset.x) * expectedX;
            transform.position = new Vector3(newX, otherPos.y, myPos.z);
            return true;
        }

        // 上下吸附
        if (Mathf.Abs(Mathf.Abs(offset.y) - expectedY) < snapTolerance && Mathf.Abs(offset.x) < snapTolerance)
        {
            float newY = otherPos.y + Mathf.Sign(offset.y) * expectedY;
            transform.position = new Vector3(otherPos.x, newY, myPos.z);
            return true;
        }

        return false;
    }

    void CombineWith(PuzzlePiece other)
    {
        PuzzleGroup group = other.currentGroup;

        if (group == null)
        {
            GameObject newGroup = new GameObject("PuzzleGroup");
            group = newGroup.AddComponent<PuzzleGroup>();
            group.AddPiece(other);
        }

        group.AddPiece(this);

        // ✅ 合并后刷新路径段
        var mover = FindObjectOfType<PathMover>();
        if (mover != null)
        {
            mover.RefreshPaths();
        }
    }

    Vector3 GetMouseWorldPos()
    {
        Vector3 screenPos = Input.mousePosition;
        screenPos.z = Camera.main.WorldToScreenPoint(transform.position).z;
        return Camera.main.ScreenToWorldPoint(screenPos);
    }
}