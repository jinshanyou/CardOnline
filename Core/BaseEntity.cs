using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//规范命名：事件结构体以 Msg 开头。必须带上是谁（Uid）的数据变了！
public struct MsgEntityHpChange
{
    public string uid;       // 身份标识！UI 根据这个决定刷新哪个血条
    public int currentHp;
    public int maxHp;        // 顺便把最大血量传过去，UI 好算百分比进度条
}
public struct MsgEnemyEntityHpChange
{
    public string uid;       // 身份标识！UI 根据这个决定刷新哪个血条
    public int currentHp;
    public int maxHp;        // 顺便把最大血量传过去，UI 好算百分比进度条
}
// 死亡事件
public struct MsgEntityDead
{
    public string uid;
}
public abstract class BaseEntity
{
    public string EntityUid;   // 实体的唯一ID (网络联机必备)
    public string EntityName;
    public int MaxHp { get; private set; } // 卡牌游戏一般用 int 就够了，不需要 float
    public int CurrentHp { get; private set; }
    public bool IsDead { get; private set; }

    //这里初始化是不是传配置文件比较好
    public virtual void Init(string uid, string name, int maxHp)
    {
        EntityUid = uid;
        EntityName = name;
        MaxHp = maxHp;
        CurrentHp = maxHp; // 初始化时满血
        IsDead = false;
    }

    public virtual void TakeDamage(int damage)
    {
        if (IsDead) return;
        
        CurrentHp -= damage;
        CurrentHp = Mathf.Max(0, CurrentHp); // 防止变成负数
        // 告诉外面（UI或特效逻辑），我的血量变了
        // 把自己的当前血量传出
        EventCenter.Instance.EventTrigger(new MsgEntityHpChange {
            currentHp = CurrentHp ,
            maxHp = MaxHp ,
            uid = EntityUid
        });

        if (CurrentHp <= 0)
        {
            IsDead = true;
            Dead();
        }
    }
    protected virtual void Dead()
    {
        IsDead = true; // 真正标记死亡
        Debug.Log($"实体 [{EntityName}] 已阵亡！");
        
        // 抛出干净的死亡事件，战场管理器或 UI 会监听它
    
        EventCenter.Instance.EventTrigger(new MsgEntityDead { uid = this.EntityUid });
    }
    
    public virtual void Heal(int healVal)
    {
        if (IsDead) return;
        CurrentHp += healVal;
        CurrentHp = Mathf.Min(MaxHp, CurrentHp); // 防止溢出上限
    
        // 抛出事件，更新 UI 血条和伤害飘字
        EventCenter.Instance.EventTrigger(new MsgEntityHpChange {
            currentHp = CurrentHp,
            maxHp = MaxHp,
            uid = EntityUid
        });
    }
}
