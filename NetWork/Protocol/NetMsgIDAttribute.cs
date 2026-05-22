using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AttributeUsage(AttributeTargets.Class)]
public class NetMsgIDAttribute : Attribute
{
    public int msgID;
    public NetMsgIDAttribute(int id)
    {
        msgID = id;
    }
}

