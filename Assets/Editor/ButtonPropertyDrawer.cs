#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Reflection;

[CustomPropertyDrawer(typeof(ButtonAttribute))]
public class ButtonPropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        ButtonAttribute buttonAttribute = (ButtonAttribute)attribute;
        
        // 获取目标对象
        object target = property.serializedObject.targetObject;
        
        // 获取方法
        MethodInfo method = target.GetType().GetMethod(buttonAttribute.methodName, 
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        
        if (method != null)
        {
            // 绘制按钮
            if (GUI.Button(position, buttonAttribute.methodName))
            {
                // 调用方法
                method.Invoke(target, null);
            }
        }
        else
        {
            // 如果方法不存在，显示错误信息
            EditorGUI.LabelField(position, $"方法 '{buttonAttribute.methodName}' 未找到", EditorStyles.helpBox);
        }
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}
#endif