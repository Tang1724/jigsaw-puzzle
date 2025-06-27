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
                Debug.Log($"[PlayerSplitter] ✅ 玩家 {player.name} 完整穿过触发器，触发分裂！");
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

    private void SplitPlayer(GameObject originalPlayer)
    {
        if (spawnNodeA == null || spawnNodeB == null || playerPrefab == null)
        {
            Debug.LogWarning("[PlayerSplitter] ❌ 缺少必要设置（节点或预制体未设置）");
            return;
        }

        PuzzlePiece oldPiece = originalPlayer.GetComponentInParent<PuzzlePiece>();
        int groupID = oldPiece != null ? oldPiece.GroupID : -1;

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
            groupID = groupID
        };

        originalPlayer.SetActive(false);

        SpawnNewPlayer(spawnNodeA, originalData, "A");
        SpawnNewPlayer(spawnNodeB, originalData, "B");
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

        SetupPathMover(newPlayer, node, originalData.groupID, label);
        SetupPuzzlePiece(newPlayer, originalData.groupID, label);
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
            mover.ForceUpdateGroupID(piece.GroupID);
            mover.RefreshPaths();
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

        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawCube(center, size);
    }
}