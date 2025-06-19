using UnityEngine;

/// <summary>
/// 玩家分流器：玩家穿过触发器后，在两个节点生成新玩家，并销毁原玩家
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

        // 获取旧玩家的组 ID
        PuzzlePiece oldPiece = other.GetComponentInParent<PuzzlePiece>();
        int groupID = oldPiece != null ? oldPiece.GroupID : -1;

        Debug.Log($"[PlayerSplitter] 👥 玩家分流触发，原组 ID: {groupID}");

        // 销毁旧玩家
        Destroy(other.gameObject);

        // 生成两个新玩家
        SpawnNewPlayer(spawnNodeA, groupID, "A");
        SpawnNewPlayer(spawnNodeB, groupID, "B");

        hasTriggered = true;
    }

    /// <summary>
    /// 在指定节点生成新玩家，并设置其父级为该节点所在的拼图块
    /// </summary>
    private void SpawnNewPlayer(PathNode node, int groupID, string label)
    {
        if (node == null)
        {
            Debug.LogWarning($"[PlayerSplitter] ❌ 路径节点 {label} 为空，无法生成玩家");
            return;
        }

        // 实例化玩家
        GameObject newPlayer = Instantiate(playerPrefab, node.transform.position, Quaternion.identity);
        if (newPlayer == null)
        {
            Debug.LogError($"[PlayerSplitter] ❌ 玩家预制体生成失败！");
            return;
        }

        newPlayer.name = $"Player_{label}";

        // ✅ 确保新玩家GameObject是激活状态
        newPlayer.SetActive(true);
        Debug.Log($"[PlayerSplitter] 🔋 Player_{label} GameObject已激活");

        // ✅ 设置为该节点所属拼图块的子对象
        PuzzlePiece targetPiece = node.parentPiece;
        if (targetPiece != null)
        {
            newPlayer.transform.SetParent(targetPiece.transform, worldPositionStays: true);
            Debug.Log($"[PlayerSplitter] ✅ Player_{label} 已设置为拼图 {targetPiece.name} 的子对象");
        }
        else
        {
            Debug.LogWarning($"[PlayerSplitter] ⚠️ 无法找到节点 {node.name} 所属的拼图块，Player_{label} 保留在场景根目录");
        }

        // 设置 PathMover 起点并确保组件启用
        var mover = newPlayer.GetComponent<PathMover>();
        if (mover != null)
        {
            mover.enabled = true; // ✅ 确保PathMover组件启用
            mover.startNode = node;
            mover.transform.position = node.transform.position;
            mover.ForceUpdateGroupID(groupID);
            Debug.Log($"[PlayerSplitter] ✅ Player_{label} PathMover已启用，起点设置完成，并分配到组 {groupID}");
        }
        else
        {
            Debug.LogWarning($"[PlayerSplitter] ⚠️ Player_{label} 缺少 PathMover 组件");
        }

        // 设置 PuzzlePiece 组信息并确保组件启用
        var piece = newPlayer.GetComponentInParent<PuzzlePiece>();
        if (piece != null)
        {
            piece.enabled = true; // ✅ 确保PuzzlePiece组件启用
            piece.initialGroupID = groupID;
            piece.originalGroupID = groupID;
            Debug.Log($"[PlayerSplitter] 🧩 Player_{label} PuzzlePiece已启用，拼图组号设置为 {groupID}");
        }
        else
        {
            Debug.LogWarning($"[PlayerSplitter] ⚠️ Player_{label} 缺少 PuzzlePiece 脚本");
        }

        // ✅ 确保其他可能存在的关键组件也是启用状态
        var collider = newPlayer.GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = true;
            Debug.Log($"[PlayerSplitter] 🔲 Player_{label} Collider2D已启用");
        }

        var rigidbody = newPlayer.GetComponent<Rigidbody2D>();
        if (rigidbody != null)
        {
            rigidbody.simulated = true; // Rigidbody2D使用simulated属性而不是enabled
            Debug.Log($"[PlayerSplitter] 🏃 Player_{label} Rigidbody2D已启用");
        }

        var renderer = newPlayer.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
            Debug.Log($"[PlayerSplitter] 🎨 Player_{label} Renderer已启用");
        }

        Debug.Log($"[PlayerSplitter] 🎯 Player_{label} 所有组件激活完成！");
    }
}