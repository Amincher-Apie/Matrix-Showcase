using UnityEngine;
using UnityEngine.Serialization;

public class PlayerRender : RenderActor
{
    [HideInInspector] public PlayerTestRenderModule TestRenderModule;

    [HideInInspector] public WeaponAimController weaponAimController;
    [HideInInspector] public WeaponAnimationController weaponAnimationController;
    
    [SerializeField]private PlayerActor playerActor;
    public ulong ActorId => playerActor ? playerActor.ObjectId : 0;

    private void Awake()
    {
        if (playerActor == null)
        {
            playerActor = GetComponent<PlayerActor>();
        }
    }
    
    protected override void RegisterModules()
    {
        // 注册并初始化渲染模块
        var testModule = new PlayerTestRenderModule(this);
        AddModule(testModule);
        TestRenderModule = testModule; // 确保模块已实例化后再赋值
    }
    
    public Transform GetFirePoint()=> TestRenderModule.FirePoint;

    /// <summary>
    /// 绑定武器控制器
    /// </summary>
    public void BindWeaponController(
        WeaponAimController weaponAimer,
        WeaponAnimationController weaponAnimation = null,
        string weaponId = null)
    {
        weaponAimController = weaponAimer;
        if (TestRenderModule != null && weaponAimController)
        {
            TestRenderModule.SetWeaponAimController(weaponAimController);
        }

        weaponAnimationController = weaponAnimation;
        if (weaponAnimationController == null && weaponAimController != null)
        {
            weaponAnimationController = weaponAimController.GetComponentInParent<WeaponAnimationController>();
        }

        if (weaponAnimationController != null)
        {
            weaponAnimationController.BindOwner(ActorId, weaponId);
        }
    }
}
