using System;
using UnityEngine;

public class ButtonAttribute : PropertyAttribute
{
    public string methodName;
    
    public ButtonAttribute(string methodName)
    {
        this.methodName = methodName;
    }
}