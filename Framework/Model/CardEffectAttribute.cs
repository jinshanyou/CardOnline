using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// 贴在特效处理类头上的标签
[AttributeUsage(AttributeTargets.Class)]
public class CardEffectAttribute : Attribute
{
    public string EffectId { get; private set; }
    public CardEffectAttribute(string effectId)
    {
        this.EffectId = effectId;
    }
}
