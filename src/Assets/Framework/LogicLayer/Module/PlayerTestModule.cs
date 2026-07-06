using Unity.Netcode;
using UnityEngine;

public class PlayerTestModule : IModule
{
    private PlayerActor _playerActor;

    public ulong ObjectId => _playerActor.ObjectId;
    
    public PlayerTestModule(PlayerActor playerActor)
    {
        _playerActor = playerActor;
    }

    public void Fire()
    {
        
    }

    #region 生命周期函数

    public void LocalInit()
    {
        
    }

    public void OnActivate()
    {
        
    }

    public void LocalDestroy()
    {
        
    }

    #endregion
}