using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GetAttribute
{

}
// 将你的 Attribute 改为可打在 Protobuf 消息类上
[NetMsgID(999)]
public partial class HeartMsg { }
[NetMsgID(998)]
public partial class QuitMsg { }
[NetMsgID(1001)]
public partial class PlayerInfo { }

[NetMsgID(2001)]
public partial class ReqPlayCard { }
[NetMsgID(2002)]
public partial class SyncPlayCard{}
[NetMsgID(2003)]
public partial class SyncStartBattle{}

[NetMsgID(2004)]
public partial class ReqEndTurn{}
[NetMsgID(2005)]
public partial class SyncEndTurn{}
