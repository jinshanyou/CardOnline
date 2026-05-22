using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System;


public enum CardType
{
    Attack = 1,
    Defense = 2,
    Magic = 3
}
public enum CardTargetType
{
    Self = 1,
    SingleEnemy = 2,
    AllEnemies = 3
}

// 💡 原子效果配置节点 (注意要加 [Serializable]，否则在面板上显示不出来)
[Serializable]
public class EffectNode
{
    [Tooltip("效果ID，对应代码里的 [CardEffect]")]
    public string EffectId; 
    
    [Tooltip("效果数值 (伤害值/回血量/抽牌数)")]
    public int Value;       
}

[CreateAssetMenu(fileName = "CardData", menuName = "Game/Card Data")]
public class CardData : ScriptableObject
{
    // 💡 架构师黑科技：使用 [field: SerializeField] 
    // 让它在 Inspector 面板上可以配置，但在其他 C# 脚本中只有 get (只读) 权限！
    
    [field: Header("Basic Info")]
    [field: SerializeField] public int Id { get; private set; }
    
    [field: SerializeField] public string CardName { get; private set; }
    
    [field: SerializeField, TextArea] public string Description { get; private set; }

    [field: Header("Battle Info")]
    [field: SerializeField] public int BaseValue { get; private set; }
    [field: SerializeField] public int Cost { get; private set; }
    [field: SerializeField] public CardType Type { get; private set; }
    [field: SerializeField] public CardTargetType TargetType { get; private set; }
    
    [field: Header("Effects 效果列表")]
    // 💡 重点：这里变成了一个列表！
    [field: SerializeField] public List<EffectNode> EffectNodes { get; private set; }
    
    [field: Header("Resource Ref")]
    [field: SerializeField] public AssetReferenceSprite IconRef { get; private set; }
    [field: SerializeField] public AssetReferenceGameObject CastEffectRef { get; private set; }
}