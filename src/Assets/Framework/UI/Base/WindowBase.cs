using System;
using System.Collections.Generic;
using DG.Tweening;
using Framework.UI.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Framework.UI.Base
{
    public class WindowBase : WindowBehavior
    {
        #region 私有保护字段
        protected CanvasGroup _canvasGroup;
        protected CanvasGroup _uiMaskCanvasGroup;
        private readonly List<Button> _buttonList = new List<Button>();
        private readonly List<Toggle> _toggleList = new List<Toggle>();
        private readonly List<InputField> _inputFieldList = new List<InputField>();
        private readonly List<Slider> _sliderList = new List<Slider>();
        protected bool _disableAnimation = false;
        protected Transform _uiContent;
        public bool IsFullScreen = false;
        public bool IsPopStack = false;
        public Action<WindowBase> PopStackListener;
        public event Action OnDestroyEvent;
        
        // ✅ 是否使用预制体里设计师设置的 alpha 作为淡入终点
        [SerializeField] protected bool _useDesignerAlpha = true;

        // ✅ 设计态 alpha 缓存
        protected float _designerCanvasAlpha = 1f;
        protected float _designerMaskAlpha = 1f;

        // ✅ 子节点设计态 alpha（UIContent 下每个直接子物体）
        private readonly Dictionary<CanvasGroup, float> _designerChildAlphas = new Dictionary<CanvasGroup, float>();

        #endregion
    
        #region 核心交互方法
        public void HideWindow()
        {
            HideAnimation();
        }

        public T ShowWindow<T>() where T : WindowBase, new()
        {
            return UIManager.Instance.PopUpWindow<T>();
        }

        public void DestroySelf()
        {
            UIManager.Instance.DestroyWindow(Name);
        }

        /// <summary>
        /// 设置是否禁用窗口动画
        /// </summary>
        /// <param name="disable">true=禁用动画，false=启用动画</param>
        public void SetDisableAnimation(bool disable)
        {
            _disableAnimation = disable;
        }
        #endregion

        #region 可见性与遮罩控制
        public override void SetVisible(bool isVisible)
        {
            if (_canvasGroup == null)
            {
                Debug.LogError($"{Name}：未找到CanvasGroup组件，请检查窗口预制体");
                return;
            }

            Visible = isVisible;
            _canvasGroup.interactable = isVisible;
            _canvasGroup.blocksRaycasts = isVisible;

            if (isVisible)
                _canvasGroup.alpha = _useDesignerAlpha ? _designerCanvasAlpha : 1f;
            else
                _canvasGroup.alpha = 0f;

            if (isVisible && IsPopStack)
            {
                GameObject.SetActive(false);
                GameObject.SetActive(true);
            }
        }

        public void SetMaskVisible(bool isVisible)
        {
            if (_uiMaskCanvasGroup == null) return;
            if (!UISetting.Instance?.SINGMASK_SYSTEM ?? true) return;

            _uiMaskCanvasGroup.alpha = isVisible ? 1 : 0;
            _uiMaskCanvasGroup.blocksRaycasts = isVisible;

            if (isVisible && IsPopStack)
            {
                _uiMaskCanvasGroup.gameObject.SetActive(false);
                _uiMaskCanvasGroup.gameObject.SetActive(true);
            }
        }
        #endregion

        #region 控件事件管理
        public void AddButtonClickListener(Button button, UnityAction action)
        {
            if (button == null)
            {
                Debug.LogError($"{Name}：绑定的按钮为空");
                return;
            }

            if (!_buttonList.Contains(button))
            {
                _buttonList.Add(button);
            }
        
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public void AddToggleClickListener(Toggle toggle, UnityAction<bool, Toggle> action)
        {
            if (toggle == null)
            {
                Debug.LogError($"{Name}：绑定的开关为空");
                return;
            }

            if (!_toggleList.Contains(toggle))
            {
                _toggleList.Add(toggle);
            }
        
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(isOn => action?.Invoke(isOn, toggle));
        }

        public void AddInputFieldListener(InputField inputField, 
            UnityAction<string> onChangeAction, 
            UnityAction<string> onEndEditAction)
        {
            if (inputField == null)
            {
                Debug.LogError($"{Name}：绑定的输入框为空");
                return;
            }

            if (!_inputFieldList.Contains(inputField))
            {
                _inputFieldList.Add(inputField);
            }
        
            inputField.onValueChanged.RemoveAllListeners();
            inputField.onEndEdit.RemoveAllListeners();
            inputField.onValueChanged.AddListener(onChangeAction);
            inputField.onEndEdit.AddListener(onEndEditAction);
        }

        public void AddSliderListener(Slider slider, UnityAction<float> action)
        {
            if (slider == null)
            {
                Debug.LogError($"{Name}：绑定的滑动条为空");
                return;
            }

            if (!_sliderList.Contains(slider))
            {
                _sliderList.Add(slider);
            }
        
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(action);
        }

        public void RemoveAllListeners()
        {
            RemoveAllButtonListener();
            RemoveAllToggleListener();
            RemoveAllInputFieldListener();
            RemoveAllSliderListener();
        }

        private void RemoveAllButtonListener()
        {
            foreach (var button in _buttonList)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        private void RemoveAllToggleListener()
        {
            foreach (var toggle in _toggleList)
            {
                toggle.onValueChanged.RemoveAllListeners();
            }
        }

        private void RemoveAllInputFieldListener()
        {
            foreach (var inputField in _inputFieldList)
            {
                inputField.onValueChanged.RemoveAllListeners();
                inputField.onEndEdit.RemoveAllListeners();
            }
        }

        private void RemoveAllSliderListener()
        {
            foreach (var slider in _sliderList)
            {
                slider.onValueChanged.RemoveAllListeners();
            }
        }
        #endregion

        #region 动画相关
        
        // ========== 原有动画代码（已注释保留） ==========
        /*
        public virtual void ShowAnimation()
        {
            if (Canvas == null)
            {
                Debug.LogWarning($"{Name}：ShowAnimation - Canvas 为 null");
                return;
            }
        
            // 如果禁用了动画，直接返回
            if (_disableAnimation)
            {
                Debug.Log($"{Name}：ShowAnimation 被禁用 (_disableAnimation=true)");
                return;
            }
        
            // 执行显示动画（移除了 sortingOrder > 90 的限制，所有窗口都可以有动画）
            Debug.Log($"{Name}：执行 ShowAnimation");
            
            if (_uiMaskCanvasGroup != null)
            {
                _uiMaskCanvasGroup.alpha = 0;
                _uiMaskCanvasGroup.DOFade(1, 0.2f);
            }
            else
            {
                Debug.LogWarning($"{Name}：_uiMaskCanvasGroup 为 null，跳过遮罩动画");
            }
        
            if (_uiContent != null)
            {
                _uiContent.localScale = Vector3.one * 0.8f;
                _uiContent.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
            }
            else
            {
                Debug.LogWarning($"{Name}：_uiContent 为 null，跳过内容动画");
            }
        }

        public virtual void HideAnimation()
        {
            if (Canvas == null)
            {
                UIManager.Instance.HideWindow(Name);
                return;
            }
        
            // 如果禁用了动画，直接隐藏窗口
            if (_disableAnimation)
            {
                UIManager.Instance.HideWindow(Name);
                return;
            }
        
            // 执行隐藏动画（移除了 sortingOrder > 90 的限制）
            if (_uiContent != null)
            {
                _uiContent.DOScale(1.1f, 0.2f)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() => 
                    {
                        UIManager.Instance.HideWindow(Name);
                    });
            }
            else
            {
                // 如果 _uiContent 为 null，直接隐藏窗口
                UIManager.Instance.HideWindow(Name);
            }
        }
        */
        // ========== 原有动画代码结束 ==========
        
        /// <summary>
        /// 从黑屏淡入，界面元素依次呈现，具有层次感
        /// </summary>
        public virtual void ShowAnimation()
        {
            if (Canvas == null)
            {
                Debug.LogWarning($"{Name}：ShowAnimation - Canvas 为 null");
                return;
            }
        
            // 如果禁用了动画，直接返回
            if (_disableAnimation)
            {
                Debug.Log($"{Name}：ShowAnimation 被禁用 (_disableAnimation=true)");
                return;
            }
            
            float targetRoot = _useDesignerAlpha ? _designerCanvasAlpha : 1f;
            float targetMask = _useDesignerAlpha ? _designerMaskAlpha : 1f;
        
            // 确保窗口初始状态为完全透明（黑屏状态）
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
            
            // 整个窗口从黑屏淡入（透明度从0到1）
            // 这是主要的过渡效果，模拟从黑屏到界面呈现
            if (_canvasGroup != null)
            {
                _canvasGroup.DOFade(targetRoot, 0.3f).SetEase(Ease.OutQuad);
            }
            
            // 内容元素依次淡入，增加层次感
            // 首先显示标题区域（如果有的话）
            if (_uiContent != null)
            {
                // 获取所有子元素，实现依次淡入效果
                var children = new List<Transform>();
                for (int i = 0; i < _uiContent.childCount; i++)
                {
                    children.Add(_uiContent.GetChild(i));
                }
                
                // 为每个子元素添加淡入动画，错开时间形成层次感
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    if (child == null) continue;
                    
                    var childCanvasGroup = child.GetComponent<CanvasGroup>();
                    
                    // 如果子元素没有 CanvasGroup，创建一个（用于独立控制透明度）
                    if (childCanvasGroup == null)
                    {
                        childCanvasGroup = child.gameObject.AddComponent<CanvasGroup>();
                    }
                    
                    // ✅ 目标 alpha：优先用“设计态缓存”，没有缓存就默认 1
                    float targetChild = 1f;
                    if (_useDesignerAlpha && _designerChildAlphas.TryGetValue(childCanvasGroup, out var a))
                        targetChild = a;
                    
                    // 初始状态透明
                    childCanvasGroup.alpha = 0f;
                    
                    // 错开时间，依次淡入（每个元素延迟0.05秒，形成层次感）
                    float delay = 0.1f + i * 0.05f;
                    childCanvasGroup.DOFade(targetChild, 0.25f)
                        .SetDelay(delay)
                        .SetEase(Ease.OutQuad);
                }
            }
            
            // 遮罩淡入（如果有遮罩）
            if (_uiMaskCanvasGroup != null)
            {
                _uiMaskCanvasGroup.alpha = 0f;
                _uiMaskCanvasGroup.DOFade(targetMask, 0.3f).SetEase(Ease.OutQuad);
            }
        }

        /// <summary>
        /// 隐藏动画 - 参考《雨中冒险 2》风格
        /// 整个界面（包括背景、所有元素）快速淡出到黑屏，简洁流畅
        /// </summary>
        public virtual void HideAnimation()
        {
            if (Canvas == null)
            {
                UIManager.Instance.HideWindow(Name);
                return;
            }
        
            // 如果禁用了动画，直接隐藏窗口
            if (_disableAnimation)
            {
                UIManager.Instance.HideWindow(Name);
                return;
            }
        
            // 整个窗口淡出到黑屏（透明度从1到0）
            // 这是主要的过渡效果，所有元素同步淡出，形成简洁的黑屏过渡
            if (_canvasGroup != null)
            {
                _canvasGroup.DOFade(0f, 0.25f)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() => 
                    {
                        // 淡出完成后隐藏窗口
                        UIManager.Instance.HideWindow(Name);
                    });
            }
            else
            {
                // 如果没有 CanvasGroup，直接隐藏窗口
                UIManager.Instance.HideWindow(Name);
            }
            
            // 遮罩也同步淡出（如果有遮罩）
            if (_uiMaskCanvasGroup != null)
            {
                _uiMaskCanvasGroup.DOFade(0f, 0.25f).SetEase(Ease.InQuad);
            }
        }
        #endregion
    
        #region 生命周期函数
        public override void OnAwake()
        {
            base.OnAwake();
            InitBaseComponents();
        }

        public override void OnShow()
        {
            base.OnShow();
            ShowAnimation();
        }

        private void InitBaseComponents()
        {
            // 获取根节点CanvasGroup
            _canvasGroup = Transform.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                Debug.LogError($"{Name}：根节点未挂载CanvasGroup组件");
            }

            // 获取遮罩CanvasGroup
            var maskTrans = Transform.Find("UIMask");
            if (maskTrans != null)
            {
                _uiMaskCanvasGroup = maskTrans.GetComponent<CanvasGroup>();
                if (_uiMaskCanvasGroup == null)
                {
                    Debug.LogError($"{Name}：UIMask节点未挂载CanvasGroup组件");
                }
            }
            else
            {
                Debug.LogWarning($"{Name}：未找到UIMask节点，遮罩功能将失效");
            }

            // 获取内容根节点
            _uiContent = Transform.Find("UIContent");
            if (_uiContent == null)
            {
                Debug.LogError($"{Name}：未找到UIContent节点，请检查预制体结构");
            }
            
            CacheDesignerAlphas();
        }
        
        private void CacheDesignerAlphas()
        {
            if (_canvasGroup != null)
                _designerCanvasAlpha = Mathf.Clamp01(_canvasGroup.alpha);

            if (_uiMaskCanvasGroup != null)
                _designerMaskAlpha = Mathf.Clamp01(_uiMaskCanvasGroup.alpha);

            _designerChildAlphas.Clear();
            if (_uiContent != null)
            {
                for (int i = 0; i < _uiContent.childCount; i++)
                {
                    var child = _uiContent.GetChild(i);
                    if (child == null) continue;

                    var cg = child.GetComponent<CanvasGroup>();
                    if (cg != null)
                        _designerChildAlphas[cg] = Mathf.Clamp01(cg.alpha);
                }
            }
        }
        
        
        #endregion
    }
}
