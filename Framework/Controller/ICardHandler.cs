using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICardEffect
{
    /// <summary>
    /// 调用卡牌效果
    /// </summary>
    /// <param name="card">卡牌</param>
    /// <param name="caster">释放者</param>
    /// <param name="target">目标</param>
    void Execute(EffectNode effect, BaseEntity caster, BaseEntity target, BattleContext context);
}

[CardEffect("Attack")]
public class Attack : ICardEffect
{
    public void Execute(EffectNode effect, BaseEntity caster, BaseEntity target , BattleContext context)
    {
        //先简单写个普通的攻击逻辑
        target.TakeDamage(effect.Value);
    }
}

// 效果2：回血术 (治疗自己)
[CardEffect("Heal")]
public class EffectHeal : ICardEffect
{
    public void Execute(EffectNode effect, BaseEntity caster, BaseEntity target, BattleContext context)
    {
        // 给自己加血 (你可以给 Entity 加个 Heal 方法，或者直接 CurrentHp += Value)
        caster.Heal(effect.Value);
        Debug.Log($"{caster.EntityName} 回复了 {effect.Value} 点生命！");
    }
}

// 效果3：奥术智慧 (抽牌)
[CardEffect("DrawCard")]
public class EffectDrawCard : ICardEffect
{
    public void Execute(EffectNode effect, BaseEntity caster, BaseEntity target, BattleContext context)
    {
        if (caster is PlayerEntity player)
        {
            player.DrawCard(effect.Value);
        }
    }
}
