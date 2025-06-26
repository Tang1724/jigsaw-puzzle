using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RotatePuzzleTrigger : MonoBehaviour
{
    [Header("旋转角度")]
    public float rotationAngle = 90f;

    [Header("是否只触发一次")]
    public bool triggerOnce = false;

    private bool hasTriggered = false;
    private bool playerInside = false;
    private Vector3 enterPosition;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (triggerOnce && hasTriggered) return;

        enterPosition = other.transform.position;
        playerInside = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (!playerInside || (triggerOnce && hasTriggered)) return;

        Vector3 exitPosition = other.transform.position;
        Vector3 center = GetComponent<Collider2D>().bounds.center;

        Vector3 toEnter = enterPosition - center;
        Vector3 toExit = exitPosition - center;

        float dot = Vector3.Dot(toEnter.normalized, toExit.normalized);
        bool crossedThrough = dot < 0f;

        if (crossedThrough)
        {
            Debug.Log("[RotateTrigger] 玩家完整穿过触发器，触发旋转");

            var player = other.gameObject;
            var piece = player.GetComponentInParent<PuzzlePiece>();

            if (piece == null)
            {
                Debug.LogWarning("[RotateTrigger] 无法从玩家找到 PuzzlePiece");
                return;
            }

            // ✅ 执行旋转
            if (piece.currentGroup != null)
            {
                foreach (var p in piece.currentGroup.pieces)
                {
                    p.RotateSelf(rotationAngle);
                }
            }
            else
            {
                piece.RotateSelf(rotationAngle);
            }

            // ✅ 在旋转后强制重新组织连接关系并刷新路径
            ReorganizeAndRefreshAll();

            hasTriggered = true;
        }
        else
        {
            Debug.Log("[RotateTrigger] 玩家未穿越触发器中心，忽略旋转");
        }

        playerInside = false;
    }

    /// <summary>
    /// ✅ 旋转后重新组织拼图组，并刷新路径（确保逻辑正确）
    /// </summary>
    private void ReorganizeAndRefreshAll()
    {
        PuzzlePiece[] allPieces = FindObjectsOfType<PuzzlePiece>();
        foreach (var piece in allPieces)
        {
            piece.ReorganizeConnectedPuzzles(); // 重新组织组
            piece.RefreshPath();                // 刷新路径
        }

        RefreshAllPaths();
    }

    /// <summary>
    /// 刷新所有路径显示和路径缓存
    /// </summary>
    private void RefreshAllPaths()
    {
        foreach (var node in FindObjectsOfType<PathNode>())
        {
            node.RefreshPathLines();
        }

        var mover = FindObjectOfType<PathMover>();
        if (mover != null)
        {
            mover.RefreshPaths();
        }

        Debug.Log("[RotateTrigger] 已刷新所有路径线段和路径缓存");
    }
}