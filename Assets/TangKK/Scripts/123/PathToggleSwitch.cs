using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(PathNode))]
[RequireComponent(typeof(Collider2D))]
public class PathToggleSwitch : MonoBehaviour
{
    [Header("连接 A（默认连接）")]
    public PathNode nodeA1;
    public PathNode nodeA2;

    [Header("连接 B（替代连接）")]
    public PathNode nodeB1;
    public PathNode nodeB2;

    /// 当前拼图块
    private PuzzlePiece parentPiece;

    /// 当前是否切换状态
    private bool isToggled = false;

    private Vector3 enterPosition;
    private bool playerInside = false;

    /// 静态：记录所有开关
    private static List<PathToggleSwitch> allSwitches = new List<PathToggleSwitch>();

    private void Awake()
    {
        parentPiece = GetComponentInParent<PuzzlePiece>();
        allSwitches.Add(this);
    }

    private void OnDestroy()
    {
        allSwitches.Remove(this);
    }

    void Start()
    {
        var col = GetComponent<Collider2D>();
        if (col) col.isTrigger = true;

        // 初始化路径
        SetConnection(nodeA1, nodeA2, true);
        SetConnection(nodeB1, nodeB2, false);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (!IsSameGroup(other.gameObject))
        {
            Debug.Log("[开关] 玩家与开关不在同一拼图组，忽略触发");
            return;
        }

        enterPosition = other.transform.position;
        playerInside = true;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !playerInside) return;

        if (!IsSameGroup(other.gameObject))
        {
            playerInside = false;
            return;
        }

        Vector3 exitPosition = other.transform.position;
        Vector3 center = GetComponent<Collider2D>().bounds.center;

        Vector3 toEnter = enterPosition - center;
        Vector3 toExit = exitPosition - center;

        float dot = Vector3.Dot(toEnter.normalized, toExit.normalized);
        bool crossedThrough = dot < 0f;

        if (crossedThrough)
        {
            Debug.Log("[开关] 玩家完整穿过，触发路径切换");

            // ✅ 同组联动切换
            ToggleAllSwitchesInSameGroup();
        }
        else
        {
            Debug.Log("[开关] 玩家没有穿过中心，忽略");
        }

        playerInside = false;
    }

    /// <summary>
    /// 切换所有同组开关
    /// </summary>
    void ToggleAllSwitchesInSameGroup()
    {
        int myGroupID = parentPiece != null ? parentPiece.GroupID : GetInstanceID();

        foreach (var sw in allSwitches)
        {
            if (sw == null) continue;
            if (sw == this || sw.GetGroupID() == myGroupID)
            {
                sw.TogglePath(); // 同组，执行切换
            }
        }
    }

    /// <summary>
    /// 切换路径状态
    /// </summary>
    void TogglePath()
    {
        isToggled = !isToggled;

        if (isToggled)
        {
            SetConnection(nodeA1, nodeA2, false);
            SetConnection(nodeB1, nodeB2, true);
        }
        else
        {
            SetConnection(nodeB1, nodeB2, false);
            SetConnection(nodeA1, nodeA2, true);
        }

        // ✅ 修复：刷新所有PathMover实例，而不只是第一个
        RefreshAllPathMovers();

        Debug.Log($"[开关] 路径切换完成，现在是 {(isToggled ? "B 路径" : "A 路径")}");
    }

    /// <summary>
    /// ✅ 新增：刷新所有PathMover实例
    /// </summary>
    void RefreshAllPathMovers()
    {
        PathMover[] allMovers = FindObjectsOfType<PathMover>();
        foreach (var mover in allMovers)
        {
            if (mover != null)
            {
                mover.RefreshPaths();
                Debug.Log($"[开关] 刷新PathMover: {mover.name}");
            }
        }
    }

    void SetConnection(PathNode a, PathNode b, bool enable)
    {
        if (a == null || b == null) return;

        if (enable)
        {
            if (!a.IsConnectedTo(b))
            {
                a.ConnectTo(b);
            }
            a.SetPathActive(b, true); // ✅ 显示实线
        }
        else
        {
            if (a.IsConnectedTo(b))
            {
                a.SetPathActive(b, false); // ✅ 显示虚线
            }
        }
    }

    int GetGroupID()
    {
        return parentPiece != null ? parentPiece.GroupID : GetInstanceID();
    }

    /// <summary>
    /// 判断玩家是否与开关在同一拼图组合中
    /// </summary>
    bool IsSameGroup(GameObject player)
    {
        var playerPiece = player.GetComponentInParent<PuzzlePiece>();
        if (playerPiece == null || parentPiece == null) return false;

        return playerPiece.GroupID == parentPiece.GroupID;
    }
}