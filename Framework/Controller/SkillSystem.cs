using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;
using Unity.VisualScripting.FullSerializer;

// 战斗上下文，把这局战斗需要的环境全部打包
public class BattleContext
{
    public BattleMgr battleMgr;
    public PlayerEntity player1;
    public PlayerEntity player2;
    // 未来还可以放随机数种子、网络发送器等
}
public class SkillSystem
{
    private Dictionary<string, ICardEffect> effectDic = new Dictionary<string, ICardEffect>();
    private BattleContext currentContext;
    
    // 战斗初始化时才 new 出来并注册
    public void Init(BattleContext context)
    {
        this.currentContext = context;
        RegisterAllEffects();
    }
    
    private void RegisterAllEffects()
    {
        //先清空防止多次注册
        effectDic.Clear();
        Assembly assembly = Assembly.GetExecutingAssembly();
        //获取所有的type
        Type[] allTypes = assembly.GetTypes();
        //遍历所有type找到带有我们指定特性的type
        foreach (var type in allTypes)
        {
            var attribute = type.GetCustomAttribute<CardEffectAttribute>();
            if (attribute == null) continue;
            
            // 确保实现了接口且不是抽象类
            if (typeof(ICardEffect).IsAssignableFrom(type) && !type.IsAbstract)
            {
                ICardEffect instance = (ICardEffect)Activator.CreateInstance(type);
                effectDic.Add(attribute.EffectId, instance);
                Debug.Log($"自动注册卡牌效果: {type.Name} -> EffectId: {attribute.EffectId}");
            }
        }
    }
    
    public void ExecuteCard(CardData card, BaseEntity caster, BaseEntity target)
    {
        foreach (var node in card.EffectNodes)
        {
            if (effectDic.TryGetValue(node.EffectId, out var effect))
            {
                // 💡 把 context 传进去，Effect 内部要什么有什么，完全解耦！
                effect.Execute(node, caster, target, currentContext);
                Debug.Log("成功攻击");
            }
        }
    }

}
