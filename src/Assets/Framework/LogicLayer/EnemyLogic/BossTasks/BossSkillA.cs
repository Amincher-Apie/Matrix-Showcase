using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

[TaskDescription("满足血量条件时返回 Success，用于二阶段/狂暴阈值判断。")]
public class BossSkillA : Action
{
    private BossBTBridge _bridge;
    private bool _isStart;
    private float _progress;

    public override void OnStart()
    {
        _bridge = GetComponent<BossBTBridge>();
        _bridge?.SetAnimTrigger("shockWave");
        _isStart = false;
    }

    public override TaskStatus OnUpdate()
    {
        if (_bridge == null) return TaskStatus.Failure;

        var clipInfoArray = _bridge.GetCurrentClipInfo();
        if (clipInfoArray.Length > 0)
        {
            string clipName = clipInfoArray[0].clip.name;
            if (clipName == "castSpellA")
                _isStart = true;
        }

        if (_isStart)
        {
            var state = _bridge.GetCurrentAnimState();
            _progress = state.normalizedTime;
            if (_progress >= 0.89f)
                return TaskStatus.Success;
        }

        return TaskStatus.Running;
    }
}
