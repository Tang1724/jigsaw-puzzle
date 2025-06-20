using UnityEngine;

/// <summary>
/// 玩家分流器：穿过触发器后在两个节点生成新玩家，并销毁原玩家
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PlayerSplitterTrigger : MonoBehaviour
{
    [Header("目标节点 A 和 B")]
    public PathNode spawnNodeA;
    public PathNode spawnNodeB;

    [Header("玩家预制体（必须包含 PathMover + PuzzlePiece）")]
    public GameObject playerPrefab;

    [Header("是否只触发一次")]
    public bool triggerOnce = true;

    [Header("调试选项")]
    public bool debugMode = true;

    private bool hasTriggered = false;

    private void Awake()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (hasTriggered && triggerOnce) return;

        if (spawnNodeA == null || spawnNodeB == null || playerPrefab == null)
        {
            Debug.LogWarning("[PlayerSplitter] ❌ 缺少必要设置（节点或预制体未设置）");
            return;
        }

        GameObject originalPlayer = other.gameObject;
        PuzzlePiece oldPiece = other.GetComponentInParent<PuzzlePiece>();
        int groupID = oldPiece != null ? oldPiece.GroupID : -1;

        SpriteRenderer originalSprite = originalPlayer.GetComponent<SpriteRenderer>();
        PathMover originalMover = originalPlayer.GetComponent<PathMover>();

        if (debugMode)
        {
            Debug.Log($"[PlayerSplitter] 📋 原玩家信息:");
            Debug.Log($"  - 组ID: {groupID}");
            Debug.Log($"  - Sprite: {(originalSprite?.sprite?.name ?? "无")}");
            Debug.Log($"  - 位置: {originalPlayer.transform.position}");
            Debug.Log($"  - PathMover状态: {(originalMover != null ? "存在" : "缺失")}");
        }

        PlayerData originalData = new PlayerData
        {
            sprite = originalSprite?.sprite,
            spriteColor = originalSprite?.color ?? Color.white,
            sortingLayerName = originalSprite?.sortingLayerName ?? "Default",
            sortingOrder = originalSprite?.sortingOrder ?? 0,
            scale = originalPlayer.transform.localScale,
            worldScale = originalPlayer.transform.lossyScale,
            groupID = groupID
        };

        Destroy(originalPlayer);

        SpawnNewPlayer(spawnNodeA, originalData, "A");
        SpawnNewPlayer(spawnNodeB, originalData, "B");

        hasTriggered = true;
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

    private void SpawnNewPlayer(PathNode node, PlayerData originalData, string label)
    {
        if (node == null)
        {
            Debug.LogWarning($"[PlayerSplitter] ❌ 路径节点 {label} 为空，无法生成玩家");
            return;
        }

        Debug.Log($"🎯 开始生成 Player_{label} 在节点: {node.name}");

        GameObject newPlayer = Instantiate(playerPrefab, node.transform.position, Quaternion.identity);
        if (newPlayer == null)
        {
            Debug.LogError($"[PlayerSplitter] ❌ 玩家预制体生成失败！");
            return;
        }

        newPlayer.name = $"Player_{label}";
        newPlayer.SetActive(true);

        CopyRenderingFromOriginal(newPlayer, originalData, label);

        PuzzlePiece targetPiece = node.parentPiece;
        if (targetPiece != null)
        {
            Vector3 worldScale = originalData.worldScale;

            newPlayer.transform.SetParent(targetPiece.transform, worldPositionStays: false);
            newPlayer.transform.position = node.transform.position;

            Vector3 parentScale = targetPiece.transform.lossyScale;
            newPlayer.transform.localScale = new Vector3(
                worldScale.x / (parentScale.x != 0 ? parentScale.x : 1),
                worldScale.y / (parentScale.y != 0 ? parentScale.y : 1),
                worldScale.z / (parentScale.z != 0 ? parentScale.z : 1)
            );

            Debug.Log($"[PlayerSplitter] ✅ Player_{label} 设置为 {targetPiece.name} 子对象，恢复原始缩放");
        }
        else
        {
            Debug.LogWarning($"[PlayerSplitter] ⚠️ 无法找到节点 {node.name} 所属的拼图块");
        }

        SetupPathMover(newPlayer, node, originalData.groupID, label);
        SetupPuzzlePiece(newPlayer, originalData.groupID, label);
        EnableAllComponents(newPlayer, label);

        var allNodes = newPlayer.GetComponentsInChildren<PathNode>();
        foreach (var pNode in allNodes)
        {
            if (pNode != null)
            {
                pNode.AssignParentPiece(); // 确保该方法为 public
                pNode.RefreshPathLines();
                Debug.Log($"[PlayerSplitter] 🔁 节点 {pNode.name} 路径线已刷新");
            }
        }

        // ✅ 关键修复：刷新所有 PathNode 的 parentPiece，确保路径段所属拼图正确
        RefreshAllPathNodeGroupIDs();

        // ✅ 每个玩家生成后立即刷新其路径状态
        var mover = newPlayer.GetComponent<PathMover>();
        var piece = newPlayer.GetComponentInParent<PuzzlePiece>();
        if (mover != null && piece != null)
        {
            mover.ForceUpdateGroupID(piece.GroupID);
            mover.RefreshPaths();
            Debug.Log($"[PlayerSplitter] 🔁 Player_{label} 路径状态刷新完成");
        }

        if (debugMode)
        {
            var finalSprite = newPlayer.GetComponent<SpriteRenderer>();
            Debug.Log($"🔍 Player_{label} 最终检查:");
            Debug.Log($"  - Sprite: {(finalSprite?.sprite?.name ?? "无")}");
            Debug.Log($"  - 位置: {newPlayer.transform.position}");
            Debug.Log($"  - 激活状态: {newPlayer.activeInHierarchy}");
        }

        Debug.Log($"[PlayerSplitter] 🎯 Player_{label} 创建完成！");
    }

    private void CopyRenderingFromOriginal(GameObject newPlayer, PlayerData originalData, string label)
    {
        var newSprite = newPlayer.GetComponent<SpriteRenderer>();
        if (newSprite == null)
        {
            Debug.LogError($"[PlayerSplitter] ❌ Player_{label} 缺少SpriteRenderer组件！");
            return;
        }

        newSprite.sprite = originalData.sprite;
        Color spriteColor = originalData.spriteColor;

        if (spriteColor.a <= 0.01f)
        {
            spriteColor.a = 1;
            Debug.LogWarning($"[PlayerSplitter] ⚠️ 原颜色透明，已设置为不透明");
        }

        newSprite.color = spriteColor;
        newSprite.sortingLayerName = string.IsNullOrEmpty(originalData.sortingLayerName) ? "Default" : originalData.sortingLayerName;
        newSprite.sortingOrder = originalData.sortingOrder;
        newSprite.enabled = true;

        newPlayer.transform.localScale = originalData.scale == Vector3.zero ? Vector3.one : originalData.scale;

        Debug.Log($"[PlayerSplitter] ✅ Player_{label} 渲染设置完成，sprite: {(newSprite.sprite?.name ?? "无")}");
    }

    private void SetupPathMover(GameObject newPlayer, PathNode startNode, int groupID, string label)
    {
        var mover = newPlayer.GetComponent<PathMover>();
        if (mover == null)
        {
            Debug.LogWarning($"[PlayerSplitter] ⚠️ Player_{label} 缺少PathMover组件");
            return;
        }

        mover.enabled = false;
        mover.startNode = startNode;
        mover.transform.position = startNode.transform.position;
        mover.ForceUpdateGroupID(groupID);
        mover.enabled = true;

        Debug.Log($"[PlayerSplitter] 🚶 Player_{label} PathMover设置完成，起点: {startNode.name}");
    }

    private void SetupPuzzlePiece(GameObject newPlayer, int groupID, string label)
    {
        var piece = newPlayer.GetComponentInParent<PuzzlePiece>();
        if (piece == null)
        {
            Debug.LogWarning($"[PlayerSplitter] ⚠️ Player_{label} 缺少PuzzlePiece组件");
            return;
        }

        piece.enabled = true;
        piece.initialGroupID = groupID;
        piece.originalGroupID = groupID;

        Debug.Log($"[PlayerSplitter] 🧩 Player_{label} PuzzlePiece设置完成，组ID: {groupID}");
    }

    private void EnableAllComponents(GameObject newPlayer, string label)
    {
        var collider = newPlayer.GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = true;
            Debug.Log($"[PlayerSplitter] 🔲 Player_{label} Collider2D已启用");
        }

        var rigidbody = newPlayer.GetComponent<Rigidbody2D>();
        if (rigidbody != null)
        {
            rigidbody.simulated = true;
            Debug.Log($"[PlayerSplitter] 🏃 Player_{label} Rigidbody2D已启用");
        }

        Debug.Log($"[PlayerSplitter] ✅ Player_{label} 所有组件已启用");
    }

    /// <summary>
    /// 强制刷新所有 PathNode 的 parentPiece 引用（用于路径段所属判断）
    /// </summary>
    private void RefreshAllPathNodeGroupIDs()
    {
        PathNode[] allNodes = FindObjectsOfType<PathNode>(true);
        foreach (var node in allNodes)
        {
            node.AssignParentPiece();
        }
    }
}