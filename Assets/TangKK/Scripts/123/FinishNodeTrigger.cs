using UnityEngine;

/// <summary>
/// 终点节点触发器，负责检测玩家进入/离开
/// </summary>
public class FinishNodeTrigger : MonoBehaviour
{
    private LevelCompletionManager manager;
    private PathNode thisNode;
    private bool playerInside = false;

    /// <summary>
    /// 初始化触发器
    /// </summary>
    public void Initialize(LevelCompletionManager completionManager, PathNode node)
    {
        manager = completionManager;
        thisNode = node;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (manager == null || thisNode == null) return;

        // ✅ 检查玩家与终点节点是否在同一拼图组
        if (!IsSameGroup(other.gameObject))
        {
            return; // 不同组，不触发
        }

        playerInside = true;
        manager.OnPlayerEnterFinish(thisNode);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (manager == null || thisNode == null) return;
        if (!playerInside) return;

        // ✅ 检查玩家与终点节点是否在同一拼图组
        if (!IsSameGroup(other.gameObject))
        {
            return; // 不同组，不处理
        }

        playerInside = false;
        manager.OnPlayerExitFinish(thisNode);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        // 确保玩家持续在触发区域内
        if (!other.CompareTag("Player")) return;
        if (manager == null || thisNode == null) return;

        // ✅ 检查玩家与终点节点是否在同一拼图组
        if (!IsSameGroup(other.gameObject))
        {
            // 如果玩家之前在里面但现在不同组了，需要触发退出
            if (playerInside)
            {
                playerInside = false;
                manager.OnPlayerExitFinish(thisNode);
            }
            return;
        }

        if (!playerInside)
        {
            playerInside = true;
            manager.OnPlayerEnterFinish(thisNode);
        }
    }

    /// <summary>
    /// 判断玩家是否与终点节点在同一拼图组
    /// </summary>
    bool IsSameGroup(GameObject player)
    {
        var playerPiece = player.GetComponentInParent<PuzzlePiece>();
        if (playerPiece == null || thisNode == null) 
        {
            Debug.LogWarning($"[FinishNodeTrigger] 玩家或终点节点没有拼图组信息");
            return false;
        }

        bool sameGroup = playerPiece.GroupID == thisNode.GroupID;
        
        if (manager.showDebugInfo)
        {
            Debug.Log($"[FinishNodeTrigger] 组检查: 玩家组ID {playerPiece.GroupID}, 终点组ID {thisNode.GroupID}, 同组: {sameGroup}");
        }

        return sameGroup;
    }
}