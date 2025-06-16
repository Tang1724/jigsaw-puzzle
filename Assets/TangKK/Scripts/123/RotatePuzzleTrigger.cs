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

            if (piece.currentGroup != null)
            {
                piece.currentGroup.RotateGroup(rotationAngle);
            }
            else
            {
                piece.RotateSelf(rotationAngle);
            }

            RefreshAllPaths();

            hasTriggered = true;
        }
        else
        {
            Debug.Log("[RotateTrigger] 玩家未穿越触发器中心，忽略旋转");
        }

        playerInside = false;
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