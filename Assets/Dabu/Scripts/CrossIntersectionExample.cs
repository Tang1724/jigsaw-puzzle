using UnityEngine;

/*
十字形交叉路口设置示例

要创建一个十字形交叉路口，您需要：

1. 创建4个节点（Node）：
   - 节点0: 上方 (-1, 1)
   - 节点1: 右方 (1, 0) 
   - 节点2: 下方 (-1, -1)
   - 节点3: 左方 (-1, 0)

2. 创建2个路径段（Segment）：
   - 段0: 连接节点0和节点2（上下连接）
   - 段1: 连接节点1和节点3（左右连接）

3. 在Unity编辑器中设置：
   - 在Dabu_PuzzleGraph组件中
   - 添加4个节点，设置它们的位置
   - 添加2个路径段，设置startNodeIndex和endNodeIndex
   - 确保isBidirectional = true，isActive = true

这样设置后，玩家就可以：
- 在水平路径上左右移动
- 在垂直路径上上下移动
- 在交叉点切换到垂直方向
*/

public class CrossIntersectionExample : MonoBehaviour
{
    [Header("十字形交叉路口设置")]
    [SerializeField] private Dabu_PuzzleGraph puzzleGraph;
    
    [ContextMenu("设置十字形交叉路口")]
    public void SetupCrossIntersection()
    {
        if (puzzleGraph == null)
        {
            puzzleGraph = GetComponent<Dabu_PuzzleGraph>();
        }
        
        if (puzzleGraph == null) return;
        
        // 清空现有数据
        puzzleGraph.nodes.Clear();
        puzzleGraph.segments.Clear();
        
        // 添加4个节点
        puzzleGraph.nodes.Add(new Dabu_PathNode { position = new Vector2(0, 1) });   // 上
        puzzleGraph.nodes.Add(new Dabu_PathNode { position = new Vector2(1, 0) });   // 右
        puzzleGraph.nodes.Add(new Dabu_PathNode { position = new Vector2(0, -1) });  // 下
        puzzleGraph.nodes.Add(new Dabu_PathNode { position = new Vector2(-1, 0) });  // 左
        
        // 添加上下连接
        puzzleGraph.segments.Add(new Dabu_PathSegment
        {
            startNodeIndex = 0,  // 上
            endNodeIndex = 2,    // 下
            isBidirectional = true,
            isActive = true
        });
        
        // 添加左右连接
        puzzleGraph.segments.Add(new Dabu_PathSegment
        {
            startNodeIndex = 1,  // 右
            endNodeIndex = 3,    // 左
            isBidirectional = true,
            isActive = true
        });
        
        Debug.Log("十字形交叉路口设置完成！");
    }
} 