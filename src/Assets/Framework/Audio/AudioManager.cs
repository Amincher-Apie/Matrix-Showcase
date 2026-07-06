using UnityEngine;
using System.Collections.Generic;
using Framework.Singleton;
using Framework.Resource;
using DG.Tweening;

/// <summary>
/// 音频管理器
/// 负责音效和背景音乐的播放、暂停、停止等管理，支持优先级和资源缓存
/// </summary>
public class AudioManager : MonoSingletonBase<AudioManager>
{
    #region 常量定义
    
    /// <summary>最大音源缓存数量</summary>
    private const int MaxAudioSourceCount = 30;
    
    /// <summary>最大音频剪辑缓存数量</summary>
    private const int MaxClipCacheCount = 50;
    
    /// <summary>音效默认音量</summary>
    private const float DefaultSoundVolume = 0.5f;
    
    /// <summary>音乐默认音量</summary>
    private const float DefaultMusicVolume = 0.5f;
    
    #endregion

    #region 私有字段
    /// <summary>已加载的音频剪辑缓存（路径 -> 剪辑）</summary>
    private readonly Dictionary<string, AudioClip> _soundClipCache = new Dictionary<string, AudioClip>();
    
    /// <summary>音频剪辑使用记录（用于缓存淘汰）</summary>
    private readonly Queue<string> _clipUsageQueue = new Queue<string>();
    
    /// <summary>带优先级的音源对象池</summary>
    private readonly List<AudioSourceInfo> _audioSourceInfos = new List<AudioSourceInfo>();
    
    /// <summary>背景音乐音源</summary>
    private AudioSource _musicSource;
    
    /// <summary>当前音效音量</summary>
    private float _soundVolume;
    
    /// <summary>当前音乐音量</summary>
    private float _musicVolume;
    
    #endregion

    #region 单例与初始化
    /// <summary>
    /// 初始化（单例创建时调用）
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();
        InitAudioSources();
        InitDefaultVolumes();
        Debug.Log("[AudioManager] 初始化完成");
    }

    /// <summary>
    /// 初始化音源组件
    /// </summary>
    private void InitAudioSources()
    {
        // 创建背景音乐音源
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = true;

        // 初始化音效音源对象池
        for (int i = 0; i < MaxAudioSourceCount; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            _audioSourceInfos.Add(new AudioSourceInfo
            {
                Source = source,
                Priority = -1 // -1表示闲置
            });
        }
    }

    /// <summary>
    /// 初始化默认音量设置
    /// </summary>
    private void InitDefaultVolumes()
    {
        _soundVolume = DefaultSoundVolume;
        _musicVolume = DefaultMusicVolume;
    }
    #endregion

    #region 资源加载与缓存管理
    /// <summary>
    /// 获取音频剪辑（带缓存机制）
    /// </summary>
    /// <param name="fullPath">资源完整路径（基于Resources文件夹）</param>
    private AudioClip GetAudioClip(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
        {
            Debug.LogError("[AudioManager] 音频路径为空，加载失败");
            return null;
        }

        // 检查缓存
        if (_soundClipCache.TryGetValue(fullPath, out AudioClip clip))
        {
            UpdateClipUsage(fullPath);
            return clip;
        }

        // 缓存未命中，通过资源管理器加载
        clip = ResourcesManager.Instance.Load<AudioClip>(fullPath);
        if (clip == null)
        {
            Debug.LogError($"[AudioManager] 加载音频失败，路径：{fullPath}");
            return null;
        }

        // 缓存新加载的资源，并检查缓存上限
        AddClipToCache(fullPath, clip);
        return clip;
    }

    /// <summary>
    /// 添加音频剪辑到缓存（超出上限时淘汰最早使用的资源）
    /// </summary>
    private void AddClipToCache(string path, AudioClip clip)
    {
        _soundClipCache[path] = clip;
        _clipUsageQueue.Enqueue(path);

        // 超出最大缓存数量，移除最早使用的资源
        if (_clipUsageQueue.Count > MaxClipCacheCount)
        {
            string oldestPath = _clipUsageQueue.Dequeue();
            _soundClipCache.Remove(oldestPath);
        }
    }

    /// <summary>
    /// 更新音频剪辑的使用记录（用于缓存淘汰）
    /// </summary>
    private void UpdateClipUsage(string path)
    {
        // 移除旧记录，添加新记录（模拟LRU策略）
        Queue<string> tempQueue = new Queue<string>();
        while (_clipUsageQueue.Count > 0)
        {
            string current = _clipUsageQueue.Dequeue();
            if (current != path)
            {
                tempQueue.Enqueue(current);
            }
        }
        tempQueue.Enqueue(path);
        _clipUsageQueue.Clear();
        while (tempQueue.Count > 0)
        {
            _clipUsageQueue.Enqueue(tempQueue.Dequeue());
        }
    }
    #endregion

    #region 音源池管理
    /// <summary>
    /// 获取可用或优先级最低的音源
    /// </summary>
    private AudioSourceInfo GetAvailableAudioSource(int priority)
    {
        AudioSourceInfo idleSource = null;
        AudioSourceInfo lowestPrioritySource = null;
        int minPriority = int.MaxValue;

        // 遍历查找闲置或最低优先级的音源
        foreach (var info in _audioSourceInfos)
        {
            if (!info.Source.isPlaying)
            {
                idleSource = info;
                break; // 优先使用闲置音源
            }

            if (info.Priority < minPriority)
            {
                minPriority = info.Priority;
                lowestPrioritySource = info;
            }
        }

        // 有闲置音源直接返回
        if (idleSource != null)
        {
            idleSource.Priority = priority;
            return idleSource;
        }

        // 无闲置音源，检查是否可以替换低优先级音源
        if (lowestPrioritySource != null && lowestPrioritySource.Priority < priority)
        {
            lowestPrioritySource.Source.Stop();
            lowestPrioritySource.Priority = priority;
            return lowestPrioritySource;
        }

        // 无法获取可用音源（优先级不足）
        return null;
    }
    #endregion

    #region 音效播放公开方法
    
    /// <summary>
    /// 播放音效（通过资源路径）
    /// </summary>
    /// <param name="path">音频资源路径（基于Resources文件夹）</param>
    /// <param name="priority">优先级（值越大优先级越高）</param>
    /// <param name="volume">音量（-1表示使用默认值）</param>
    public void PlaySound(string path, int priority = 0, float volume = -1)
    {
        AudioClip clip = GetAudioClip(path);
        if (clip == null) return;

        PlaySound(clip, priority, volume, false);
    }

    /// <summary>
    /// 播放音效（通过AudioClip）
    /// </summary>
    /// <param name="clip">音频剪辑</param>
    /// <param name="priority">优先级（值越大优先级越高）</param>
    /// <param name="volume">音量（-1表示使用默认值）</param>
    /// <param name="isLoop">是否循环</param>
    public void PlaySound(AudioClip clip, int priority = 0, float volume = -1, bool isLoop = false)
    {
        if (clip == null)
        {
            Debug.LogError("[AudioManager] 播放失败，音频剪辑为空");
            return;
        }

        AudioSourceInfo sourceInfo = GetAvailableAudioSource(priority);
        if (sourceInfo == null)
        {
            Debug.LogWarning($"[AudioManager] 音效播放失败，优先级不足（当前优先级：{priority}）");
            return;
        }

        // 配置并播放音效
        AudioSource source = sourceInfo.Source;
        source.clip = clip;
        source.volume = volume > 0 ? volume : _soundVolume;
        source.loop = isLoop;
        source.Play();
    }

    /// <summary>
    /// 停止指定音效
    /// </summary>
    /// <param name="clip">需要停止的音频剪辑</param>
    public void StopSound(AudioClip clip)
    {
        if (clip == null) return;

        foreach (var info in _audioSourceInfos)
        {
            if (info.Source.clip == clip)
            {
                info.Source.Stop();
                info.Source.clip = null;
                info.Priority = -1;
            }
        }
    }

    /// <summary>
    /// 停止所有音效
    /// </summary>
    public void StopAllSounds()
    {
        foreach (var info in _audioSourceInfos)
        {
            info.Source.Stop();
            info.Source.clip = null;
            info.Priority = -1;
        }
    }
    #endregion

    #region 背景音乐接口
    /// <summary>
    /// 播放背景音乐
    /// </summary>
    /// <param name="path">音频资源路径（基于Resources文件夹）</param>
    /// <param name="isLoop">是否循环</param>
    public void PlayMusic(string path, bool isLoop = true)
    {
        AudioClip clip = GetAudioClip(path);
        if (clip == null) return;

        _musicSource.Stop();
        _musicSource.clip = clip;
        _musicSource.loop = isLoop;
        _musicSource.volume = _musicVolume;
        _musicSource.Play();
    }

    /// <summary>
    /// 渐入播放背景音乐
    /// </summary>
    /// <param name="path">音频资源路径（基于Resources文件夹）</param>
    /// <param name="duration">渐入时长（秒）</param>
    public void PlayMusicFade(string path, float duration = 1f)
    {
        AudioClip clip = GetAudioClip(path);
        if (clip == null) return;

        _musicSource.Stop();
        _musicSource.clip = clip;
        _musicSource.loop = true;
        _musicSource.volume = 0;
        _musicSource.Play();

        // 使用DOTween实现渐入效果
        _musicSource.DOFade(_musicVolume, duration);
    }

    /// <summary>
    /// 停止背景音乐
    /// </summary>
    public void StopMusic()
    {
        _musicSource.Stop();
        _musicSource.clip = null;
    }

    /// <summary>
    /// 暂停背景音乐
    /// </summary>
    public void PauseMusic()
    {
        if (_musicSource.isPlaying)
        {
            _musicSource.Pause();
        }
    }

    /// <summary>
    /// 恢复背景音乐
    /// </summary>
    public void ResumeMusic()
    {
        if (!_musicSource.isPlaying && _musicSource.clip != null)
        {
            _musicSource.Play();
        }
    }
    #endregion

    #region 音量控制
    /// <summary>
    /// 设置音效音量
    /// </summary>
    /// <param name="volume">音量值（0-1）</param>
    public void SetSoundVolume(float volume)
    {
        _soundVolume = Mathf.Clamp01(volume);

        // 更新所有正在播放的音效音量
        foreach (var info in _audioSourceInfos)
        {
            if (info.Source.isPlaying)
            {
                info.Source.volume = _soundVolume;
            }
        }
    }

    /// <summary>
    /// 设置音乐音量
    /// </summary>
    /// <param name="volume">音量值（0-1）</param>
    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        _musicSource.volume = _musicVolume;
    }

    /// <summary>
    /// 获取当前音效音量
    /// </summary>
    public float GetSoundVolume()
    {
        return _soundVolume;
    }

    /// <summary>
    /// 获取当前音乐音量
    /// </summary>
    public float GetMusicVolume()
    {
        return _musicVolume;
    }
    #endregion

    #region 资源释放
    /// <summary>
    /// 释放所有音频资源缓存
    /// </summary>
    public void ClearCache()
    {
        _soundClipCache.Clear();
        _clipUsageQueue.Clear();
        ResourcesManager.Instance.ReleaseUnusedResources();
        Debug.Log("[AudioManager] 音频缓存已清空");
    }

    /// <summary>
    /// 释放资源（单例销毁时调用）
    /// </summary>
    public override void Release()
    {
        StopAllSounds();
        StopMusic();
        ClearCache();
        base.Release();
    }
    
    #endregion

    /// <summary>
    /// 音源信息内部类（包含音源组件和优先级）
    /// </summary>
    private class AudioSourceInfo
    {
        /// <summary>音频源组件</summary>
        public AudioSource Source;
        /// <summary>优先级（-1表示闲置）</summary>
        public int Priority;
    }
}
    