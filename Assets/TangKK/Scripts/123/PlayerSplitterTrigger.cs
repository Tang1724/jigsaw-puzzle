using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class PlayerSplitterTrigger : MonoBehaviour
{
    [Header("目标节点 A 和 B")]
    public PathNode spawnNodeA;
    public PathNode spawnNodeB;

    [Header("玩家预制体（必须包含 PathMover + PuzzlePiece）")]
    public GameObject playerPrefab;

    [Header("调试选项")]
    public bool debugMode = true;
    public bool showDirectionArrow = true;

    [Header("🔒 组权限设置")]
    public bool requireSameGroup = true; // 是否要求同组才能触发
    public bool showGroupInfo = true;    // 是否显示组信息用于调试

    private Dictionary<GameObject, Vector3> playersInTrigger = new Dictionary<GameObject, Vector3>();

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        GameObject player = other.gameObject;
        playersInTrigger[player] = player.transform.position;

        if (debugMode)
        {
            Debug.Log($"[PlayerSplitter] 🎯 玩家 {player.name} 进入触发器，位置: {player.transform.position}");
            
            if (showGroupInfo)
            {
                LogGroupInformation(player);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        GameObject player = other.gameObject;
        if (!playersInTrigger.ContainsKey(player)) return;

        Vector3 enterPosition = playersInTrigger[player];
        Vector3 exitPosition = player.transform.position;
        Vector3 center = GetComponent<Collider2D>().bounds.center;

        Vector3 toEnter = enterPosition - center;
        Vector3 toExit = exitPosition - center;

        float dot = Vector3.Dot(toEnter.normalized, toExit.normalized);
        bool passedThrough = dot < 0f;

        if (passedThrough)
        {
            if (debugMode)
            {
                Debug.Log($"[PlayerSplitter] ✅ 玩家 {player.name} 完整穿过触发器");
            }

            // 🔒 检查组权限
            if (requireSameGroup && !CheckGroupPermission(player))
            {
                if (debugMode)
                {
                    Debug.Log($"[PlayerSplitter] 🚫 玩家 {player.name} 不属于同一组，拒绝分裂触发");
                }
                playersInTrigger.Remove(player);
                return;
            }

            if (debugMode)
            {
                Debug.Log($"[PlayerSplitter] 🎉 触发分裂！玩家：{player.name}");
            }

            SplitPlayer(player);
        }
        else
        {
            if (debugMode)
            {
                Debug.Log($"[PlayerSplitter] ❌ 玩家 {player.name} 未完整穿过触发器，忽略");
            }
        }

        playersInTrigger.Remove(player);
    }

    /// <summary>
    /// 🔒 检查玩家是否与触发器属于同一个拼图组
    /// </summary>
    private bool CheckGroupPermission(GameObject player)
    {
        // 获取玩家所在的拼图组信息
        var playerPiece = player.GetComponentInParent<PuzzlePiece>();
        if (playerPiece == null)
        {
            if (debugMode)
            {
                Debug.LogWarning($"[组权限检查] 玩家 {player.name} 没有关联的 PuzzlePiece");
            }
            return false; // 没有拼图组信息的玩家不能触发
        }

        // 获取玩家所在的组
        var playerGroup = playerPiece.currentGroup;
        int playerGroupID = playerPiece.GroupID;

        // 获取触发器所在的拼图组信息
        var triggerPiece = GetComponentInParent<PuzzlePiece>();
        if (triggerPiece == null)
        {
            if (debugMode)
            {
                Debug.LogWarning($"[组权限检查] 触发器 {name} 没有关联的 PuzzlePiece");
            }
            return false; // 触发器不在拼图上，不允许触发
        }

        var triggerGroup = triggerPiece.currentGroup;
        int triggerGroupID = triggerPiece.GroupID;

        // 🔍 详细的组检查逻辑
        bool sameGroup = false;

        // 方法1：通过组ID比较
        if (playerGroupID == triggerGroupID && playerGroupID != -1)
        {
            sameGroup = true;
            if (debugMode)
            {
                Debug.Log($"[组权限检查] ✅ 组ID匹配：玩家组 {playerGroupID} == 触发器组 {triggerGroupID}");
            }
        }
        // 方法2：通过组对象比较
        else if (playerGroup != null && triggerGroup != null && playerGroup == triggerGroup)
        {
            sameGroup = true;
            if (debugMode)
            {
                Debug.Log($"[组权限检查] ✅ 组对象匹配：玩家组 {playerGroup.name} == 触发器组 {triggerGroup.name}");
            }
        }
        // 方法3：检查是否物理连接（作为备用方案）
        else if (playerPiece.IsPhysicallyConnected(playerPiece, triggerPiece))
        {
            sameGroup = true;
            if (debugMode)
            {
                Debug.Log($"[组权限检查] ✅ 物理连接：玩家拼图与触发器拼图物理连接");
            }
        }

        if (!sameGroup && debugMode)
        {
            Debug.Log($"[组权限检查] ❌ 组不匹配：玩家组 {playerGroupID}({playerGroup?.name}) ≠ 触发器组 {triggerGroupID}({triggerGroup?.name})");
        }

        return sameGroup;
    }

    /// <summary>
    /// 🔍 记录组信息用于调试
    /// </summary>
    private void LogGroupInformation(GameObject player)
    {
        var playerPiece = player.GetComponentInParent<PuzzlePiece>();
        var triggerPiece = GetComponentInParent<PuzzlePiece>();

        Debug.Log("=== 组信息调试 ===");
        
        if (playerPiece != null)
        {
            Debug.Log($"👤 玩家 {player.name}:");
            Debug.Log($"   - 拼图: {playerPiece.name}");
            Debug.Log($"   - 组ID: {playerPiece.GroupID}");
            Debug.Log($"   - 初始组ID: {playerPiece.initialGroupID}");
            Debug.Log($"   - 当前组: {(playerPiece.currentGroup?.name ?? "无")}");
        }
        else
        {
            Debug.Log($"👤 玩家 {player.name}: 无关联拼图");
        }

        if (triggerPiece != null)
        {
            Debug.Log($"🎯 触发器 {name}:");
            Debug.Log($"   - 拼图: {triggerPiece.name}");
            Debug.Log($"   - 组ID: {triggerPiece.GroupID}");
            Debug.Log($"   - 初始组ID: {triggerPiece.initialGroupID}");
            Debug.Log($"   - 当前组: {(triggerPiece.currentGroup?.name ?? "无")}");
        }
        else
        {
            Debug.Log($"🎯 触发器 {name}: 无关联拼图");
        }

        Debug.Log("==================");
    }

    /// <summary>
    /// 🔒 获取有效的目标节点组ID（确保分裂后的玩家在正确的组中）
    /// </summary>
    private int GetValidTargetGroupID(PathNode targetNode, int originalGroupID)
    {
        if (targetNode?.parentPiece != null)
        {
            return targetNode.parentPiece.GroupID;
        }
        
        // 如果目标节点没有有效的组ID，使用原始组ID
        return originalGroupID;
    }

    private void SplitPlayer(GameObject originalPlayer)
    {
        if (spawnNodeA == null || spawnNodeB == null || playerPrefab == null)
        {
            Debug.LogWarning("[PlayerSplitter] ❌ 缺少必要设置（节点或预制体未设置）");
            return;
        }

        PuzzlePiece oldPiece = originalPlayer.GetComponentInParent<PuzzlePiece>();
        int originalGroupID = oldPiece != null ? oldPiece.GroupID : -1;

        SpriteRenderer originalSprite = originalPlayer.GetComponent<SpriteRenderer>();
        PathMover originalMover = originalPlayer.GetComponent<PathMover>();

        PlayerData originalData = new PlayerData
        {
            sprite = originalSprite?.sprite,
            spriteColor = originalSprite?.color ?? Color.white,
            sortingLayerName = originalSprite?.sortingLayerName ?? "Default",
            sortingOrder = originalSprite?.sortingOrder ?? 0,
            scale = originalPlayer.transform.localScale,
            worldScale = originalPlayer.transform.lossyScale,
            groupID = originalGroupID
        };

        originalPlayer.SetActive(false);

        // 🔒 确保新玩家在正确的组中
        int groupID_A = GetValidTargetGroupID(spawnNodeA, originalGroupID);
        int groupID_B = GetValidTargetGroupID(spawnNodeB, originalGroupID);

        if (debugMode)
        {
            Debug.Log($"[分裂] 原始组ID: {originalGroupID}, 目标A组ID: {groupID_A}, 目标B组ID: {groupID_B}");
        }

        SpawnNewPlayer(spawnNodeA, originalData, "A", groupID_A);
        SpawnNewPlayer(spawnNodeB, originalData, "B", groupID_B);
    }

    private struct PlayerData
    {
        public Sprite sprite;
        public Color spriteColor;
        public string sortingLayerName;
        public int sortingOrder;
        public Vector3 scale;
        public Vector3 worldScale;
        public int groupID;
    }

    private void SpawnNewPlayer(PathNode node, PlayerData originalData, string label, int targetGroupID)
    {
        if (node == null)
        {
            Debug.LogWarning($"[PlayerSplitter] ❌ 路径节点 {label} 为空，无法生成玩家");
            return;
        }

        GameObject newPlayer = Instantiate(playerPrefab, node.transform.position, Quaternion.identity);
        newPlayer.name = $"Player_{label}";
        newPlayer.SetActive(true);

        CopyRenderingFromOriginal(newPlayer, originalData, label);

        PuzzlePiece targetPiece = node.parentPiece;
        if (targetPiece != null)
        {
            Vector3 worldScale = originalData.worldScale;
            newPlayer.transform.SetParent(targetPiece.transform, false);
            newPlayer.transform.position = node.transform.position;

            Vector3 parentScale = targetPiece.transform.lossyScale;
            newPlayer.transform.localScale = new Vector3(
                worldScale.x / (parentScale.x != 0 ? parentScale.x : 1),
                worldScale.y / (parentScale.y != 0 ? parentScale.y : 1),
                worldScale.z / (parentScale.z != 0 ? parentScale.z : 1)
            );
        }

        // 🔒 使用正确的目标组ID
        SetupPathMover(newPlayer, node, targetGroupID, label);
        SetupPuzzlePiece(newPlayer, targetGroupID, label);
        EnableAllComponents(newPlayer, label);

        foreach (var pNode in newPlayer.GetComponentsInChildren<PathNode>())
        {
            pNode.AssignParentPiece();
            pNode.RefreshPathLines();
        }

        RefreshAllPathNodeGroupIDs();

        PathMover mover = newPlayer.GetComponent<PathMover>();
        PuzzlePiece piece = newPlayer.GetComponentInParent<PuzzlePiece>();
        if (mover != null && piece != null)
        {
            mover.ForceUpdateGroupID(targetGroupID);
            mover.RefreshPaths();
        }

        if (debugMode)
        {
            Debug.Log($"[分裂完成] 生成玩家 {newPlayer.name}，目标组ID: {targetGroupID}");
        }
    }

    private void CopyRenderingFromOriginal(GameObject newPlayer, PlayerData originalData, string label)
    {
        var newSprite = newPlayer.GetComponent<SpriteRenderer>();
        if (newSprite == null) return;

        newSprite.sprite = originalData.sprite;
        Color spriteColor = originalData.spriteColor;
        if (spriteColor.a <= 0.01f) spriteColor.a = 1f;

        newSprite.color = spriteColor;
        newSprite.sortingLayerName = string.IsNullOrEmpty(originalData.sortingLayerName) ? "Default" : originalData.sortingLayerName;
        newSprite.sortingOrder = originalData.sortingOrder;
        newSprite.enabled = true;

        newPlayer.transform.localScale = originalData.scale == Vector3.zero ? Vector3.one : originalData.scale;
    }

    private void SetupPathMover(GameObject newPlayer, PathNode startNode, int groupID, string label)
    {
        var mover = newPlayer.GetComponent<PathMover>();
        if (mover == null) return;

        mover.enabled = false;
        mover.startNode = startNode;
        mover.transform.position = startNode.transform.position;
        mover.ForceUpdateGroupID(groupID);
        mover.enabled = true;
    }

    private void SetupPuzzlePiece(GameObject newPlayer, int groupID, string label)
    {
        var piece = newPlayer.GetComponentInParent<PuzzlePiece>();
        if (piece == null) return;

        piece.enabled = true;
        piece.initialGroupID = groupID;
        piece.originalGroupID = groupID;
    }

    private void EnableAllComponents(GameObject newPlayer, string label)
    {
        var collider = newPlayer.GetComponent<Collider2D>();
        if (collider != null) collider.enabled = true;

        var rigidbody = newPlayer.GetComponent<Rigidbody2D>();
        if (rigidbody != null) rigidbody.simulated = true;
    }

    private void RefreshAllPathNodeGroupIDs()
    {
        foreach (var node in FindObjectsOfType<PathNode>(true))
        {
            node.AssignParentPiece();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showDirectionArrow) return;
        var collider = GetComponent<Collider2D>();
        if (collider == null) return;

        Vector3 center = transform.position;
        Vector3 size = Vector3.one;

        if (collider is BoxCollider2D boxCollider)
        {
            size = boxCollider.size;
        }
        else if (collider is CircleCollider2D circleCollider)
        {
            float diameter = circleCollider.radius * 2;
            size = new Vector3(diameter, diameter, 1);
        }

        // 🔒 根据权限要求改变颜色
        Color gizmoColor = requireSameGroup ? new Color(1, 0.5f, 0, 0.3f) : new Color(1, 1, 0, 0.3f);
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(center, size);

        // 🔍 显示组信息（如果启用调试）
        if (showGroupInfo && debugMode)
        {
            var triggerPiece = GetComponentInParent<PuzzlePiece>();
            if (triggerPiece != null)
            {
                Gizmos.color = Color.white;
                Vector3 textPos = center + Vector3.up * (size.y / 2 + 0.2f);
                
                // 在编辑器中显示组ID
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(textPos, $"组ID: {triggerPiece.GroupID}");
                #endif
            }
        }
    }
}