using System.Collections;
using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

public interface IHandler
{
    void Execute(IMessage data );
}

public abstract class BaseHandler<T> : IHandler where T : IMessage
{
    public void Execute(IMessage data)
    {
        if (data is T msg)
        {
            HandleMsg(msg);
        }
    }

    protected abstract void HandleMsg(T msg);
}
