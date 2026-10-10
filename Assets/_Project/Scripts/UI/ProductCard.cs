using System;
using UnityEngine;
using UnityEngine.UI;

namespace Aayam.UI
{
    /// <summary>
    /// Controls a single product card in the horizontal scroll list.
    /// Attach to the product item prefab.
    /// </summary>
    public class ProductCard : MonoBehaviour
    {
        [SerializeField] private Image           _thumbnail;
        [SerializeField] private Text   _nameText;
        [SerializeField] private Text   _priceText;
        [SerializeField] private Text   _compatText;
        [SerializeField] private Button          _selectButton;
        [SerializeField] private GameObject      _selectedIndicator;

        public event Action<ProductUIItem> OnSelected;
        private ProductUIItem _data;

        public void Bind(ProductUIItem data)
        {
            _data = data;

            if (_thumbnail   != null && data.Thumbnail != null) _thumbnail.sprite = data.Thumbnail;
            if (_nameText    != null) _nameText.text    = data.Name;
            if (_priceText   != null) _priceText.text   = data.PriceFormatted;
            if (_compatText  != null)
            {
                _compatText.text  = data.CompatibilityLabel;
                _compatText.color = data.CompatibilityColor;
            }

            _selectButton?.onClick.RemoveAllListeners();
            _selectButton?.onClick.AddListener(() => OnSelected?.Invoke(_data));
        }

        public void SetSelected(bool selected)
        {
            _selectedIndicator?.SetActive(selected);
        }
    }
}
