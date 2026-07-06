using System.Collections.Generic;

namespace Framework.UI.Editor
{
    /// <summary>
    /// 纯数据类 用于存储控件的基本信息
    /// </summary>
    public class ControlData
    {
        public int InstanceID;
        public string ControlName;
        public string ControlType;
        public List<ControlData> DataList;

    }
}
