using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;


//static写的在部分情况不好用
public static class EffectFactory
{
    // // 字典 Key 变成了 string (对应配置表里的 effectId)
    // private static Dictionary<string, ICardEffect> effectDic = new Dictionary<string, ICardEffect>();
    //
    // // 游戏启动时调用一次（比如在 BattleMgr 的 Awake 里）
    // public static void AutoRegisterEffects()
    // {
    //     //先清空防止多次注册
    //     effectDic.Clear();
    //     Assembly assembly = Assembly.GetExecutingAssembly();
    //     //获取所有的type
    //     Type[] allTypes = assembly.GetTypes();
    //     //遍历所有type找到带有我们指定特性的type
    //     foreach (var type in allTypes)
    //     {
    //         var attribute = type.GetCustomAttribute<CardEffectAttribute>();
    //         if (attribute == null) continue;
    //         
    //         // 确保实现了接口且不是抽象类
    //         if (typeof(ICardEffect).IsAssignableFrom(type) && !type.IsAbstract)
    //         {
    //             ICardEffect instance = (ICardEffect)Activator.CreateInstance(type);
    //             effectDic.Add(attribute.EffectId, instance);
    //             Debug.Log($"自动注册卡牌效果: {type.Name} -> EffectId: {attribute.EffectId}");
    //         }
    //     }
    // }
    //
    // // 💡 重点：遍历卡牌里的所有效果节点，按顺序执行！
    // public static void ExecuteCard(CardData card, BaseEntity caster, BaseEntity target)
    // {
    //     foreach (var node in card.EffectNodes)
    //     {
    //         if (effectDic.TryGetValue(node.EffectId, out var effect))
    //         {
    //             effect.Execute(node, caster, target, );
    //         }
    //         else
    //         {
    //             Debug.LogError($"未知的效果ID: {node.EffectId}");
    //         }
    //     }
    // }
}

