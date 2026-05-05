using System.ComponentModel;

namespace BiliBiliDanmu.Dao
{
    /// <summary>
    /// 礼物排行榜条目。由 <see cref="DanmakuModel"/> 解析 <c>GIFT_TOP</c> 消息时填充。
    /// 实现 <see cref="INotifyPropertyChanged"/> 方便外部绑定刷新。
    /// </summary>
    public class GiftRank : INotifyPropertyChanged
    {
        private decimal _coin;
        private int _uid;
        private string _uid_str;
        private long _uidLong;
        private string _userName;

        /// <summary>用户名。</summary>
        public string UserName
        {
            get => _userName;
            set
            {
                if (value == _userName) return;
                _userName = value;
                OnPropertyChanged(nameof(UserName));
            }
        }

        /// <summary>花销（金瓜子）。</summary>
        public decimal coin
        {
            get => _coin;
            set
            {
                if (value == _coin) return;
                _coin = value;
                OnPropertyChanged(nameof(coin));
            }
        }

        /// <summary>UID（已弃用，B 站用长 UID；超范围会赋 -1，请用 <see cref="uid_long"/> / <see cref="uid_str"/>）。</summary>
        public int uid
        {
            get => _uid;
            set
            {
                if (value == _uid) return;
                _uid = value;
                OnPropertyChanged(nameof(uid));
            }
        }

        /// <summary>长 UID。</summary>
        public long uid_long
        {
            get => _uidLong;
            set
            {
                if (value == _uidLong) return;
                _uidLong = value;
                OnPropertyChanged(nameof(uid_long));
            }
        }

        /// <summary>UID 字符串形式。</summary>
        public string uid_str
        {
            get => _uid_str;
            set
            {
                if (value == _uid_str) return;
                _uid_str = value;
                OnPropertyChanged(nameof(uid_str));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
