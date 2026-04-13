using EssSystem.EssManager.UIManager.Entity;
using UnityEngine;
using UnityEngine.UI;
using EssSystem.UIManager.Dao;

namespace EssSystem.UIManager.Entity
{
    /// <summary>
    /// UI Entity
    /// </summary>
    public class UIPanelEntity : UIEntity
    {
        private Image _image;

        protected override void Awake()
        {
            base.Awake();
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            _image = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            _image.color = Color.clear;
        }

        protected override void SyncFromDao()
        {
            base.SyncFromDao();
            if (Dao is UIPanelComponent panelDao && _image != null)
            {
                _image.color = panelDao.BackgroundColor;
            }
        }

        public Image GetImage() => _image;
    }
}
