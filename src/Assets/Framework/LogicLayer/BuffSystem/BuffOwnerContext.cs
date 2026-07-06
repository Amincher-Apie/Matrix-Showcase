
using UnityEngine;

public class BuffOwnerContext : IBuffOwnerContext
{
    public ulong NetworkObjectId { get; private set; }
    public BuffOwnerCategory OwnerCategory { get; private set; }
    public IAttributeProxy AttributeProxy { get; private set; }

    public BuffOwnerContext(ulong id, BuffOwnerCategory category, IAttributeProxy proxy)
    {
        NetworkObjectId = id;
        OwnerCategory = category;
        AttributeProxy = proxy;
    }
}