using UnityEngine;

public class Dabu_Puzzle : MonoBehaviour
{
    public bool isHeld = false;
    private Vector3 offset;
    private Camera cam;
    private Dabu_PuzzleGraph graph;

    // 记录吸附信息
    private Transform snappedTarget = null;
    private Vector2 snappedDirection = Vector2.zero;

    private void Start()
    {
        cam = Camera.main;
        graph = GetComponent<Dabu_PuzzleGraph>();
        Dabu_PuzzleManager.Instance?.RegisterPuzzle(graph);
    }

    private void OnMouseDown()
    {
        isHeld = true;
        offset = transform.position - GetMouseWorldPos();

        var player = FindObjectOfType<Dabu_PlayerController>();
        if (player != null && player.IsOnThisPuzzle(transform))
        {
            player.FollowPuzzle(transform);
        }
    }

    private void OnMouseUp()
    {
        isHeld = false;

        // 检查是否需要断开连接
        CheckAndDisconnectIfNeeded();

        TrySnapToNeighbor();

        // 停止玩家跟随
        var player = FindObjectOfType<Dabu_PlayerController>();
        if (player != null)
        {
            player.StopFollowingPuzzle();
        }

        // 重建图并更新玩家位置
        Dabu_PuzzleManager.Instance?.RebuildMergedGraph();
        if (player != null)
        {
            player.UpdatePositionOnGraph();
        }
    }

    private void CheckAndDisconnectIfNeeded()
    {
        if (snappedTarget == null) return;

        float tileSize = 2.5f;
        float disconnectThreshold = 0.1f; // 如果距离超过这个阈值，就断开连接

        Vector2 myPos = transform.position;
        Vector2 targetPos = snappedTarget.position;
        Vector2 expectedPos = targetPos + (Vector2)(snappedDirection * tileSize);
        float distance = Vector2.Distance(myPos, expectedPos);

        if (distance > disconnectThreshold)
        {
            // 断开连接
            snappedTarget = null;
            snappedDirection = Vector2.zero;
        }
    }

    private void Update()
    {
        if (isHeld)
        {
            transform.position = GetMouseWorldPos() + offset;
            
            // 重建图，让其他玩家能够在更新的路径上移动
            Dabu_PuzzleManager.Instance?.RebuildMergedGraph();
        }
    }

    private Vector3 GetMouseWorldPos()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 0f;
        return cam.ScreenToWorldPoint(mousePos);
    }

    private void TrySnapToNeighbor()
    {
        float tileSize = 2.5f;
        float snapThreshold = 0.4f;

        Vector2 myPos = transform.position;

        foreach (var other in FindObjectsOfType<Dabu_Puzzle>())
        {
            if (other == this) continue;

            Vector2 otherPos = other.transform.position;
            Vector2 offset = myPos - otherPos;

            // 水平吸附
            if (Mathf.Abs(offset.y) < snapThreshold)
            {
                if (Mathf.Abs(offset.x - tileSize) < snapThreshold)
                {
                    transform.position = otherPos + Vector2.right * tileSize;
                    snappedTarget = other.transform;
                    snappedDirection = Vector2.right;
                    return;
                }
                if (Mathf.Abs(offset.x + tileSize) < snapThreshold)
                {
                    transform.position = otherPos - Vector2.right * tileSize;
                    snappedTarget = other.transform;
                    snappedDirection = Vector2.left;
                    return;
                }
            }

            // 垂直吸附
            if (Mathf.Abs(offset.x) < snapThreshold)
            {
                if (Mathf.Abs(offset.y - tileSize) < snapThreshold)
                {
                    transform.position = otherPos + Vector2.up * tileSize;
                    snappedTarget = other.transform;
                    snappedDirection = Vector2.up;
                    return;
                }
                if (Mathf.Abs(offset.y + tileSize) < snapThreshold)
                {
                    transform.position = otherPos - Vector2.up * tileSize;
                    snappedTarget = other.transform;
                    snappedDirection = Vector2.down;
                    return;
                }
            }
        }

        // 如果没有吸附成功，清空状态
        snappedTarget = null;
        snappedDirection = Vector2.zero;
    }
}
