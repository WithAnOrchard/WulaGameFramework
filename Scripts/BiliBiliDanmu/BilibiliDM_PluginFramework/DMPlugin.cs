using System;
using System.ComponentModel;
using BilibiliDM_PluginFramework;
using BilibiliDM_PluginFramework.Annotations;
using UnityEngine;

namespace BiliBiliDanmu.BilibiliDM_PluginFramework
{
    public class DMPlugin : INotifyPropertyChanged
    {
        private bool _status;

        /// <summary>
        ///     插件名稱
        /// </summary>
        public string PluginName { get; set; } = "這是插件";

        /// <summary>
        ///     插件作者
        /// </summary>
        public string PluginAuth { get; set; } = "這是作者";

        /// <summary>
        ///     插件作者聯繫方式
        /// </summary>
        public string PluginCont { get; set; } = "這是聯繫方式";

        /// <summary>
        ///     插件版本號
        /// </summary>
        public string PluginVer { get; set; } = "這是版本號";

        /// <summary>
        ///     插件描述
        /// </summary>
        public string PluginDesc { get; set; } = "描述還沒填";

        /// <summary>
        ///     插件描述, 已過期, 請使用PluginDesc
        /// </summary>
        [Obsolete("手滑產品, 請使用PluginDesc")]
        public string PlubinDesc
        {
            get => PluginDesc;
            set => PluginDesc = value;
        }

        /// <summary>
        ///     插件狀態
        /// </summary>
        public bool Status
        {
            get => _status;
            private set
            {
                if (value == _status) return;
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        /// <summary>
        ///     當前連接中的房間
        /// </summary>
        public int? RoomId { get; private set; }


        public event PropertyChangedEventHandler PropertyChanged;
        public event ReceivedDanmakuEvt ReceivedDanmaku;
        public event DisconnectEvt Disconnected;
        public event ReceivedRoomCountEvt ReceivedRoomCount;
        public event ConnectedEvt Connected;

        public void MainConnected(int roomid)
        {
            RoomId = roomid;
            try
            {
                Connected?.Invoke(null, new ConnectedEvtArgs { roomid = roomid });
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex.Message);
            }
        }

        public void MainReceivedDanMaku(ReceivedDanmakuArgs e)
        {
            try
            {
                ReceivedDanmaku?.Invoke(null, e);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex.Message);
            }
        }

        public void MainReceivedRoomCount(ReceivedRoomCountArgs e)
        {
            try
            {
                ReceivedRoomCount?.Invoke(null, e);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex.Message);
            }
        }

        public void MainDisconnected()
        {
            RoomId = null;
            try
            {
                Disconnected?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex.Message);
            }
        }

        /// <summary>
        ///     啟用插件方法 請重寫此方法
        /// </summary>
        public virtual void Start()
        {
            Status = true;
            Debug.Log(PluginName + " Start!");
        }

        /// <summary>
        ///     禁用插件方法 請重寫此方法
        /// </summary>
        public virtual void Stop()
        {
            Status = false;
            Debug.Log(PluginName + " Stop!");
        }

        /// <summary>
        ///     管理插件方法 請重寫此方法
        /// </summary>
        public virtual void Admin()
        {
        }

        /// <summary>
        ///     此方法在所有插件加载完毕后调用
        /// </summary>
        public virtual void Inited()
        {
            Debug.Log("初始化完毕");
        }

        /// <summary>
        ///     反初始化方法, 在弹幕姬主程序退出时调用, 若有需要请重写,
        /// </summary>
        public virtual void DeInit()
        {
        }

        /// <summary>
        ///     打日志
        /// </summary>
        /// <param name="text"></param>
        public void Log(string text)
        {
        }

        /// <summary>
        ///     打彈幕
        /// </summary>
        /// <param name="text"></param>
        /// <param name="fullscreen"></param>
        public void AddDM(string text, bool fullscreen = false)
        {
            //todo
        }

        /// <summary>
        ///     发送伪春菜脚本, 前提是用户有打开伪春菜并允许弹幕姬和伪春菜联动(默认允许)
        /// </summary>
        /// <param name="text">Sakura Script脚本</param>
        public void SendSSPMsg(string text)
        {
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}