using System;
using System.Collections.Generic;
using UnityEngine;

public enum BuffUpdateTimeEnum //用来处理Buff每一帧的更新
{
    add,//当被施加buff的时候该层buff会累积时间
    replace,//当施加/掉落的buff层数其需求为刷新buff持续时间
    keep,//buff施加/掉落的时候保持原有时间
    single//buff是逐层掉落,单独计算时间的
}

public enum BuffRemoveStackUpdateEnum //用来处理buff的层数更新
{
    clear, //buff失效即清空
    reduce, //buff会逐层掉落
    single, //buff是独立计算的
    half, //buff会掉落至原来的一半
    none, //这里应该只有9001能用
}

public enum BuffStackKeyMode
{
    BuffIdOnly,
    BuffIdAndApplier
}
